using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence.Configurations;

namespace TmsApi.Infrastructure.Persistence;

public class TmsDbContext(DbContextOptions<TmsDbContext> options)
    : IdentityDbContext<TmsUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT:
        // Configure Identity's tables first.
        base.OnModelCreating(modelBuilder);

        // Apply only the student configuration here. Enabling assembly-wide scanning would also
        // change unrelated course and enrollment mappings in the existing database model.
        modelBuilder.ApplyConfiguration(new StudentConfiguration());

        modelBuilder
            .Entity<Enrollment>()
            .HasIndex(e => new { e.StudentId, e.CourseId })
            .IsUnique();
    }
}

// using Microsoft.EntityFrameworkCore;
// using TmsApi.Domain.Entities;

// // namespace TMSAPI.Data;
// namespace TmsApi.Infrastructure.Persistence;

// public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
// {
//     public DbSet<Student> Students => Set<Student>();
//     public DbSet<Course> Courses => Set<Course>();
//     public DbSet<Enrollment> Enrollments => Set<Enrollment>();
//     public DbSet<Assessment> Assessments => Set<Assessment>();

//     public DbSet<Certificate> certificates => Set<Certificate>();

//     // protected override void OnModelCreating(ModelBuilder modelBuilder)
//     // {
//     //     modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);
//     // }

//     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     {
//         modelBuilder
//             .Entity<Student>()
//             .Property<DateTime>("LastUpdated")
//             .HasColumnType("timestamp without time zone")
//             .HasDefaultValueSql("CURRENT_TIMESTAMP")
//             .IsConcurrencyToken();
//         base.OnModelCreating(modelBuilder);
//     }
// }
