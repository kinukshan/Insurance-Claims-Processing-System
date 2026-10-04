using System.Text;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.Users;
using Xunit;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

public class DocumentTypeIntegrityRegressionTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static readonly byte[] DocxZipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00, 0x08, 0x00 };
    private static readonly byte[] JpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

    private static byte[] MakeFakePdfBytes(string text)
    {
        var streamContent = $"BT /F1 12 Tf ({text}) Tj ET";
        var pdf = "%PDF-1.4\n" +
                  "1 0 obj << /Length " + streamContent.Length + " >>\n" +
                  "stream\n" + streamContent + "\nendstream\nendobj\n" +
                  "xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \n" +
                  "trailer << /Size 2 /Root 1 0 R >>\nstartxref\n50\n%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }

    private static readonly byte[] PoliceReportPdfBytes = MakeFakePdfBytes("Police Station Traffic Accident Collision Report Officer FIR");
    private static readonly byte[] RepairEstimatePdfBytes = MakeFakePdfBytes("Auto Body Workshop Vehicle Repair Estimate Parts Labor Quotation");
    private static readonly byte[] DriverLicensePdfBytes = MakeFakePdfBytes("Driving Licence Driver License Number Class Date of birth");

    // ── 1. DOCX tagged as Photos of Damage -> rejected/flagged ──

    [Fact]
    public void Regression1_DocxTaggedAsPhotosOfDamage_IsRejectedWithMismatchFinding()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Photos of Damage",
            FileName = "Assignment__ ABD (1).docx",
            FileUrl = "/uploads/assignment.docx",
            FileSize = DocxZipBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => DocxZipBytes
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Rejected, result.Findings[0].Status);
        Assert.Contains("not valid for 'Photos of Damage'", result.Findings[0].Description);
    }

    // ── 2. Valid JPEG tagged Photos of Damage -> accepted ──

    [Fact]
    public void Regression2_ValidJpegTaggedPhotosOfDamage_IsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Photos of Damage",
            FileName = "damage_front.jpg",
            FileUrl = "/uploads/damage_front.jpg",
            FileSize = JpegBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => JpegBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── 3. Valid PNG tagged Photos of Damage -> accepted ──

    [Fact]
    public void Regression3_ValidPngTaggedPhotosOfDamage_IsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Photos of Damage",
            FileName = "damage_side.png",
            FileUrl = "/uploads/damage_side.png",
            FileSize = PngBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => PngBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── 4. A DOCX renamed to .jpg -> rejected based on actual signature/content ──

    [Fact]
    public void Regression4_DocxRenamedToJpg_RejectedBasedOnActualSignature()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Photos of Damage",
            FileName = "sneaky_damage.jpg",
            FileUrl = "/uploads/sneaky_damage.jpg",
            FileSize = DocxZipBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => DocxZipBytes // File has .jpg extension, but magic bytes are PK\x03\x04
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Rejected, result.Findings[0].Status);
        Assert.Contains("zip_archive", result.Findings[0].Description);
    }

    // ── 5. Correct Police Report PDF -> remains accepted ──

    [Fact]
    public void Regression5_CorrectPoliceReportPdf_RemainsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Police Report",
            FileName = "police_report.pdf",
            FileUrl = "/uploads/police_report.pdf",
            FileSize = PoliceReportPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => PoliceReportPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── 6. Correct Repair Estimate PDF -> remains accepted ──

    [Fact]
    public void Regression6_CorrectRepairEstimatePdf_RemainsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Repair Estimate",
            FileName = "repair_estimate.pdf",
            FileUrl = "/uploads/repair_estimate.pdf",
            FileSize = RepairEstimatePdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => RepairEstimatePdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── 7. Correct Driver License supported format -> remains accepted ──

    [Theory]
    [InlineData("license.jpg", true)]
    [InlineData("license.pdf", false)]
    public void Regression7_CorrectDriverLicense_RemainsAccepted(string fileName, bool isJpeg)
    {
        var bytes = isJpeg ? JpegBytes : DriverLicensePdfBytes;
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Driver License",
            FileName = fileName,
            FileUrl = $"/uploads/{fileName}",
            FileSize = bytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Motor",
            new[] { doc },
            url => bytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── 8. Motor claim with valid docs + invalid DOCX pretending to be Photos of Damage -> Complete = false ──

    [Fact]
    public async Task Regression8_MotorClaimWithInvalidDocxPhotosOfDamage_CompleteFalse()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-01",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 5000m
        };

        storage.SetFileBytes("/uploads/police.pdf", PoliceReportPdfBytes);
        storage.SetFileBytes("/uploads/estimate.pdf", RepairEstimatePdfBytes);
        storage.SetFileBytes("/uploads/license.pdf", DriverLicensePdfBytes);
        storage.SetFileBytes("/uploads/assignment.docx", DocxZipBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "police.pdf", FileUrl = "/uploads/police.pdf", FileSize = PoliceReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "estimate.pdf", FileUrl = "/uploads/estimate.pdf", FileSize = RepairEstimatePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Driver License", FileName = "license.pdf", FileUrl = "/uploads/license.pdf", FileSize = DriverLicensePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "Assignment__ ABD (1).docx", FileUrl = "/uploads/assignment.docx", FileSize = DocxZipBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.False(verifResult.Complete);
        Assert.Contains("Photos of Damage", verifResult.MissingItems);
        Assert.Contains(verifResult.Inconsistencies, i => i.Field == "Photos of Damage" && i.Description.Contains("not valid for 'Photos of Damage'"));

        // Document in repository must be marked Rejected
        var updatedClaim = await claimRepo.GetByIdWithDocumentsAsync(claimId);
        var photosDoc = updatedClaim!.Documents.First(d => d.DocumentType == "Photos of Damage");
        Assert.Equal(DocumentVerificationStatus.Rejected, photosDoc.VerificationStatus);
    }

    // ── 9. Same claim with a valid damage image -> complete = true ──

    [Fact]
    public async Task Regression9_MotorClaimWithValidDamageImage_CompleteTrue()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-02",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 5000m
        };

        storage.SetFileBytes("/uploads/police.pdf", PoliceReportPdfBytes);
        storage.SetFileBytes("/uploads/estimate.pdf", RepairEstimatePdfBytes);
        storage.SetFileBytes("/uploads/license.pdf", DriverLicensePdfBytes);
        storage.SetFileBytes("/uploads/damage.jpg", JpegBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "police.pdf", FileUrl = "/uploads/police.pdf", FileSize = PoliceReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "estimate.pdf", FileUrl = "/uploads/estimate.pdf", FileSize = RepairEstimatePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Driver License", FileName = "license.pdf", FileUrl = "/uploads/license.pdf", FileSize = DriverLicensePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "damage.jpg", FileUrl = "/uploads/damage.jpg", FileSize = JpegBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.True(verifResult.Complete);
        Assert.Empty(verifResult.MissingItems);
    }

    // ── 10. Gemini success must not override deterministic invalid result ──

    [Fact]
    public async Task Regression10_GeminiSuccess_MustNotOverrideDeterministicInvalidResult()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        // AI client erroneously claims complete=true
        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "All documents appear adequate.",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-03",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 5000m
        };

        storage.SetFileBytes("/uploads/assignment.docx", DocxZipBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "Assignment__ ABD (1).docx", FileUrl = "/uploads/assignment.docx", FileSize = DocxZipBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var result = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        // Authoritative deterministic rule must PREVENT Gemini from declaring it complete
        Assert.False(result.Complete);
        Assert.Contains("Photos of Damage", result.MissingItems);
        Assert.Contains(result.Inconsistencies, i => i.Field == "Photos of Damage");
    }

    // ── 11. Gemini failure/fallback must produce the same deterministic decision ──

    [Fact]
    public async Task Regression11_GeminiFailureFallback_ProducesSameDeterministicDecision()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        // AI client throws an unexpected error (service down)
        var aiClient = new FailingAiClient();
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-04",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 5000m
        };

        storage.SetFileBytes("/uploads/assignment.docx", DocxZipBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "Assignment__ ABD (1).docx", FileUrl = "/uploads/assignment.docx", FileSize = DocxZipBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var result = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.False(result.Complete);
        Assert.True(result.FallbackUsed);
        Assert.Contains("Photos of Damage", result.MissingItems);
        Assert.Contains(result.Inconsistencies, i => i.Field == "Photos of Damage");
    }

    // ── 12. Checklist requirement: incompatible document does NOT satisfy requirement ──

    [Fact]
    public async Task Regression12_GetDocumentRequirements_IncompatibleDocx_DoesNotCountAsUploaded()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-05",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted
        };

        // Uploaded document is a docx file tagged as Photos of Damage
        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "Assignment__ ABD (1).docx", FileUrl = "/uploads/assignment.docx", VerificationStatus = DocumentVerificationStatus.Rejected }
        };
        await claimRepo.AddAsync(claim);

        var reqs = await service.GetDocumentRequirementsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.NotNull(reqs);
        var photosReq = reqs.RequiredDocuments.First(r => r.Type == "Photos of Damage");
        Assert.False(photosReq.Uploaded, "Photos of Damage must NOT be marked uploaded when the file is incompatible/rejected.");
        Assert.Equal(0, reqs.UploadedRequiredCount);
        Assert.Equal(4, reqs.MissingCount);
        Assert.False(reqs.Complete);
    }

    // ── 13. AddDocumentAsync: incompatible docx tagged as Photos of Damage is persisted as Rejected ──

    [Fact]
    public async Task Regression13_AddDocumentAsync_DocxPhotosOfDamage_PersistedAsRejected()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-MOTOR-REG-06",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted
        };
        await claimRepo.AddAsync(claim);

        using var stream = new MemoryStream(DocxZipBytes);
        var uploadDto = new UploadDocumentDto(
            DocumentType: "Photos of Damage",
            FileName: "Assignment__ ABD (1).docx",
            ContentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSize: DocxZipBytes.Length,
            FileStream: stream
        );

        var docResult = await service.AddDocumentAsync(claimId, OwnerId, Role.Policyholder, uploadDto);

        Assert.Equal("Rejected", docResult.VerificationStatus);

        var updatedClaim = await claimRepo.GetByIdWithDocumentsAsync(claimId);
        Assert.Single(updatedClaim!.Documents);
        Assert.Equal(DocumentVerificationStatus.Rejected, updatedClaim.Documents.First().VerificationStatus);
    }

    // ── Helper test fakes ──

    private class ConfigurableAiClient : IDocumentVerificationClient
    {
        private readonly DocumentVerificationResultDto _result;
        public ConfigurableAiClient(DocumentVerificationResultDto result) => _result = result;
        public Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, string claimType, List<ClaimDocumentDto> documents, DateTime incidentDate, decimal claimedAmount)
            => Task.FromResult(_result);
    }

    private class FailingAiClient : IDocumentVerificationClient
    {
        public Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, string claimType, List<ClaimDocumentDto> documents, DateTime incidentDate, decimal claimedAmount)
            => throw new HttpRequestException("Gemini service connection refused.");
    }
}
