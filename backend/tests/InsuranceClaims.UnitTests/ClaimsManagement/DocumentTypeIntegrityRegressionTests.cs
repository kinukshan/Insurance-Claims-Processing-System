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

    // ══════════════════════════════════════════════════════════════════════════
    // HEALTH SEMANTIC CONTENT VALIDATION REGRESSION TESTS (1-15)
    // ══════════════════════════════════════════════════════════════════════════

    private static byte[] MakeScannedPdfWithoutTextBytes()
    {
        var pdf = "%PDF-1.4\n" +
                  "1 0 obj << /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
                  "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
                  "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n" +
                  "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n" +
                  "trailer << /Size 4 /Root 1 0 R >>\nstartxref\n190\n%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }

    private static readonly byte[] ArchitecturePdfBytes = MakeFakePdfBytes(
        "Lecture 06 Software Architecture Patterns Model View Controller MVC Layered Architecture Event Driven Microservices Design Patterns Dependency Injection University Course Slides");

    private static readonly byte[] GenuineDoctorReferralPdfBytes = MakeFakePdfBytes(
        "Doctor Referral Letter Date 22 Sep 2026 To Orthopedic Department Hospital Patient Saki Ki Patient ID 123 Referral Reason kindly evaluate the patient for further management Referring Doctor Dr Saman Perera MBBS");

    private static readonly byte[] DoctorReferralVariationPdfBytes = MakeFakePdfBytes(
        "Consultation Request and Referral To Cardiology Specialist Clinic We refer this patient Mr David Perera for second opinion and clinical review of chest symptoms Referring Physician Dr Nimal Silva General Practitioner");

    private static readonly byte[] MedicalReportPdfBytes = MakeFakePdfBytes(
        "General Hospital Colombo Medical Examination Clinical Report Patient Name Jane Doe Attending Physician Dr Silva MD Clinical Findings Patient presents with acute condition Diagnosis Closed displaced fracture Treatment plan Surgical reduction");

    private static readonly byte[] HospitalBillPdfBytes = MakeFakePdfBytes(
        "Asiri Central Hospital Invoice and Billing Statement Bill No INV 98241 Patient John Doe Room charges pharmacy charges Subtotal LKR 45000 Total Amount Due LKR 52500 Payment method Cash Card");

    private static readonly byte[] PrescriptionPdfBytes = MakeFakePdfBytes(
        "Dr Kamal Perera Clinic Medical Prescription Patient Saki Ki Rx Amoxicillin 500mg capsules 1 capsule 3 times daily for 7 days Paracetamol 500mg tablets 2 tablets every 6 hours Dosage PRN for pain Doctor Physician Dr Kamal Perera");

    // ── Test 1: Software architecture lecture PDF tagged Doctor Referral -> Mismatch, not Verified, missing, Complete = false ──
    [Fact]
    public async Task Regression_Health1_ArchitecturePdfTaggedDoctorReferral_IsMismatchAndNotVerified()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "06 - Architecture Patterns.pdf",
            FileUrl = "/uploads/architecture.pdf",
            FileSize = ArchitecturePdfBytes.Length
        };

        var evalResult = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => ArchitecturePdfBytes
        );

        Assert.True(evalResult.HasMismatches);
        Assert.Single(evalResult.Findings);
        Assert.Equal(DocumentVerificationStatus.Mismatch, evalResult.Findings[0].Status);
        Assert.Contains("not appear consistent with Doctor Referral", evalResult.Findings[0].Description);

        // Verify service integration
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: false,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: false,
            AiProvider: null,
            AiModel: null,
            ReasoningSummary: null,
            FallbackUsed: false
        ));
        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-01",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };
        storage.SetFileBytes("/uploads/architecture.pdf", ArchitecturePdfBytes);
        claim.Documents = new List<ClaimDocument> { doc };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.False(verifResult.Complete);
        Assert.Contains("Doctor Referral", verifResult.MissingItems);

        var reqs = await service.GetDocumentRequirementsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.NotNull(reqs);
        var referralReq = reqs.RequiredDocuments.First(r => r.Type == "Doctor Referral");
        Assert.False(referralReq.Uploaded);
        Assert.False(reqs.Complete);
    }

    // ── Test 2: Valid dummy Doctor Referral PDF -> accepted / Verified, satisfies Doctor Referral ──
    [Fact]
    public void Regression_Health2_ValidDummyDoctorReferral_IsAcceptedAndVerified()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "dummy_doctor_referral.pdf",
            FileUrl = "/uploads/dummy_doctor_referral.pdf",
            FileSize = GenuineDoctorReferralPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => GenuineDoctorReferralPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── Test 3: Valid Doctor Referral wording variation -> accepted without requiring exact template ──
    [Fact]
    public void Regression_Health3_DoctorReferralVariation_IsAcceptedWithoutExactTemplate()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "referral_specialist.pdf",
            FileUrl = "/uploads/referral_specialist.pdf",
            FileSize = DoctorReferralVariationPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => DoctorReferralVariationPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── Test 4: Medical Report PDF tagged Doctor Referral -> must not satisfy Doctor Referral ──
    [Fact]
    public void Regression_Health4_MedicalReportTaggedDoctorReferral_IsMismatch()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "medical_report_as_referral.pdf",
            FileUrl = "/uploads/medical_report.pdf",
            FileSize = MedicalReportPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => MedicalReportPdfBytes
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Mismatch, result.Findings[0].Status);
    }

    // ── Test 5: Hospital Bill tagged Medical Report -> Mismatch ──
    [Fact]
    public void Regression_Health5_HospitalBillTaggedMedicalReport_IsMismatch()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Medical Report",
            FileName = "hospital_bill_as_report.pdf",
            FileUrl = "/uploads/hospital_bill.pdf",
            FileSize = HospitalBillPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => HospitalBillPdfBytes
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Mismatch, result.Findings[0].Status);
    }

    // ── Test 6: Prescription tagged Hospital Bills -> Mismatch ──
    [Fact]
    public void Regression_Health6_PrescriptionTaggedHospitalBills_IsMismatch()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Hospital Bills",
            FileName = "prescription_as_bill.pdf",
            FileUrl = "/uploads/prescription.pdf",
            FileSize = PrescriptionPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => PrescriptionPdfBytes
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Mismatch, result.Findings[0].Status);
    }

    // ── Test 7: Valid Medical Report -> accepted ──
    [Fact]
    public void Regression_Health7_ValidMedicalReport_IsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Medical Report",
            FileName = "medical_report.pdf",
            FileUrl = "/uploads/medical_report.pdf",
            FileSize = MedicalReportPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => MedicalReportPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── Test 8: Valid Hospital Bill -> accepted ──
    [Fact]
    public void Regression_Health8_ValidHospitalBill_IsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Hospital Bills",
            FileName = "hospital_bill.pdf",
            FileUrl = "/uploads/hospital_bill.pdf",
            FileSize = HospitalBillPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => HospitalBillPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── Test 9: Valid Prescription -> accepted ──
    [Fact]
    public void Regression_Health9_ValidPrescription_IsAccepted()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Prescription",
            FileName = "prescription.pdf",
            FileUrl = "/uploads/prescription.pdf",
            FileSize = PrescriptionPdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => PrescriptionPdfBytes
        );

        Assert.False(result.HasMismatches);
        Assert.Empty(result.Findings);
    }

    // ── Test 10: Valid PDF with no extractable text, tagged Doctor Referral -> Unreadable / Needs Review ──
    [Fact]
    public void Regression_Health10_ValidPdfWithNoExtractableText_IsUnreadable()
    {
        var scannedBytes = MakeScannedPdfWithoutTextBytes();
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "scanned_doctor_referral.pdf",
            FileUrl = "/uploads/scanned.pdf",
            FileSize = scannedBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => scannedBytes
        );

        Assert.True(result.HasUnreadable);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Unreadable, result.Findings[0].Status);
        Assert.Contains("insufficient or unreadable text", result.Findings[0].Description);
    }

    // ── Test 11: Architecture PDF renamed to doctor_referral.pdf -> still mismatch based on content ──
    [Fact]
    public void Regression_Health11_ArchitecturePdfRenamedToDoctorReferral_StillMismatch()
    {
        var doc = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = "Doctor Referral",
            FileName = "doctor_referral.pdf",
            FileUrl = "/uploads/doctor_referral.pdf",
            FileSize = ArchitecturePdfBytes.Length
        };

        var result = DocumentIntegrityValidator.EvaluateClaimDocuments(
            "Health",
            new[] { doc },
            url => ArchitecturePdfBytes
        );

        Assert.True(result.HasMismatches);
        Assert.Single(result.Findings);
        Assert.Equal(DocumentVerificationStatus.Mismatch, result.Findings[0].Status);
    }

    // ── Test 12: Gemini success claiming everything is complete -> cannot override deterministic mismatch ──
    [Fact]
    public async Task Regression_Health12_GeminiSuccessClaimingComplete_CannotOverrideDeterministicMismatch()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "All health documents look completely genuine and verified.",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-12",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/architecture.pdf", ArchitecturePdfBytes);
        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "06 - Architecture Patterns.pdf", FileUrl = "/uploads/architecture.pdf", FileSize = ArchitecturePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var result = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        // Even though Gemini claimed complete, authoritative deterministic validation must force Complete = false
        Assert.False(result.Complete);
        Assert.Contains("Doctor Referral", result.MissingItems);
        Assert.Contains(result.Inconsistencies, i => i.Field == "Doctor Referral");
    }

    // ── Test 13: Gemini unavailable / 503 / 504 -> deterministic mismatch remains, Complete = false, FallbackUsed = true ──
    [Fact]
    public async Task Regression_Health13_GeminiUnavailableFallback_DeterministicMismatchRemains()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FailingAiClient();

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-13",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/architecture.pdf", ArchitecturePdfBytes);
        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "06 - Architecture Patterns.pdf", FileUrl = "/uploads/architecture.pdf", FileSize = ArchitecturePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var result = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.False(result.Complete);
        Assert.True(result.FallbackUsed);
        Assert.Contains("Doctor Referral", result.MissingItems);
        Assert.Contains(result.Inconsistencies, i => i.Field == "Doctor Referral");
    }

    // ── Test 14: Health claim: 3 valid docs + architecture PDF tagged Doctor Referral -> Complete = false ──
    [Fact]
    public async Task Regression_Health14_ThreeValidDocsPlusArchitectureAsDoctorReferral_IsIncomplete()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "AI evaluation",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-14",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        storage.SetFileBytes("/uploads/architecture.pdf", ArchitecturePdfBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "06 - Architecture Patterns.pdf", FileUrl = "/uploads/architecture.pdf", FileSize = ArchitecturePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.False(verifResult.Complete);
        Assert.Contains("Doctor Referral", verifResult.MissingItems);

        var reqs = await service.GetDocumentRequirementsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.NotNull(reqs);
        Assert.Equal(3, reqs.UploadedRequiredCount);
        Assert.Equal(1, reqs.MissingCount);
        Assert.False(reqs.Complete);
    }

    // ── Test 15: Same Health claim with genuine Doctor Referral -> Complete = true ──
    [Fact]
    public async Task Regression_Health15_AllFourValidHealthDocs_IsComplete()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "All documents valid",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-15",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        storage.SetFileBytes("/uploads/dummy_doctor_referral.pdf", GenuineDoctorReferralPdfBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "dummy_doctor_referral.pdf", FileUrl = "/uploads/dummy_doctor_referral.pdf", FileSize = GenuineDoctorReferralPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.True(verifResult.Complete);
        Assert.Empty(verifResult.MissingItems);

        var reqs = await service.GetDocumentRequirementsAsync(claimId, OwnerId, Role.Policyholder);
        Assert.NotNull(reqs);
        Assert.Equal(4, reqs.UploadedRequiredCount);
        Assert.Equal(0, reqs.MissingCount);
        Assert.True(reqs.Complete);
    }

    // ── Test 16: All four valid Health documents, AI service returns 502 fallback -> Complete = true, FallbackUsed = true ──
    [Fact]
    public async Task Regression_Health16_AllFourValidHealthDocs_AiService502Fallback_CompleteIsTrue()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: false,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string> { "Document verification service unavailable: Response status code does not indicate success: 502 (Bad Gateway)." },
            AiUsed: false,
            AiProvider: null,
            AiModel: null,
            ReasoningSummary: null,
            FallbackUsed: true
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-16",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        storage.SetFileBytes("/uploads/dummy_doctor_referral.pdf", GenuineDoctorReferralPdfBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "dummy_doctor_referral.pdf", FileUrl = "/uploads/dummy_doctor_referral.pdf", FileSize = GenuineDoctorReferralPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.True(verifResult.Complete);
        Assert.True(verifResult.FallbackUsed);
        Assert.False(verifResult.AiUsed);
        Assert.Empty(verifResult.MissingItems);
        Assert.Contains(verifResult.Warnings, w => w.Contains("502 (Bad Gateway)"));
    }

    // ── Test 17: All four valid Health documents, AI returns Complete = false -> deterministic Complete = true ──
    [Fact]
    public async Task Regression_Health17_AllFourValidHealthDocs_AiReturnsCompleteFalse_DeterministicSuccessPrevails()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: false,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "AI mistakenly evaluated claim as incomplete.",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-17",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        storage.SetFileBytes("/uploads/dummy_doctor_referral.pdf", GenuineDoctorReferralPdfBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "dummy_doctor_referral.pdf", FileUrl = "/uploads/dummy_doctor_referral.pdf", FileSize = GenuineDoctorReferralPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        // AI returning Complete=false cannot veto deterministic success
        Assert.True(verifResult.Complete);
        Assert.Empty(verifResult.MissingItems);
        Assert.True(verifResult.AiUsed);
        Assert.False(verifResult.FallbackUsed);
    }

    // ── Test 18: Architecture PDF tagged Doctor Referral, AI returns Complete = true -> Complete = false ──
    [Fact]
    public async Task Regression_Health18_ArchitecturePdfTaggedDoctorReferral_AiSaysCompleteTrue_DeterministicFailurePrevails()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "AI hallucinated that architecture slides are a valid doctor referral.",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-18",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/architecture.pdf", ArchitecturePdfBytes);
        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "06 - Architecture Patterns.pdf", FileUrl = "/uploads/architecture.pdf", FileSize = ArchitecturePdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        // AI returning Complete=true cannot override deterministic mismatch failure
        Assert.False(verifResult.Complete);
        Assert.Contains("Doctor Referral", verifResult.MissingItems);
        Assert.Contains(verifResult.Inconsistencies, i => i.Field == "Doctor Referral");
    }

    // ── Test 19: One required Health doc genuinely missing, AI returns Complete = true -> Complete = false ──
    [Fact]
    public async Task Regression_Health19_OneRequiredHealthDocGenuinelyMissing_AiSaysCompleteTrue_CompleteIsFalse()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();

        var aiClient = new ConfigurableAiClient(new DocumentVerificationResultDto(
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "Gemini",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "AI erroneously marked incomplete document checklist as complete.",
            FallbackUsed: false
        ));

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-19",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        // Doctor Referral is genuinely missing (not uploaded at all)

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        // AI returning Complete=true cannot override genuinely missing required document
        Assert.False(verifResult.Complete);
        Assert.Contains("Doctor Referral", verifResult.MissingItems);
    }

    // ── Test 20: All four valid Health documents, AI service throws -> Complete = true, FallbackUsed = true ──
    [Fact]
    public async Task Regression_Health20_AllFourValidHealthDocs_AiThrowsException_FallbackUsedAndCompleteIsTrue()
    {
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FailingAiClient();

        var service = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var claimId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = claimId,
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-HLT-REG-20",
            ClaimType = ClaimType.Health,
            Status = ClaimStatus.Submitted,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            ClaimedAmount = 50000m
        };

        storage.SetFileBytes("/uploads/med_report.pdf", MedicalReportPdfBytes);
        storage.SetFileBytes("/uploads/hosp_bill.pdf", HospitalBillPdfBytes);
        storage.SetFileBytes("/uploads/prescription.pdf", PrescriptionPdfBytes);
        storage.SetFileBytes("/uploads/dummy_doctor_referral.pdf", GenuineDoctorReferralPdfBytes);

        claim.Documents = new List<ClaimDocument>
        {
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Medical Report", FileName = "medical_report.pdf", FileUrl = "/uploads/med_report.pdf", FileSize = MedicalReportPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Hospital Bills", FileName = "hospital_bill.pdf", FileUrl = "/uploads/hosp_bill.pdf", FileSize = HospitalBillPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Prescription", FileName = "prescription.pdf", FileUrl = "/uploads/prescription.pdf", FileSize = PrescriptionPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending },
            new() { Id = Guid.NewGuid(), ClaimId = claimId, DocumentType = "Doctor Referral", FileName = "dummy_doctor_referral.pdf", FileUrl = "/uploads/dummy_doctor_referral.pdf", FileSize = GenuineDoctorReferralPdfBytes.Length, VerificationStatus = DocumentVerificationStatus.Pending }
        };
        await claimRepo.AddAsync(claim);

        var verifResult = await service.VerifyDocumentsAsync(claimId, OwnerId, Role.Policyholder);

        Assert.True(verifResult.Complete);
        Assert.True(verifResult.FallbackUsed);
        Assert.False(verifResult.AiUsed);
        Assert.Empty(verifResult.MissingItems);
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
