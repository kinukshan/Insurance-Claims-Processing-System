namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Abstraction for file storage. Allows swapping local dev storage for cloud storage later.
/// Only document metadata/URLs are stored in PostgreSQL.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Stores a file and returns the URL/reference to it.
    /// </summary>
    Task<string> UploadAsync(string fileName, string contentType, Stream fileStream);

    /// <summary>
    /// Deletes a stored file by its URL/reference.
    /// </summary>
    Task<bool> DeleteAsync(string fileUrl);
}
