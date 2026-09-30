using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace InsuranceClaims.IntegrationTests.Database;

public class CheckDatabaseTests
{
    private readonly ITestOutputHelper _output;

    public CheckDatabaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InspectRiskAssessmentsInDatabase()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<InsuranceClaims.Api.Controllers.AuthController>()
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(conn))
        {
            _output.WriteLine("No connection string found.");
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(conn)
            .Options;

        using var db = new ApplicationDbContext(options);
        var assessments = await db.RiskAssessments
            .Include(r => r.FraudFlags)
            .Include(r => r.FraudCase)
            .ToListAsync();

        _output.WriteLine($"Found {assessments.Count} assessments in DB.");
        foreach (var a in assessments)
        {
            _output.WriteLine($"Id: {a.Id}, ClaimId: {a.ClaimId}, Score: {a.RiskScore}, Level: {a.RiskLevel}, Rec: {a.Recommendation}, Flags: {a.FraudFlags.Count}, FraudCase: {a.FraudCase != null}");
        }

        var repo = new InsuranceClaims.Infrastructure.Repositories.RiskAssessmentRepository(db);
        var mockAi = new InsuranceClaims.Infrastructure.ExternalServices.AiRiskClient(new HttpClient { BaseAddress = new Uri("http://localhost:8000") }, Microsoft.Extensions.Logging.Abstractions.NullLogger<InsuranceClaims.Infrastructure.ExternalServices.AiRiskClient>.Instance);
        var service = new InsuranceClaims.Application.RiskAssessment.Services.RiskAssessmentService(repo, mockAi);

        try
        {
            var all = await service.GetAllAssessmentsAsync();
            _output.WriteLine($"GetAllAssessmentsAsync returned {all.Count} items.");
            var jsonAll = System.Text.Json.JsonSerializer.Serialize(all);
            _output.WriteLine($"Serialized all assessments: {jsonAll.Length} chars.");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"GetAllAssessmentsAsync FAILED: {ex}");
        }
        try
        {
            var flagged = await service.GetFlaggedClaimsAsync();
            _output.WriteLine($"GetFlaggedClaimsAsync returned {flagged.Count} items.");
            var jsonFlagged = System.Text.Json.JsonSerializer.Serialize(flagged);
            _output.WriteLine($"Serialized flagged claims: {jsonFlagged.Length} chars.");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"GetFlaggedClaimsAsync FAILED: {ex}");
        }

        try
        {
            var logs = await db.NotificationLogs.ToListAsync();
            _output.WriteLine($"Found {logs.Count} NotificationLogs in DB.");
            foreach (var log in logs)
            {
                _output.WriteLine($"Log: {log.NotificationKey}, Type: {log.NotificationType}, Status: {log.Status}, Recipient: {log.Recipient}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Querying NotificationLogs FAILED: {ex}");
        }
    }
}
