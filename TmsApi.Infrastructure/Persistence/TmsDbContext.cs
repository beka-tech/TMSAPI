using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;

// namespace TMSAPI.Data;
namespace TmsApi.Infrastructure.Persistence;

public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<Certificate> certificates => Set<Certificate>();

    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);
    // }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Student>()
            .Property<DateTime>("LastUpdated")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsConcurrencyToken();
        base.OnModelCreating(modelBuilder);
    }
}
