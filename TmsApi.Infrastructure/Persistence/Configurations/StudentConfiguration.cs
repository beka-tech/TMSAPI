// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
// using TMSAPI.Entities;

// public class StudentConfiguration : IEntityTypeConfiguration<Student>
// {
//     public void Configure(EntityTypeBuilder<Student> builder)
//     {
//         // builder.HasOne(e =>e.Student).WithMany()
//         builder.Property(s => s.GPA).HasPrecision(3, 2);
//         builder.Property(s => s.Name).IsRequired().HasMaxLength(200);

//         // SHADOW PROPERTY
//         builder
//             .Property<DateTime>("LastUpdated")
//             .HasColumnName("LastUpdated")
//             .HasColumnType("datetime2")
//             .HasDefaultValueSql("GETUTCDATE()");

//         //CONCURRENCY TOKEN
//     }
// }
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TMSAPI.Configurations;

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

        builder.Property<DateTime>("LastUpdated").HasDefaultValueSql("NOW()").IsConcurrencyToken();
    }
}
