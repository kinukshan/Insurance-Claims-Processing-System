using System.Text;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.RiskAssessment.DTOs;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Application.RiskAssessment.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsuranceClaims.UnitTests.RiskAssessment;

public class DocumentIntegrityAndFraudRiskTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StubRiskAssessmentRepository _repository;
    private readonly StubAiRiskClient _aiClient;
    private readonly MockStorageService _storageService;
    private readonly RiskAssessmentService _service;

    public DocumentIntegrityAndFraudRiskTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocIntegrityTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _repository = new StubRiskAssessmentRepository(_dbContext);
        _aiClient = new StubAiRiskClient();
        _storageService = new MockStorageService();
        _service = new RiskAssessmentService(_repository, _aiClient, _storageService);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static byte[] MakeFakePdfBytes(string text)
    {
        // Construct a simple valid uncompressed PDF stream containing the text
        var streamContent = $"BT /F1 12 Tf ({text}) Tj ET";
        var pdf = "%PDF-1.4\n" +
                  "1 0 obj << /Length " + streamContent.Length + " >>\n" +
                  "stream\n" + streamContent + "\nendstream\nendobj\n" +
                  "xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \n" +
                  "trailer << /Size 2 /Root 1 0 R >>\nstartxref\n50\n%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }

    [Fact]
    public async Task ExactRegression_BeneficiaryPdfUploadedAsPoliceReport_ProducesMismatchAndMediumRisk()
    {
        // Arrange: Motor claim with all 4 required document types uploaded
        var claimId = Guid.NewGuid();
        var policyHolderId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = policyHolderId,
            ClaimType = ClaimType.Auto,
            Description = "Motor vehicle accident",
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Status = ClaimStatus.Submitted
        };

        // Beneficiary identification text inside a PDF
        var fakePoliceBytes = MakeFakePdfBytes("Beneficiary Nominee Identification relationship to insured nominee spouse full name NIC");
        var fakePhotoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }; // JPEG
        var fakeEstimateBytes = MakeFakePdfBytes("Auto Body Repair Workshop. Vehicle repair estimate parts labor total $5000.");
        var fakeLicenseBytes = MakeFakePdfBytes("Driving Licence Driver License Class of Vehicle Date of birth 1990-01-01.");

        _storageService.AddFile("/uploads/fake_police.pdf", fakePoliceBytes);
        _storageService.AddFile("/uploads/photo.jpg", fakePhotoBytes);
        _storageService.AddFile("/uploads/estimate.pdf", fakeEstimateBytes);
        _storageService.AddFile("/uploads/license.pdf", fakeLicenseBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "dummy_beneficiary_nominee_identification.pdf", FileUrl = "/uploads/fake_police.pdf", FileSize = fakePoliceBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "photo.jpg", FileUrl = "/uploads/photo.jpg", FileSize = fakePhotoBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "estimate.pdf", FileUrl = "/uploads/estimate.pdf", FileSize = fakeEstimateBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Driver License", FileName = "license.pdf", FileUrl = "/uploads/license.pdf", FileSize = fakeLicenseBytes.Length },
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        // Act: Assess claim
        var result = await _service.AssessClaimAsync(claimId, new AssessClaimRequest { IncludeAiAnalysis = false });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FraudFlagCount >= 1, "Expected at least 1 fraud flag for document mismatch.");
        Assert.Equal(40.0m, result.RiskScore);
        Assert.Equal("Medium", result.RiskLevelDisplay);
        Assert.Equal(RiskRecommendation.Escalate, result.Recommendation);

        var flags = await _service.GetFlagsAsync(claimId);
        var mismatchFlag = flags.FirstOrDefault(f => f.FlagType == FraudFlagType.DocumentTypeMismatch);
        Assert.NotNull(mismatchFlag);
        Assert.Contains("Beneficiary / Nominee Identification", mismatchFlag.Description);
    }

    [Fact]
    public async Task MultipleMismatchedDocuments_ProducesHighScoreAndEscalate()
    {
        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = Guid.NewGuid(),
            ClaimType = ClaimType.Auto,
            Description = "Accident",
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Status = ClaimStatus.Submitted
        };

        var fakePoliceBytes = MakeFakePdfBytes("Beneficiary Nominee Identification spouse NIC");
        var fakeEstimateBytes = MakeFakePdfBytes("Airline flight booking itinerary passenger e-ticket");

        _storageService.AddFile("/uploads/doc1.pdf", fakePoliceBytes);
        _storageService.AddFile("/uploads/doc2.pdf", fakeEstimateBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "doc1.pdf", FileUrl = "/uploads/doc1.pdf", FileSize = fakePoliceBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "doc2.pdf", FileUrl = "/uploads/doc2.pdf", FileSize = fakeEstimateBytes.Length },
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AssessClaimAsync(claimId, new AssessClaimRequest { IncludeAiAnalysis = false });

        Assert.True(result.RiskScore >= 80.0m, $"Expected score >= 80, got {result.RiskScore}");
        Assert.True(result.RiskLevel == RiskLevel.High || result.RiskLevel == RiskLevel.Critical);
        Assert.Equal(RiskRecommendation.Escalate, result.Recommendation);
        Assert.True(result.FraudFlagCount >= 2);
    }

    [Fact]
    public async Task ValidMotorDocuments_ProducesZeroDocumentRiskAndProceed()
    {
        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = Guid.NewGuid(),
            ClaimType = ClaimType.Auto,
            Description = "Accident",
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Status = ClaimStatus.Submitted
        };

        var policeBytes = MakeFakePdfBytes("Police Station Traffic Department. FIR Incident Report by Officer regarding traffic accident collision.");
        var photoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var estimateBytes = MakeFakePdfBytes("Automotive Workshop Repair Estimate. Labour, replacement parts, total estimated amount.");
        var licenseBytes = MakeFakePdfBytes("Driver License Class of vehicle Motor Car. Driving Licence DOB 1990.");

        _storageService.AddFile("/uploads/v_police.pdf", policeBytes);
        _storageService.AddFile("/uploads/v_photo.jpg", photoBytes);
        _storageService.AddFile("/uploads/v_estimate.pdf", estimateBytes);
        _storageService.AddFile("/uploads/v_license.pdf", licenseBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "police.pdf", FileUrl = "/uploads/v_police.pdf", FileSize = policeBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Photos of Damage", FileName = "photo.jpg", FileUrl = "/uploads/v_photo.jpg", FileSize = photoBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "estimate.pdf", FileUrl = "/uploads/v_estimate.pdf", FileSize = estimateBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Driver License", FileName = "license.pdf", FileUrl = "/uploads/v_license.pdf", FileSize = licenseBytes.Length },
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AssessClaimAsync(claimId, new AssessClaimRequest { IncludeAiAnalysis = false });

        Assert.Equal(0.0m, result.RiskScore);
        Assert.Equal("Low", result.RiskLevelDisplay);
        Assert.Equal(RiskRecommendation.Proceed, result.Recommendation);
        Assert.Equal(0, result.FraudFlagCount);
    }

    [Fact]
    public async Task DuplicateFileHashReuse_AcrossDifferentDocumentTypes_FlagsDuplicateReuse()
    {
        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = Guid.NewGuid(),
            ClaimType = ClaimType.Auto,
            Description = "Accident",
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Status = ClaimStatus.Submitted
        };

        var identicalBytes = MakeFakePdfBytes("Some generic document content.");
        _storageService.AddFile("/uploads/docA.pdf", identicalBytes);
        _storageService.AddFile("/uploads/docB.pdf", identicalBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "docA.pdf", FileUrl = "/uploads/docA.pdf", FileSize = identicalBytes.Length },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Repair Estimate", FileName = "docB.pdf", FileUrl = "/uploads/docB.pdf", FileSize = identicalBytes.Length },
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AssessClaimAsync(claimId, new AssessClaimRequest { IncludeAiAnalysis = false });

        var flags = await _service.GetFlagsAsync(claimId);
        Assert.Contains(flags, f => f.FlagType == FraudFlagType.DuplicateDocumentReused);
        Assert.True(result.RiskScore >= 25.0m);
    }

    [Fact]
    public async Task GeminiFallback_ObviousMismatchDocuments_StillFlagsMismatchDeterministically()
    {
        // When AI client is unavailable (returns null)
        _aiClient.ConfiguredResult = null;

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = Guid.NewGuid(),
            ClaimType = ClaimType.Auto,
            Description = "Accident",
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Status = ClaimStatus.Submitted
        };

        var fakePoliceBytes = MakeFakePdfBytes("Beneficiary Nominee Identification spouse NIC");
        _storageService.AddFile("/uploads/mismatch.pdf", fakePoliceBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Police Report", FileName = "mismatch.pdf", FileUrl = "/uploads/mismatch.pdf", FileSize = fakePoliceBytes.Length },
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AssessClaimAsync(claimId, new AssessClaimRequest { IncludeAiAnalysis = true });

        // Deterministic mismatch flag and medium risk score must be preserved
        Assert.NotNull(result);
        Assert.True(result.RiskScore >= 40.0m);
        Assert.Equal(RiskRecommendation.Escalate, result.Recommendation);
        Assert.True(result.FraudFlagCount >= 1);

        var flags = await _service.GetFlagsAsync(claimId);
        Assert.Contains(flags, f => f.FlagType == FraudFlagType.DocumentTypeMismatch);
    }
}

internal class MockStorageService : IDocumentStorageService
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string url, byte[] bytes) => _files[url] = bytes;

    public Task<string> UploadAsync(string fileName, string contentType, Stream fileStream) => Task.FromResult($"/uploads/{fileName}");
    public Task<bool> DeleteAsync(string fileUrl) => Task.FromResult(_files.Remove(fileUrl));
    public Task<byte[]?> GetFileBytesAsync(string fileUrl) => Task.FromResult(_files.TryGetValue(fileUrl, out var b) ? b : null);
}
