using InsuranceClaims.Application.ClaimsManagement.Interfaces;

namespace InsuranceClaims.Infrastructure.ExternalServices;

/// <summary>
/// Local filesystem storage for development.
/// Stores files in a local uploads directory.
/// Will be replaced with cloud storage (e.g., Azure Blob, S3) in production.
/// No cloud credentials are hard-coded.
/// </summary>
public class LocalFileStorageService : IDocumentStorageService
{
    private readonly string _uploadDirectory;

    public LocalFileStorageService()
    {
        _uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(_uploadDirectory))
            Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task<string> UploadAsync(string fileName, string contentType, Stream fileStream)
    {
        var uniqueName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(_uploadDirectory, uniqueName);

        using var output = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(output);

        // Return a relative URL for the stored file
        return $"/uploads/{uniqueName}";
    }

    public Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return Task.FromResult(false);

            // Avoid path traversal by extracting only the file name component
            var fileName = Path.GetFileName(fileUrl);
            var filePath = Path.Combine(_uploadDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<byte[]?> GetFileBytesAsync(string fileUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return null;

            var fileName = Path.GetFileName(fileUrl);
            var filePath = Path.Combine(_uploadDirectory, fileName);

            if (!File.Exists(filePath))
                return null;

            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
