using Hangfire;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that generates an interview drill plan for a specific application.
/// Calls <see cref="IAiProvider.GenerateInterviewQuestionsAsync"/> in the background so the HTTP
/// request returns immediately (202 Accepted).
/// Fires an <c>applicationInterviewDrillCompleted</c> SSE event on completion so the UI updates.
/// </summary>
public sealed class InterviewDrillJob
{
    private readonly AppDbContext _dbContext;
    private readonly IEventBroadcaster _broadcaster;
    private readonly IAiProvider _aiProvider;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IInterviewDrillRepository _drillRepository;
    private readonly ILogger<InterviewDrillJob> _logger;

    public InterviewDrillJob(
        AppDbContext dbContext,
        IEventBroadcaster broadcaster,
        IAiProvider aiProvider,
        ISettingsRepository settingsRepository,
        IInterviewDrillRepository drillRepository,
        ILogger<InterviewDrillJob> logger)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
        _aiProvider = aiProvider;
        _settingsRepository = settingsRepository;
        _drillRepository = drillRepository;
        _logger = logger;
    }

    /// <summary>
    /// Runs the interview drill generation pipeline:
    /// fetches the application and settings → calls the AI → persists the result → fires SSE event.
    /// </summary>
    /// <param name="applicationId">The ID of the application to generate a drill plan for.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [Queue("critical")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid applicationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting interview drill generation for application {ApplicationId}", applicationId);

        var application = await _dbContext.Applications
            .FindAsync(new object[] { applicationId }, ct)
            .ConfigureAwait(false);

        if (application is null)
        {
            _logger.LogWarning("Application {ApplicationId} not found — interview drill generation aborted", applicationId);
            return;
        }

        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);

        if (!settings.HasResume)
        {
            application.SetInterviewDrillFailed("Resume missing from Settings.");
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await PublishEventAsync(applicationId).ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await _aiProvider.GenerateInterviewQuestionsAsync(
                resumeContent:     settings.ResumeContent!,
                resumeContentType: settings.ResumeContentType!,
                resumeFileName:    settings.ResumeFileName!,
                jobAdContent:      application.JobAdContent ?? application.Description ?? string.Empty,
                requirements:      application.Requirements,
                model:             settings.InterviewTrainerModel,
                maxTokens:         settings.InterviewTrainerMaxTokens,
                ct:                ct)
            .ConfigureAwait(false);

            var questions = result.Questions
                .Select(q => new InterviewQuestion
                {
                    OrderIndex      = q.OrderIndex,
                    Text            = q.Text,
                    Category        = q.Category,
                    RequirementName = q.RequirementName,
                })
                .ToList();

            var plan = InterviewDrillPlan.Create(applicationId, settings.InterviewTrainerModel, questions);
            await _drillRepository.UpsertAsync(plan, CancellationToken.None).ConfigureAwait(false);

            application.ClearInterviewDrillPending();

            _logger.LogInformation("Interview drill generation completed for application {ApplicationId} ({Count} questions)", applicationId, questions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interview drill generation failed for application {ApplicationId}", applicationId);
            application.SetInterviewDrillFailed(ex.Message);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        await PublishEventAsync(applicationId).ConfigureAwait(false);
    }

    private Task PublishEventAsync(Guid applicationId) =>
        _broadcaster.PublishAsync(new WorkcastEvent
        {
            Type          = WorkcastEvent.ApplicationInterviewDrillCompleted,
            ApplicationId = applicationId,
        });
}
