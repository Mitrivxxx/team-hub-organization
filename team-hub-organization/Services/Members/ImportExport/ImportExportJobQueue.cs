using System.Threading.Channels;

namespace team_hub_organization.Services.Members.ImportExport;

public interface IImportExportJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ImportExportJobQueue : IImportExportJobQueue
{
    readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(jobId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
