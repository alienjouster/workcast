using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that generates a tailored HTML resume for a specific application.
/// Calls <see cref="IAiProvider.GenerateResumeAsync"/> in the background so the HTTP request
/// returns immediately (202 Accepted) and the user can navigate away without breaking the process.
/// Fires an <c>applicationResumeGenerationCompleted</c> SSE event on completion so the UI updates.
/// </summary>
public sealed class ApplicationResumeGenerationJob
{
    private readonly AppDbContext _dbContext;
    private readonly IEventBroadcaster _broadcaster;
    private readonly IAiProvider _aiProvider;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<ApplicationResumeGenerationJob> _logger;

    public ApplicationResumeGenerationJob(
        AppDbContext dbContext,
        IEventBroadcaster broadcaster,
        IAiProvider aiProvider,
        ISettingsRepository settingsRepository,
        ILogger<ApplicationResumeGenerationJob> logger)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
        _aiProvider = aiProvider;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Runs the resume generation pipeline:
    /// fetches the application and settings → calls the AI → persists the result → fires SSE event.
    /// </summary>
    /// <param name="applicationId">The ID of the application to generate a resume for.</param>
    /// <param name="optimizationLevel">Controls how aggressively the AI may adapt the resume content.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [Queue("critical")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid applicationId, ResumeOptimizationLevel optimizationLevel, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting resume generation for application {ApplicationId} (optimization: {Level})", applicationId, optimizationLevel);

        var application = await _dbContext.Applications
            .FindAsync(new object[] { applicationId }, ct)
            .ConfigureAwait(false);

        if (application is null)
        {
            _logger.LogWarning("Application {ApplicationId} not found — resume generation aborted", applicationId);
            return;
        }

        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);

        if (!settings.HasResume || !settings.HasResumeTemplate)
        {
            application.SetResumeGenerationFailed("Resume or template missing from Settings.");
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await _broadcaster.PublishAsync(new WorkcastEvent
            {
                Type          = WorkcastEvent.ApplicationResumeGenerationCompleted,
                ApplicationId = applicationId,
            }).ConfigureAwait(false);
            return;
        }

        try
        {
            var requirementsJson = System.Text.Json.JsonSerializer.Serialize(application.Requirements);

            var htmlContent = await _aiProvider.GenerateResumeAsync(
                resumeContent:           settings.ResumeContent!,
                resumeContentType:       settings.ResumeContentType!,
                resumeFileName:          settings.ResumeFileName!,
                resumeTemplateHtml:      settings.ResumeTemplateContent!,
                jobAdContent:            application.JobAdContent ?? application.Description ?? string.Empty,
                scoringSummary:          application.Summary ?? string.Empty,
                scoringRecommendation:   application.Recommendation ?? string.Empty,
                scoringRequirementsJson: requirementsJson,
                optimizationLevel:       optimizationLevel,
                ct:                      ct)
            .ConfigureAwait(false);

            var existing = await _dbContext.GeneratedResumes
                .Where(r => r.ApplicationId == applicationId)
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefaultAsync(CancellationToken.None)
                .ConfigureAwait(false);

            if (existing is not null)
                _dbContext.GeneratedResumes.Remove(existing);

            _dbContext.GeneratedResumes.Add(GeneratedResume.Create(applicationId, htmlContent, settings.ResumeGenerationModel));
            application.ClearResumeGenerationPending();

            _logger.LogInformation("Resume generation completed for application {ApplicationId}", applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume generation failed for application {ApplicationId}", applicationId);
            application.SetResumeGenerationFailed(ex.Message);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        await _broadcaster.PublishAsync(new WorkcastEvent
        {
            Type          = WorkcastEvent.ApplicationResumeGenerationCompleted,
            ApplicationId = applicationId,
        }).ConfigureAwait(false);
    }
}
