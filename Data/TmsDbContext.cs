using Microsoft.EntityFrameworkCore;
using TMSAPI.Entities;

namespace TMSAPI.Data;

public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<Certificate> certificates => Set<Certificate>();
}
