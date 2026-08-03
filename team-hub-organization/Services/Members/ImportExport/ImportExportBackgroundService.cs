using Microsoft.EntityFrameworkCore;
using Serilog;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.ImportExport;

public sealed class ImportExportBackgroundService(
    IImportExportJobQueue queue,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Recover queued jobs after restart.
        await EnqueuePendingAsync(stoppingToken);

        await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IImportExportJobProcessor>();
                await processor.ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error processing import/export job {JobId}", jobId);
            }
        }
    }

    async Task EnqueuePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var pending = await db.ImportExportJobs.AsNoTracking()
                .Where(j => j.Status == ImportExportJobStatus.Queued)
                .OrderBy(j => j.CreatedAt)
                .Select(j => j.Id)
                .ToListAsync(cancellationToken);

            foreach (var id in pending)
                await queue.EnqueueAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to recover queued import/export jobs on startup");
        }
    }
}
