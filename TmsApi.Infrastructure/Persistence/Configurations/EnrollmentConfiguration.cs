using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        ///
        /// Student 1 ---- * Enrollment * ---- 1 Course
        ///
        /// Enrollment is Junction Table
        ///
        ///
        // builder.HasOne(e =>e.Student).WithMany()
        // Student -> Enrollment (1-to-Many)
        builder
            .HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Course -> Enrollment (1-to-Many)
        builder
            .HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .Property(e => e.Status)
            .HasColumnType("enrollment_status")
            .HasDefaultValue(EnrollmentStatus.Pending)
            .IsRequired();
    }
}
