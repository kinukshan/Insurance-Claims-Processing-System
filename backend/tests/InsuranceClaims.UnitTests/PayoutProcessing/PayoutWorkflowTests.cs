using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Tests for payout workflow state machine and approval gates.
/// </summary>
public class PayoutWorkflowTests
{
    // ── State transition tests ───────────────────────────────────────

    [Theory]
    [InlineData(PayoutStatus.Draft, PayoutStatus.PendingApproval, true)]
    [InlineData(PayoutStatus.PendingApproval, PayoutStatus.Approved, true)]
    [InlineData(PayoutStatus.PendingApproval, PayoutStatus.Rejected, true)]
    [InlineData(PayoutStatus.PendingApproval, PayoutStatus.RevisionRequested, true)]
    [InlineData(PayoutStatus.RevisionRequested, PayoutStatus.Draft, true)]
    [InlineData(PayoutStatus.Approved, PayoutStatus.Processing, true)]
    [InlineData(PayoutStatus.Processing, PayoutStatus.Paid, true)]
    [InlineData(PayoutStatus.Processing, PayoutStatus.Failed, true)]
    public void IsValidTransition_ValidTransitions_ReturnsTrue(
        PayoutStatus from, PayoutStatus to, bool expected)
    {
        Assert.Equal(expected, Payout.IsValidTransition(from, to));
    }

    [Theory]
    [InlineData(PayoutStatus.Draft, PayoutStatus.Approved)]
    [InlineData(PayoutStatus.Draft, PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Draft, PayoutStatus.Paid)]
    [InlineData(PayoutStatus.Draft, PayoutStatus.Failed)]
    [InlineData(PayoutStatus.PendingApproval, PayoutStatus.Processing)]
    [InlineData(PayoutStatus.PendingApproval, PayoutStatus.Paid)]
    [InlineData(PayoutStatus.Approved, PayoutStatus.Rejected)]
    [InlineData(PayoutStatus.Approved, PayoutStatus.Draft)]
    [InlineData(PayoutStatus.Rejected, PayoutStatus.Approved)]
    [InlineData(PayoutStatus.Rejected, PayoutStatus.PendingApproval)]
    [InlineData(PayoutStatus.Paid, PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Paid, PayoutStatus.Draft)]
    [InlineData(PayoutStatus.Failed, PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Failed, PayoutStatus.Draft)]
    public void IsValidTransition_InvalidTransitions_ReturnsFalse(
        PayoutStatus from, PayoutStatus to)
    {
        Assert.False(Payout.IsValidTransition(from, to));
    }

    // ── Submit for approval ──────────────────────────────────────────

    [Fact]
    public void SubmitForApproval_FromDraft_Succeeds()
    {
        var payout = new Payout { Status = PayoutStatus.Draft };

        payout.SubmitForApproval();

        Assert.Equal(PayoutStatus.PendingApproval, payout.Status);
    }

    [Theory]
    [InlineData(PayoutStatus.PendingApproval)]
    [InlineData(PayoutStatus.Approved)]
    [InlineData(PayoutStatus.Rejected)]
    [InlineData(PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Paid)]
    [InlineData(PayoutStatus.Failed)]
    public void SubmitForApproval_FromNonDraft_ThrowsException(PayoutStatus status)
    {
        var payout = new Payout { Status = status };

        Assert.Throws<InvalidOperationException>(() => payout.SubmitForApproval());
    }

    // ── Approval required before execution ───────────────────────────

    [Fact]
    public void ExecuteBeforeApproval_IsInvalidTransition()
    {
        // PendingApproval → Processing is NOT valid
        Assert.False(Payout.IsValidTransition(PayoutStatus.PendingApproval, PayoutStatus.Processing));
        // Draft → Processing is NOT valid
        Assert.False(Payout.IsValidTransition(PayoutStatus.Draft, PayoutStatus.Processing));
    }

    [Fact]
    public void ExecuteAfterApproval_IsValidTransition()
    {
        // Approved → Processing IS valid
        Assert.True(Payout.IsValidTransition(PayoutStatus.Approved, PayoutStatus.Processing));
    }

    // ── Deletion guard ───────────────────────────────────────────────

    [Theory]
    [InlineData(PayoutStatus.Draft, true)]
    [InlineData(PayoutStatus.RevisionRequested, true)]
    [InlineData(PayoutStatus.PendingApproval, false)]
    [InlineData(PayoutStatus.Approved, false)]
    [InlineData(PayoutStatus.Rejected, false)]
    [InlineData(PayoutStatus.Processing, false)]
    [InlineData(PayoutStatus.Paid, false)]
    [InlineData(PayoutStatus.Failed, false)]
    public void CanBeDeleted_ReturnsCorrectResult(PayoutStatus status, bool expected)
    {
        var payout = new Payout { Status = status };

        Assert.Equal(expected, payout.CanBeDeleted());
    }

    // ── Rejected and Failed are terminal ─────────────────────────────

    [Fact]
    public void Rejected_IsTerminal_NoValidOutgoingTransitions()
    {
        var allStatuses = Enum.GetValues<PayoutStatus>();
        foreach (var target in allStatuses)
        {
            Assert.False(
                Payout.IsValidTransition(PayoutStatus.Rejected, target),
                $"Rejected → {target} should be invalid");
        }
    }

    [Fact]
    public void Paid_IsTerminal_NoValidOutgoingTransitions()
    {
        var allStatuses = Enum.GetValues<PayoutStatus>();
        foreach (var target in allStatuses)
        {
            Assert.False(
                Payout.IsValidTransition(PayoutStatus.Paid, target),
                $"Paid → {target} should be invalid");
        }
    }

    [Fact]
    public void Failed_IsTerminal_NoValidOutgoingTransitions()
    {
        var allStatuses = Enum.GetValues<PayoutStatus>();
        foreach (var target in allStatuses)
        {
            Assert.False(
                Payout.IsValidTransition(PayoutStatus.Failed, target),
                $"Failed → {target} should be invalid");
        }
    }
}
