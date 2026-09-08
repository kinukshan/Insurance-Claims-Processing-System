using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Policy management endpoints — Component A (Member 1).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    // TODO: Inject policy management service

    // TODO: Implement endpoints:
    // GET    /api/policies
    // GET    /api/policies/{id}
    // POST   /api/policies
    // PUT    /api/policies/{id}
    // POST   /api/policies/{id}/calculate-premium
    // POST   /api/policies/{id}/renew
}
