using ElectionApi.Net.Models;
using ElectionApi.Net.Services;

namespace ElectionApi.Net.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ElectionDbContext context, IAuthService authService)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if we already have data
        if (context.Admins.Any())
        {
            return; // Database has been seeded
        }

        // Create master user (system developer)
        var masterAdmin = new Admin
        {
            Name = "Lohran Fagundes",
            Email = "lohran@funcorsan.com.br",
            Password = authService.HashPassword("Master@2024!Dev"),
            Role = "master",
            Permissions = "{\"elections\": [\"create\", \"read\", \"update\", \"delete\"], \"voters\": [\"create\", \"read\", \"update\", \"delete\"], \"admins\": [\"create\", \"read\", \"update\", \"delete\"], \"reports\": [\"read\"], \"system\": [\"full_access\"]}",
            IsActive = true,
            IsSuper = true,
            IsMaster = true
        };

        context.Admins.Add(masterAdmin);

        // Create default admin user
        var defaultAdmin = new Admin
        {
            Name = "Administrator",
            Email = "admin@election-system.com",
            Password = authService.HashPassword("admin123"),
            Role = "admin",
            Permissions = "{\"elections\": [\"create\", \"read\", \"update\", \"delete\"], \"voters\": [\"create\", \"read\", \"update\", \"delete\"], \"reports\": [\"read\"]}",
            IsActive = true,
            IsSuper = false,
            IsMaster = false
        };

        context.Admins.Add(defaultAdmin);

        // Create sample voter
        var sampleVoter = new Voter
        {
            Name = "João Silva",
            Email = "joao@example.com",
            Password = authService.HashPassword("voter123"),
            Cpf = "12345678901",
            BirthDate = new DateTime(1990, 1, 1),
            Phone = "(11) 99999-9999",
            VoteWeight = 1.0m,
            IsActive = true,
            IsVerified = true,
            EmailVerifiedAt = DateTime.UtcNow
        };

        context.Voters.Add(sampleVoter);

        // Create sample election
        var sampleElection = new Election
        {
            Title = "Eleição de Exemplo 2024",
            Description = "Esta é uma eleição de demonstração do sistema",
            ElectionType = "internal",
            IsSealed = false,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(8),
            Timezone = "America/Sao_Paulo",
            AllowBlankVotes = true,
            AllowNullVotes = false,
            RequireJustification = false,
            MaxVotesPerVoter = 1,
            VotingMethod = "single_choice",
            ResultsVisibility = "after_election",
            CreatedBy = 1, // Will be the master admin ID after save
            UpdatedBy = 1
        };

        context.Elections.Add(sampleElection);

        await context.SaveChangesAsync();

        // Update election with correct master admin ID
        sampleElection.CreatedBy = masterAdmin.Id;
        sampleElection.UpdatedBy = masterAdmin.Id;
        
        // Create sample position
        var samplePosition = new Position
        {
            Title = "Presidente",
            Description = "Cargo de presidente da organização",
            MaxCandidates = 10,
            MaxVotesPerVoter = 1,
            AllowBlankVotes = true,
            AllowNullVotes = false,
            OrderPosition = 1,
            IsActive = true,
            ElectionId = sampleElection.Id
        };

        context.Positions.Add(samplePosition);
        await context.SaveChangesAsync();

        // Create sample candidates
        var candidate1 = new Candidate
        {
            Name = "Maria Santos",
            Number = "10",
            Description = "Candidata com experiência em gestão",
            Biography = "Maria Santos tem 15 anos de experiência em liderança e gestão.",
            OrderPosition = 1,
            IsActive = true,
            PositionId = samplePosition.Id
        };

        var candidate2 = new Candidate
        {
            Name = "Carlos Oliveira",
            Number = "20",
            Description = "Candidato focado em inovação",
            Biography = "Carlos Oliveira é especialista em tecnologia e inovação.",
            OrderPosition = 2,
            IsActive = true,
            PositionId = samplePosition.Id
        };

        context.Candidates.AddRange(candidate1, candidate2);

        // Create audit log for seeding
        var auditLog = new AuditLog
        {
            UserId = null,
            UserType = "system",
            Action = "seed_database",
            EntityType = "system",
            Details = "Database seeded with initial data",
            IpAddress = "127.0.0.1",
            UserAgent = "DataSeeder",
            LoggedAt = DateTime.UtcNow
        };

        context.AuditLogs.Add(auditLog);

        await context.SaveChangesAsync();
    }
}