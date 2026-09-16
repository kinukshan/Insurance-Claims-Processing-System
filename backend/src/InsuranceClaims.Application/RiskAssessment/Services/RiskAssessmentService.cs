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

    // ── Risk thresholds ──────────────────────────────────────────
    private const decimal LowThreshold = 30m;
    private const decimal MediumThreshold = 60m;
    private const decimal HighThreshold = 80m;
    private const decimal AutoEscalateThreshold = 85m;
    private const decimal HighAmountThreshold = 50000m;

    public RiskAssessmentService(
        IRiskAssessmentRepository repository,
        IAiRiskClient aiClient)
    {
        _repository = repository;
        _aiClient = aiClient;
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

        // 3. Optionally call AI agent
        decimal aiScore = 0m;
        var aiFlags = new List<FraudFlag>();

        if (request.IncludeAiAnalysis)
        {
            var aiResult = await _aiClient.AnalyzeClaimAsync(new AiRiskRequest
            {
                ClaimId = claimId,
                PolicyHolderId = claim.PolicyHolderId,
                ClaimAmount = claim.ClaimedAmount,
                Description = claim.Description,
                IncidentDate = claim.IncidentDate,
                IncidentLocation = claim.IncidentLocation
            });

            if (aiResult != null)
            {
                aiScore = Math.Clamp(aiResult.RiskScore, 0m, 100m);

                foreach (var flag in aiResult.Flags)
                {
                    var flagType = ParseFlagType(flag.FlagType);
                    var severity = ParseSeverity(flag.Severity);

                    aiFlags.Add(CreateFlag(claimId, flagType, flag.Description, severity, FlagSource.AI));
                }
            }
        }

        // 4. Merge scores: weighted average (rules 60%, AI 40%) if AI was used
        decimal finalScore;
        if (request.IncludeAiAnalysis && aiScore > 0)
        {
            finalScore = Math.Clamp(ruleScore * 0.6m + aiScore * 0.4m, 0m, 100m);
        }
        else
        {
            finalScore = Math.Clamp(ruleScore, 0m, 100m);
        }

        var riskLevel = ClassifyRiskLevel(finalScore);
        var recommendation = finalScore >= AutoEscalateThreshold
            ? RiskRecommendation.Escalate
            : RiskRecommendation.Proceed;

        // 5. Create the assessment entity
        var allFlags = ruleFlags.Concat(aiFlags).ToList();

        var assessment = new Domain.RiskAssessment.RiskAssessment
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            RiskScore = finalScore,
            RiskLevel = riskLevel,
            Recommendation = recommendation,
            AssessorType = request.IncludeAiAnalysis ? AssessorType.AI : AssessorType.System,
            AssessmentTimestamp = DateTime.UtcNow,
            Summary = BuildSummary(finalScore, riskLevel, recommendation, allFlags.Count, request.Notes)
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

        return RiskAssessmentDto.FromEntity(assessment);
    }

    /// <inheritdoc />
    public async Task<RiskAssessmentDto?> GetAssessmentAsync(Guid claimId)
    {
        var assessment = await _repository.GetByClaimIdAsync(claimId);
        return assessment != null ? RiskAssessmentDto.FromEntity(assessment) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RiskAssessmentDto>> GetFlaggedClaimsAsync()
    {
        var flagged = await _repository.GetFlaggedAsync();
        return flagged.Select(RiskAssessmentDto.FromEntity).ToList();
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
        string? notes)
    {
        var summary = $"Risk Score: {score:N1}/100 | Level: {level} | Recommendation: {recommendation} | Flags: {flagCount}";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            summary += $" | Notes: {notes}";
        }
        return summary;
    }
}
