using System.Security.Cryptography;

namespace ReciclaApi.Infrastructure.Storage;

public sealed record StoredFile(
    string NombreArchivo,
    string RutaLocal,
    string UrlPublica,
    string HashSha256,
    long TamanoBytes);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default);
}

public sealed class LocalFileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var storedName = $"{Guid.NewGuid():N}_{safeFileName}";
        var normalizedFolder = folder.Replace('\\', '/').Trim('/');
        var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(root, normalizedFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, storedName);
        await using (var fileStream = File.Create(fullPath))
            await content.CopyToAsync(fileStream, cancellationToken);

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new StoredFile(
            storedName,
            fullPath,
            $"/{normalizedFolder}/{storedName}",
            hash,
            bytes.LongLength);
    }
}
