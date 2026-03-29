using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that generates a tailored HTML application letter for a specific application.
/// Calls <see cref="IAiProvider.GenerateLetterAsync"/> in the background so the HTTP request
/// returns immediately (202 Accepted) and the user can navigate away without breaking the process.
/// Fires an <c>applicationLetterGenerationCompleted</c> SSE event on completion so the UI updates.
/// </summary>
public sealed class ApplicationLetterGenerationJob
{
    private readonly AppDbContext _dbContext;
    private readonly IEventBroadcaster _broadcaster;
    private readonly IAiProvider _aiProvider;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<ApplicationLetterGenerationJob> _logger;

    public ApplicationLetterGenerationJob(
        AppDbContext dbContext,
        IEventBroadcaster broadcaster,
        IAiProvider aiProvider,
        ISettingsRepository settingsRepository,
        ILogger<ApplicationLetterGenerationJob> logger)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
        _aiProvider = aiProvider;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Runs the letter generation pipeline:
    /// fetches the application and settings → calls the AI → persists the result → fires SSE event.
    /// </summary>
    /// <param name="applicationId">The ID of the application to generate a letter for.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [Queue("critical")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid applicationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting letter generation for application {ApplicationId}", applicationId);

        var application = await _dbContext.Applications
            .FindAsync(new object[] { applicationId }, ct)
            .ConfigureAwait(false);

        if (application is null)
        {
            _logger.LogWarning("Application {ApplicationId} not found — letter generation aborted", applicationId);
            return;
        }

        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);

        if (!settings.HasResume)
        {
            application.SetLetterGenerationFailed("Resume missing from Settings.");
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await _broadcaster.PublishAsync(new WorkcastEvent
            {
                Type          = WorkcastEvent.ApplicationLetterGenerationCompleted,
                ApplicationId = applicationId,
            }).ConfigureAwait(false);
            return;
        }

        try
        {
            var requirementsJson = System.Text.Json.JsonSerializer.Serialize(application.Requirements);

            var htmlContent = await _aiProvider.GenerateLetterAsync(
                resumeContent:           settings.ResumeContent!,
                resumeContentType:       settings.ResumeContentType!,
                resumeFileName:          settings.ResumeFileName!,
                jobAdContent:            application.JobAdContent ?? application.Description ?? string.Empty,
                jobTitle:                application.Title,
                company:                 application.Company,
                scoringSummary:          application.Summary ?? string.Empty,
                scoringRecommendation:   application.Recommendation ?? string.Empty,
                scoringRequirementsJson: requirementsJson,
                ct:                      ct)
            .ConfigureAwait(false);

            var existing = await _dbContext.GeneratedLetters
                .Where(l => l.ApplicationId == applicationId)
                .OrderByDescending(l => l.GeneratedAt)
                .FirstOrDefaultAsync(CancellationToken.None)
                .ConfigureAwait(false);

            if (existing is not null)
                _dbContext.GeneratedLetters.Remove(existing);

            _dbContext.GeneratedLetters.Add(GeneratedLetter.Create(applicationId, htmlContent, settings.LetterGenerationModel));
            application.ClearLetterGenerationPending();

            _logger.LogInformation("Letter generation completed for application {ApplicationId}", applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Letter generation failed for application {ApplicationId}", applicationId);
            application.SetLetterGenerationFailed(ex.Message);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        await _broadcaster.PublishAsync(new WorkcastEvent
        {
            Type          = WorkcastEvent.ApplicationLetterGenerationCompleted,
            ApplicationId = applicationId,
        }).ConfigureAwait(false);
    }
}
