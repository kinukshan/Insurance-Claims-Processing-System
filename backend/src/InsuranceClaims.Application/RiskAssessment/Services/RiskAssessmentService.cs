using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.RiskAssessment.DTOs;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.Services;

/// <summary>
/// Core business logic for risk assessment and fraud detection.
/// Combines deterministic rule evaluation with AI agent analysis.
/// AI recommendation never automatically becomes a high-impact final decision.
/// </summary>
public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly IRiskAssessmentRepository _repository;
    private readonly IAiRiskClient _aiClient;
    private readonly IDocumentStorageService? _storageService;

    // ── Risk thresholds ──────────────────────────────────────────
    private const decimal LowThreshold = 30m;
    private const decimal MediumThreshold = 60m;
    private const decimal HighThreshold = 80m;
    private const decimal AutoEscalateThreshold = 85m;
    private const decimal HighAmountThreshold = 50000m;

    public RiskAssessmentService(
        IRiskAssessmentRepository repository,
        IAiRiskClient aiClient,
        IDocumentStorageService? storageService = null)
    {
        _repository = repository;
        _aiClient = aiClient;
        _storageService = storageService;
    }

    /// <inheritdoc />
    public async Task<RiskAssessmentDto> AssessClaimAsync(Guid claimId, AssessClaimRequest request)
    {
        // 1. Retrieve the claim
        var claim = await _repository.GetClaimByIdAsync(claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found.");

        // 2. Run deterministic rules
        var ruleFlags = new List<FraudFlag>();
        decimal ruleScore = 0m;

        // Rule: High claim amount
        if (claim.ClaimedAmount > HighAmountThreshold)
        {
            ruleFlags.Add(CreateFlag(claimId, FraudFlagType.HighAmount,
                $"Claim amount ${claim.ClaimedAmount:N2} exceeds threshold of ${HighAmountThreshold:N2}.",
                claim.ClaimedAmount > HighAmountThreshold * 2 ? FlagSeverity.Critical : FlagSeverity.High,
                FlagSource.Rule));
            ruleScore += 25m;
        }

        // Rule: Duplicate claim detection
        var duplicates = await _repository.HasDuplicateClaimAsync(
            claimId, claim.PolicyHolderId, claim.Description, claim.IncidentDate);

        if (duplicates)
        {
            ruleFlags.Add(CreateFlag(claimId, FraudFlagType.DuplicateClaim,
                "Potential duplicate: another claim with identical description and incident date found for this policyholder.",
                FlagSeverity.Critical,
                FlagSource.Rule));
            ruleScore += 40m;
        }

        // Rule: Frequent claims — check claims in last 12 months
        var recentClaimCount = await _repository.GetRecentClaimCountAsync(claim.PolicyHolderId, 12);

        if (recentClaimCount > 3)
        {
            ruleFlags.Add(CreateFlag(claimId, FraudFlagType.FrequentClaims,
                $"Policyholder has submitted {recentClaimCount} claims in the last 12 months.",
                recentClaimCount > 5 ? FlagSeverity.High : FlagSeverity.Medium,
                FlagSource.Rule));
            ruleScore += 15m;
        }

        // Rule: Historical pattern — check for past fraud cases
        var pastFraudCases = await _repository.GetFraudCasesByPolicyholderAsync(claim.PolicyHolderId);
        if (pastFraudCases.Count > 0)
        {
            ruleFlags.Add(CreateFlag(claimId, FraudFlagType.SuspiciousPattern,
                $"Policyholder has {pastFraudCases.Count} previous fraud case(s) on record.",
                FlagSeverity.High,
                FlagSource.Rule));
            ruleScore += 20m;
        }

        // Rule: Document integrity and consistency validation
        if (claim.Documents != null && claim.Documents.Count > 0)
        {
            var fileBytesMap = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
            if (_storageService != null)
            {
                foreach (var doc in claim.Documents)
                {
                    if (!string.IsNullOrWhiteSpace(doc.FileUrl))
                    {
                        try
                        {
                            fileBytesMap[doc.FileUrl] = await _storageService.GetFileBytesAsync(doc.FileUrl);
                        }
                        catch
                        {
                            fileBytesMap[doc.FileUrl] = null;
                        }
                    }
                }
            }

            var docEval = DocumentIntegrityValidator.EvaluateClaimDocuments(
                claim.ClaimType.ToString(),
                claim.Documents,
                url => fileBytesMap.TryGetValue(url, out var b) ? b : null
            );

            foreach (var finding in docEval.Findings)
            {
                ruleFlags.Add(CreateFlag(
                    claimId,
                    finding.FlagType,
                    finding.Description,
                    finding.Severity,
                    FlagSource.Rule
                ));

                switch (finding.FlagType)
                {
                    case FraudFlagType.DocumentTypeMismatch:
                        ruleScore += 40m;
                        break;
                    case FraudFlagType.DuplicateDocumentReused:
                        ruleScore += 25m;
                        break;
                    case FraudFlagType.DocumentUnreadable:
                        ruleScore += 20m;
                        break;
                    case FraudFlagType.DocumentContentInconsistent:
                        ruleScore += 20m;
                        break;
                    case FraudFlagType.DocumentVerificationFailed:
                        ruleScore += 15m;
                        break;
                }
            }
        }

        // 3. Deterministic rules are authoritative for all risk decisions
        decimal finalScore = Math.Clamp(ruleScore, 0m, 100m);
        var riskLevel = ClassifyRiskLevel(finalScore);

        var hasCriticalIssues = finalScore >= MediumThreshold
            || ruleFlags.Any(f => f.FlagType is FraudFlagType.DocumentTypeMismatch
                or FraudFlagType.DuplicateDocumentReused
                or FraudFlagType.DocumentUnreadable);

        var recommendation = (finalScore >= AutoEscalateThreshold || hasCriticalIssues)
            ? RiskRecommendation.Escalate
            : RiskRecommendation.Proceed;

        // 4. Optionally call AI agent for explanatory reasoning only (does NOT modify score, flags, or recommendation)
        AiRiskResult? aiResult = null;
        if (request.IncludeAiAnalysis)
        {
            aiResult = await _aiClient.AnalyzeClaimAsync(new AiRiskRequest
            {
                ClaimId = claimId,
                PolicyHolderId = claim.PolicyHolderId,
                ClaimAmount = claim.ClaimedAmount,
                Description = claim.Description,
                IncidentDate = claim.IncidentDate,
                IncidentLocation = claim.IncidentLocation,
                ClaimType = claim.ClaimType.ToString(),
                DocumentFlags = ruleFlags
                    .Where(f => f.FlagType is FraudFlagType.DocumentTypeMismatch
                        or FraudFlagType.DocumentUnreadable
                        or FraudFlagType.DuplicateDocumentReused
                        or FraudFlagType.DocumentContentInconsistent
                        or FraudFlagType.DocumentVerificationFailed)
                    .Select(f => $"{f.FlagType}: {f.Description}")
                    .ToList()
            });
        }

        // 5. Create the assessment entity (using deterministic flags)
        var allFlags = ruleFlags;

        var assessment = new Domain.RiskAssessment.RiskAssessment
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            RiskScore = finalScore,
            RiskLevel = riskLevel,
            Recommendation = recommendation,
            AssessorType = request.IncludeAiAnalysis && aiResult != null ? AssessorType.AI : AssessorType.System,
            AssessmentTimestamp = DateTime.UtcNow,
            Summary = BuildSummary(finalScore, riskLevel, recommendation, allFlags.Count, request.Notes, aiResult?.ReasoningSummary)
        };

        // Link flags to the assessment
        foreach (var flag in allFlags)
        {
            flag.Id = Guid.NewGuid();
            flag.RiskAssessmentId = assessment.Id;
            assessment.FraudFlags.Add(flag);
        }

        await _repository.AddAsync(assessment);

        // 6. Auto-create fraud case if score is critically high
        if (finalScore >= AutoEscalateThreshold)
        {
            var fraudCase = new FraudCase
            {
                Id = Guid.NewGuid(),
                ClaimId = claimId,
                RiskAssessmentId = assessment.Id,
                PolicyHolderId = claim.PolicyHolderId,
                Status = FraudCaseStatus.Open,
                Priority = finalScore >= 90m ? FraudCasePriority.Urgent : FraudCasePriority.High,
                Notes = $"Auto-created: Risk score {finalScore:N1} exceeded escalation threshold."
            };

            await _repository.AddFraudCaseAsync(fraudCase);
            assessment.FraudCase = fraudCase;
        }

        await _repository.SaveChangesAsync();

        var dto = RiskAssessmentDto.FromEntity(assessment);
        dto.ClaimNumber = claim.ClaimNumber;
        if (aiResult != null)
        {
            dto.AiUsed = aiResult.AiUsed;
            dto.AiProvider = aiResult.AiProvider;
            dto.AiModel = aiResult.AiModel;
            dto.ReasoningSummary = aiResult.ReasoningSummary;
            dto.FallbackUsed = aiResult.FallbackUsed;
        }
        else if (request.IncludeAiAnalysis)
        {
            dto.FallbackUsed = true;
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<RiskAssessmentDto?> GetAssessmentAsync(Guid claimId)
    {
        var assessment = await _repository.GetByClaimIdAsync(claimId);
        if (assessment == null) return null;

        var dto = RiskAssessmentDto.FromEntity(assessment);
        var claim = await _repository.GetClaimByIdAsync(claimId);
        if (claim != null)
        {
            dto.ClaimNumber = claim.ClaimNumber;
        }
        return dto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RiskAssessmentDto>> GetFlaggedClaimsAsync()
    {
        var flagged = await _repository.GetFlaggedAsync();
        var dtos = new List<RiskAssessmentDto>(flagged.Count);
        foreach (var f in flagged)
        {
            var dto = RiskAssessmentDto.FromEntity(f);
            var claim = await _repository.GetClaimByIdAsync(f.ClaimId);
            if (claim != null)
            {
                dto.ClaimNumber = claim.ClaimNumber;
            }
            dtos.Add(dto);
        }
        return dtos;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RiskAssessmentDto>> GetAllAssessmentsAsync()
    {
        var assessments = await _repository.GetAllAsync();
        var dtos = new List<RiskAssessmentDto>(assessments.Count);
        foreach (var a in assessments)
        {
            var dto = RiskAssessmentDto.FromEntity(a);
            var claim = await _repository.GetClaimByIdAsync(a.ClaimId);
            if (claim != null)
            {
                dto.ClaimNumber = claim.ClaimNumber;
            }
            dtos.Add(dto);
        }
        return dtos;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudCaseDto>> GetHistoryAsync(Guid policyholderId)
    {
        var cases = await _repository.GetFraudCasesByPolicyholderAsync(policyholderId);
        return cases.Select(FraudCaseDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<FraudCaseDto> EscalateAsync(Guid assessmentId, EscalateRequest request)
    {
        var assessment = await _repository.GetByIdAsync(assessmentId)
            ?? throw new KeyNotFoundException($"Risk assessment {assessmentId} not found.");

        // Check if a fraud case already exists
        var existingCase = await _repository.GetFraudCaseByAssessmentIdAsync(assessmentId);
        if (existingCase != null)
        {
            throw new InvalidOperationException(
                $"A fraud case already exists for assessment {assessmentId}.");
        }

        // Retrieve the claim for PolicyHolderId
        var claim = await _repository.GetClaimByIdAsync(assessment.ClaimId)
            ?? throw new KeyNotFoundException($"Claim {assessment.ClaimId} not found.");

        var fraudCase = new FraudCase
        {
            Id = Guid.NewGuid(),
            ClaimId = assessment.ClaimId,
            RiskAssessmentId = assessmentId,
            PolicyHolderId = claim.PolicyHolderId,
            Status = FraudCaseStatus.Open,
            Priority = request.Priority,
            AssignedReviewer = request.AssignedReviewer,
            Notes = request.Reason
        };

        await _repository.AddFraudCaseAsync(fraudCase);
        await _repository.SaveChangesAsync();

        return FraudCaseDto.FromEntity(fraudCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudFlagDto>> GetFlagsAsync(Guid claimId)
    {
        var flags = await _repository.GetFlagsByClaimIdAsync(claimId);
        return flags.Select(FraudFlagDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<FraudCaseDto> UpdateFraudCaseAsync(Guid fraudCaseId, UpdateFraudCaseRequest request)
    {
        var fraudCase = await _repository.GetFraudCaseByIdAsync(fraudCaseId)
            ?? throw new KeyNotFoundException($"Fraud case {fraudCaseId} not found.");

        if (request.Status.HasValue)
        {
            fraudCase.Status = request.Status.Value;

            // Set ClosedAt when resolving or dismissing
            if (request.Status.Value is FraudCaseStatus.Resolved or FraudCaseStatus.Dismissed)
            {
                fraudCase.ClosedAt = DateTime.UtcNow;
            }
        }

        if (request.Notes != null)
            fraudCase.Notes = request.Notes;

        if (request.Resolution != null)
            fraudCase.Resolution = request.Resolution;

        if (request.AssignedReviewer != null)
            fraudCase.AssignedReviewer = request.AssignedReviewer;

        if (request.Priority.HasValue)
            fraudCase.Priority = request.Priority.Value;

        await _repository.UpdateFraudCaseAsync(fraudCase);
        await _repository.SaveChangesAsync();

        return FraudCaseDto.FromEntity(fraudCase);
    }

    /// <inheritdoc />
    public async Task<PolicyholderReviewStatusDto> GetPolicyholderStatusAsync(Guid claimId)
    {
        var assessment = await _repository.GetByClaimIdAsync(claimId);

        if (assessment == null)
        {
            return new PolicyholderReviewStatusDto
            {
                ClaimId = claimId,
                ReviewStatus = "Additional Review Required",
                LastUpdated = null
            };
        }

        var fraudCase = await _repository.GetFraudCaseByAssessmentIdAsync(assessment.Id);

        string status;
        if (fraudCase != null && fraudCase.Status is FraudCaseStatus.Resolved or FraudCaseStatus.Dismissed)
        {
            status = "Review Completed";
        }
        else if (assessment.Recommendation == RiskRecommendation.Escalate || fraudCase != null)
        {
            status = "Under Manual Review";
        }
        else
        {
            status = "Review Completed";
        }

        return new PolicyholderReviewStatusDto
        {
            ClaimId = claimId,
            ReviewStatus = status,
            LastUpdated = assessment.UpdatedAt
        };
    }

    // ── Private helpers ──────────────────────────────────────────

    private static RiskLevel ClassifyRiskLevel(decimal score) => score switch
    {
        < 30m => RiskLevel.Low,
        < 60m => RiskLevel.Medium,
        < 80m => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    private static FraudFlag CreateFlag(
        Guid claimId,
        FraudFlagType flagType,
        string description,
        FlagSeverity severity,
        FlagSource source)
    {
        return new FraudFlag
        {
            ClaimId = claimId,
            FlagType = flagType,
            Description = description,
            Severity = severity,
            Source = source,
            IsResolved = false
        };
    }

    private static FraudFlagType ParseFlagType(string flagType)
    {
        return Enum.TryParse<FraudFlagType>(flagType, ignoreCase: true, out var result)
            ? result
            : FraudFlagType.SuspiciousPattern;
    }

    private static FlagSeverity ParseSeverity(string severity)
    {
        return Enum.TryParse<FlagSeverity>(severity, ignoreCase: true, out var result)
            ? result
            : FlagSeverity.Medium;
    }

    private static string BuildSummary(
        decimal score,
        RiskLevel level,
        RiskRecommendation recommendation,
        int flagCount,
        string? notes,
        string? reasoningSummary = null)
    {
        var summary = $"Risk Score: {score:N1}/100 | Level: {level} | Recommendation: {recommendation} | Flags: {flagCount}";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            summary += $" | Notes: {notes}";
        }
        if (!string.IsNullOrWhiteSpace(reasoningSummary))
        {
            summary += $" | AI Context: {reasoningSummary}";
        }
        return summary;
    }
}
