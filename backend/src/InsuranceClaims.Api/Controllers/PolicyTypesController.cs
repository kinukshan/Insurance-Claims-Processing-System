using Microsoft.AspNetCore.Mvc;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Policy type reference data — read-only, public.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PolicyTypesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public PolicyTypesController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /api/policytypes — Get all active policy types.
    /// Public — no authentication required (reference data).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rawTypes = await _dbContext.PolicyTypes
            .Where(pt => pt.IsActive)
            .OrderBy(pt => pt.Name)
            .ToListAsync();

        var policyTypes = rawTypes.Select(pt => new
        {
            pt.Id,
            pt.Name,
            pt.Description,
            pt.DefaultCoverageLimit,
            DefaultDeductible = PolicyClaimCompatibility.GetFixedDeductible(pt.Id)
                ?? PolicyClaimCompatibility.GetFixedDeductible(pt.Name)
                ?? pt.DefaultDeductible,
            InsuranceClass = (int)pt.InsuranceClass,
            InsuranceClassCode = pt.InsuranceClass.ToString(),
            InsuranceClassName = pt.InsuranceClass == InsuranceClaims.Domain.PolicyManagement.Enums.InsuranceClass.LongTerm
                ? "Long-Term Insurance"
                : "General Insurance"
        });

        return Ok(policyTypes);
    }
}
