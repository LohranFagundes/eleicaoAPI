
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Models;
using ElectionApi.Net.Services;

namespace ElectionApi.Net.Data;

public class ElectionDbContext : DbContext
{
    private readonly IDateTimeService? _dateTimeService;
    private readonly TimeZoneInfo _brazilTimeZone;

    public ElectionDbContext(DbContextOptions<ElectionDbContext> options) : base(options)
    {
        _brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }

    public ElectionDbContext(DbContextOptions<ElectionDbContext> options, IDateTimeService dateTimeService) : base(options)
    {
        _dateTimeService = dateTimeService;
        _brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }

    public DbSet<Admin> Admins { get; set; }
    public DbSet<Voter> Voters { get; set; }
    public DbSet<Election> Elections { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<VoteReceipt> VoteReceipts { get; set; }
    public DbSet<SystemSeal> SystemSeals { get; set; }
    public DbSet<ZeroReport> ZeroReports { get; set; }
    public DbSet<SecureVote> SecureVotes { get; set; }
    public DbSet<Company> Companies { get; set; }

    public override int SaveChanges()
    {
        UpdateTimestampsToBrazilTime();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestampsToBrazilTime();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestampsToBrazilTime()
    {
        var brazilNow = _dateTimeService?.Now ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _brazilTimeZone);

        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity != null &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            // Set CreatedAt for new entities
            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
                if (createdAtProperty != null)
                {
                    createdAtProperty.CurrentValue = brazilNow;
                }

                var timestampProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Timestamp");
                if (timestampProperty != null)
                {
                    timestampProperty.CurrentValue = brazilNow;
                }
            }

            // Set UpdatedAt for new and modified entities
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                var updatedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProperty != null)
                {
                    updatedAtProperty.CurrentValue = brazilNow;
                }

                var lastLoginAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "LastLoginAt");
                if (lastLoginAtProperty != null && entry.State == EntityState.Modified)
                {
                    lastLoginAtProperty.CurrentValue = brazilNow;
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure all DateTime properties to handle Brazil timezone
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    // Configure DateTime columns to use local timezone representation
                    property.SetColumnType("datetime(6)");
                }
                // Configure string properties to use UTF-8 encoding
                else if (property.ClrType == typeof(string))
                {
                    property.SetCollation("utf8mb4_unicode_ci");
                }
            }
        }

        // Admin configuration
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Permissions).HasColumnType("json");
            entity.Property(e => e.Name).HasColumnType("varchar(255)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Email).HasColumnType("varchar(255)").UseCollation("utf8mb4_unicode_ci");
        });

        // Voter configuration
        modelBuilder.Entity<Voter>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.VoteWeight).HasPrecision(10, 2);
        });

        // Election configuration
        modelBuilder.Entity<Election>(entity =>
        {
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.Title).HasColumnType("varchar(500)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Description).HasColumnType("text").UseCollation("utf8mb4_unicode_ci");
            
            // Company relationship
            entity.HasOne(e => e.Company)
                .WithMany(c => c.Elections)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Position configuration
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasOne(p => p.Election)
                .WithMany(e => e.Positions)
                .HasForeignKey(p => p.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Candidate configuration
        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasOne(c => c.Position)
                .WithMany(p => p.Candidates)
                .HasForeignKey(c => c.PositionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Vote configuration
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasOne(v => v.Voter)
                .WithMany(vo => vo.Votes)
                .HasForeignKey(v => v.VoterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Election)
                .WithMany(e => e.Votes)
                .HasForeignKey(v => v.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.Position)
                .WithMany()
                .HasForeignKey(v => v.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Candidate)
                .WithMany(c => c.Votes)
                .HasForeignKey(v => v.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(v => v.VoteWeight).HasPrecision(10, 2);
            entity.HasIndex(v => new { v.VoterId, v.ElectionId, v.PositionId }).IsUnique();
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.LoggedAt).HasColumnType("datetime");
            entity.HasIndex(e => new { e.UserId, e.EntityType, e.Action });
            entity.HasIndex(e => e.LoggedAt);
        });

        // VoteReceipt configuration
        modelBuilder.Entity<VoteReceipt>(entity =>
        {
            entity.HasOne(vr => vr.Voter)
                .WithMany()
                .HasForeignKey(vr => vr.VoterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(vr => vr.Election)
                .WithMany()
                .HasForeignKey(vr => vr.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(vr => vr.ReceiptToken).IsUnique();
            entity.HasIndex(vr => new { vr.VoterId, vr.ElectionId }).IsUnique();
        });

        // SystemSeal configuration
        modelBuilder.Entity<SystemSeal>(entity =>
        {
            entity.HasOne(ss => ss.Election)
                .WithMany()
                .HasForeignKey(ss => ss.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ss => ss.Admin)
                .WithMany()
                .HasForeignKey(ss => ss.SealedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ss => new { ss.ElectionId, ss.SealType });
        });

        // ZeroReport configuration
        modelBuilder.Entity<ZeroReport>(entity =>
        {
            entity.HasOne(zr => zr.Election)
                .WithMany()
                .HasForeignKey(zr => zr.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(zr => zr.Admin)
                .WithMany()
                .HasForeignKey(zr => zr.GeneratedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(zr => zr.ElectionId).IsUnique();
        });

        // SecureVote configuration
        modelBuilder.Entity<SecureVote>(entity =>
        {
            entity.HasOne(sv => sv.Voter)
                .WithMany()
                .HasForeignKey(sv => sv.VoterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sv => sv.Election)
                .WithMany()
                .HasForeignKey(sv => sv.ElectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sv => sv.Position)
                .WithMany()
                .HasForeignKey(sv => sv.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(sv => sv.VoteId).IsUnique();
            entity.HasIndex(sv => new { sv.VoterId, sv.ElectionId }).IsUnique();
            entity.HasIndex(sv => sv.VotedAt);
            entity.HasIndex(sv => sv.ElectionId);
            entity.HasIndex(sv => sv.PositionId);
        });

        // Company configuration
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(c => c.Cnpj).IsUnique();
            entity.Property(c => c.NomeFantasia).HasColumnType("varchar(200)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.RazaoSocial).HasColumnType("varchar(200)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.Cnpj).HasColumnType("varchar(18)");
            entity.Property(c => c.Cep).HasColumnType("varchar(10)");
            entity.Property(c => c.Bairro).HasColumnType("varchar(100)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.Logradouro).HasColumnType("varchar(200)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.Numero).HasColumnType("varchar(10)");
            entity.Property(c => c.Cidade).HasColumnType("varchar(100)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.Pais).HasColumnType("varchar(100)").UseCollation("utf8mb4_unicode_ci");
            entity.Property(c => c.LogoUrl).HasColumnType("varchar(500)");
        });

        // Configure table names to match PHP conventions
        modelBuilder.Entity<Admin>().ToTable("admins");
        modelBuilder.Entity<Voter>().ToTable("voters");
        modelBuilder.Entity<Election>().ToTable("elections");
        modelBuilder.Entity<Position>().ToTable("positions");
        modelBuilder.Entity<Candidate>().ToTable("candidates");
        modelBuilder.Entity<Vote>().ToTable("votes");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        modelBuilder.Entity<VoteReceipt>().ToTable("vote_receipts");
        modelBuilder.Entity<SystemSeal>().ToTable("system_seals");
        modelBuilder.Entity<ZeroReport>().ToTable("zero_reports");
        modelBuilder.Entity<SecureVote>().ToTable("secure_votes");
        modelBuilder.Entity<Company>().ToTable("companies");

        // Configure column names to snake_case
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        return string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower();
    }
}
