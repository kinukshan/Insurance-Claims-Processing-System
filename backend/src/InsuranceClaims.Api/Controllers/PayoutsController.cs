using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Payout processing endpoints — Component D (Member 4).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PayoutsController : ControllerBase
{
    // TODO: Inject payout processing service

    // TODO: Implement endpoints:
    // POST   /api/payouts
    // GET    /api/payouts/{id}
    // POST   /api/payouts/{id}/calculate
    // POST   /api/payouts/{id}/submit-for-approval
    // POST   /api/payouts/{id}/approve
    // POST   /api/payouts/{id}/reject
    // POST   /api/payouts/{id}/request-revision
    // GET    /api/payouts/history
}
