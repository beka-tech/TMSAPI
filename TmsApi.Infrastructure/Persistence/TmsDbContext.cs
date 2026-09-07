using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence.Configurations;

namespace TmsApi.Infrastructure.Persistence;

public class TmsDbContext(DbContextOptions<TmsDbContext> options, IAuditActor? actor = null)
    : IdentityDbContext<TmsUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TranscriptJob> TranscriptJobs => Set<TranscriptJob>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new EnrollmentConfiguration());
        modelBuilder.Entity<Enrollment>().HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();
        modelBuilder.Entity<Enrollment>().Property(e => e.Status).IsConcurrencyToken();
        modelBuilder.Entity<Enrollment>().Property(e => e.Grade).IsConcurrencyToken();
        modelBuilder.Entity<Course>().HasOne<TmsUser>().WithMany().HasForeignKey(c => c.InstructorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TmsUser>().HasOne<Student>().WithOne().HasForeignKey<TmsUser>(u => u.StudentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RefreshToken>().HasIndex(t => t.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(t => t.UserId);
        modelBuilder.Entity<RefreshToken>().Property(t => t.IsUsed).IsConcurrencyToken();
        modelBuilder.Entity<TranscriptJob>().HasKey(t => t.Id);
        modelBuilder.Entity<TranscriptJob>().HasIndex(t => new { t.RequestedBy, t.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<TranscriptJob>().HasIndex(t => new { t.State, t.RequestedAt });
        modelBuilder.Entity<AuditEntry>().HasIndex(a => a.OccurredAt);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries().Where(e =>
            e.Entity is Student or Course or Enrollment or Assessment or Certificate
            && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        var audits = entries.Select(e => (Entry: e, Audit: new AuditEntry
        {
            ActorId = actor?.ActorId ?? "system",
            EntityType = e.Metadata.ClrType.Name,
            Action = e.State.ToString(),
            Changes = JsonSerializer.Serialize(e.Properties.Where(p => !p.Metadata.IsPrimaryKey() &&
                (e.State != EntityState.Modified || p.IsModified)).ToDictionary(p => p.Metadata.Name,
                p => new { Before = e.State == EntityState.Added ? null : p.OriginalValue,
                    After = e.State == EntityState.Deleted ? null : p.CurrentValue }))
        })).ToList();
        foreach (var entry in entries.Where(e => e.Entity is Student && e.State == EntityState.Modified))
            entry.Property("LastUpdated").CurrentValue = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        if (audits.Count == 0) return await base.SaveChangesAsync(cancellationToken);
        var ownsTransaction = Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var count = await base.SaveChangesAsync(cancellationToken);
        foreach (var item in audits)
        {
            item.Audit.EntityId = string.Join(",", item.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
            AuditEntries.Add(item.Audit);
        }
        await base.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return count;
    }
}
