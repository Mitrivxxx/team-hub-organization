using TeamHub.BlobStorage;

namespace team_hub_organization.Tests.Controllers;

internal sealed class FakeBlobStorageService : IBlobStorageService
{
    readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = new(StringComparer.Ordinal);

    public Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        _blobs[blobName] = (memory.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken = default)
    {
        _blobs.Remove(blobName);
        return Task.CompletedTask;
    }

    public Uri? GetReadSasUri(string blobName)
    {
        if (!_blobs.ContainsKey(blobName))
            return null;

        return new Uri($"https://blob.test/{blobName}?sas=fake");
    }

    public bool Contains(string blobName) => _blobs.ContainsKey(blobName);
}
