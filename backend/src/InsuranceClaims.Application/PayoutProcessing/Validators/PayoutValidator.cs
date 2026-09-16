using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.Validators;

/// <summary>
/// Server-side validation for payout operations.
/// </summary>
public static class PayoutValidator
{
    /// <summary>
    /// Validate that all financial amounts are non-negative.
    /// </summary>
    public static List<string> ValidateAmounts(
        decimal approvedClaimAmount, decimal coverageLimit, decimal deductible)
    {
        var errors = new List<string>();

        if (approvedClaimAmount < 0)
            errors.Add("Approved claim amount must be non-negative.");
        if (coverageLimit < 0)
            errors.Add("Coverage limit must be non-negative.");
        if (deductible < 0)
            errors.Add("Deductible must be non-negative.");

        return errors;
    }

    /// <summary>
    /// Validate that a status transition is legal per the payout state machine.
    /// </summary>
    public static bool ValidateStatusTransition(PayoutStatus from, PayoutStatus to)
    {
        return Payout.IsValidTransition(from, to);
    }

    /// <summary>
    /// Validate that a payout has been approved before execution.
    /// </summary>
    public static bool ValidateApprovalBeforeExecution(Payout payout)
    {
        return payout.Status == PayoutStatus.Approved;
    }

    /// <summary>
    /// Validate that a payout can be deleted (only draft/revision-requested).
    /// </summary>
    public static bool ValidateDeletion(Payout payout)
    {
        return payout.CanBeDeleted();
    }
}
