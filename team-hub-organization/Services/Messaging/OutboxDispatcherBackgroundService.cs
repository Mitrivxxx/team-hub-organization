using Microsoft.EntityFrameworkCore;
using Serilog;
using TeamHub.Kafka;
using team_hub_organization.Data;

namespace team_hub_organization.Services.Messaging;

public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error during outbox dispatch");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await producer.ProduceRawAsync(
                    message.Topic,
                    message.Payload,
                    key: message.PartitionKey,
                    cancellationToken);

                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                message.AttemptCount += 1;
                message.LastError = ex.Message;
                await db.SaveChangesAsync(cancellationToken);
                Log.Error(
                    ex,
                    "Failed to dispatch outbox message {OutboxId} type {EventType} (attempt {Attempt})",
                    message.Id,
                    message.EventType,
                    message.AttemptCount);
                // Stop this batch so older messages keep priority; retry next poll.
                break;
            }
        }
    }
}
