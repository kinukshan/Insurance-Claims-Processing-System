using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Risk assessment and fraud flagging endpoints — Component C (Member 3).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RiskAssessmentsController : ControllerBase
{
    // TODO: Inject risk assessment service

    // TODO: Implement endpoints:
    // POST   /api/riskassessments/{claimId}/assess
    // GET    /api/riskassessments/{claimId}
    // GET    /api/riskassessments/flagged
    // POST   /api/riskassessments/{id}/escalate
    // GET    /api/riskassessments/fraud-history
}
