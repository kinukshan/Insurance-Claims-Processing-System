using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Claims submission and document verification endpoints — Component B (Member 2).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    // TODO: Inject claims management service

    // TODO: Implement endpoints:
    // POST   /api/claims
    // GET    /api/claims
    // GET    /api/claims/{id}
    // POST   /api/claims/{id}/documents
    // GET    /api/claims/{id}/status
    // POST   /api/claims/{id}/validate-coverage
}
