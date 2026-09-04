using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.RegistrationNumber).IsRequired().HasMaxLength(20);

        builder.HasIndex(s => s.RegistrationNumber).IsUnique();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);

        builder.Property(s => s.GPA).HasPrecision(3, 2);

        builder.Property(s => s.IsActive).IsRequired();

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder
            .Property<DateTime>("LastUpdated")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsConcurrencyToken();
    }
}
