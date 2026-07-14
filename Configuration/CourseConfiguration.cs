using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TMSAPI.Entities;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasMany(c => c.Enrollments).WithOne(e => e.Course).HasForeignKey(e => e.CourseId);
        // builder.HasMany.()
    }
}
