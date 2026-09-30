using System.Text.Json;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using Xunit;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PayoutValidationResultDeserializationTests
{
    [Fact]
    public void Deserializes_PythonAiServiceSnakeCaseJson_Correctly()
    {
        // Python AI service response format:
        var json = """
        {
            "valid": true,
            "violations": ["Minor warning note"],
            "requires_human_approval": true,
            "ai_used": true,
            "ai_provider": "gemini",
            "ai_model": "gemini-1.5-flash",
            "reasoning_summary": "Claim amount is within policy limits after deductible.",
            "fallback_used": false,
            "summary": "Validation successful via AI model",
            "agent_id": "payout-validation-agent-v1",
            "timestamp": "2026-09-22T10:00:00Z"
        }
        """;

        var result = JsonSerializer.Deserialize<PayoutValidationResult>(json);

        Assert.NotNull(result);
        Assert.True(result.Valid);
        Assert.Single(result.Violations);
        Assert.Equal("Minor warning note", result.Violations[0]);
        Assert.True(result.RequiresHumanApproval);
        Assert.True(result.AiUsed);
        Assert.Equal("gemini", result.AiProvider);
        Assert.Equal("gemini-1.5-flash", result.AiModel);
        Assert.Equal("Claim amount is within policy limits after deductible.", result.ReasoningSummary);
        Assert.False(result.FallbackUsed);
        Assert.Equal("Validation successful via AI model", result.Summary);
        Assert.Equal("payout-validation-agent-v1", result.AgentId);
    }

    [Fact]
    public void Deserializes_FallbackResponse_Correctly()
    {
        var json = """
        {
            "valid": false,
            "violations": ["Payout exceeds policy coverage limit."],
            "requires_human_approval": true,
            "ai_used": false,
            "ai_provider": null,
            "ai_model": null,
            "reasoning_summary": null,
            "fallback_used": true,
            "summary": "Deterministic validation fallback",
            "agent_id": "payout-validation-rule-engine",
            "timestamp": "2026-09-22T10:05:00Z"
        }
        """;

        var result = JsonSerializer.Deserialize<PayoutValidationResult>(json);

        Assert.NotNull(result);
        Assert.False(result.Valid);
        Assert.Single(result.Violations);
        Assert.True(result.RequiresHumanApproval);
        Assert.False(result.AiUsed);
        Assert.Null(result.AiProvider);
        Assert.True(result.FallbackUsed);
    }
}
