using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Agent workflow endpoints — shared component.
/// ASP.NET Core initiates and monitors agentic AI workflows.
/// The AI service is INTERNAL ONLY — never called directly by React or Flutter.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentWorkflowsController : ControllerBase
{
    // TODO: Inject agent workflow service

    // TODO: Implement endpoints:
    // POST   /api/agentworkflows/claims/{claimId}/start
    // GET    /api/agentworkflows/{workflowId}
    // GET    /api/agentworkflows/{workflowId}/steps
    // GET    /api/agentworkflows/history
}
