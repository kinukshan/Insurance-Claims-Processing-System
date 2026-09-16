using InsuranceClaims.Application.ClaimsManagement.DTOs;

namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Client contract for communicating with the internal AI Document Verification Agent.
/// Architecture: ASP.NET Core -> AI Service (internal only).
/// </summary>
public interface IDocumentVerificationClient
{
    /// <summary>
    /// Sends claim data and documents to the Document Verification Agent
    /// and returns the structured verification result.
    /// </summary>
    Task<DocumentVerificationResultDto> VerifyDocumentsAsync(
        Guid claimId,
        string claimType,
        List<ClaimDocumentDto> documents,
        DateTime incidentDate,
        decimal claimedAmount);
}
