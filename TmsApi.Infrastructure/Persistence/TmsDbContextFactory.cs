using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using TmsApi.Domain.Enums;

namespace TmsApi.Infrastructure.Persistence;

public sealed class TmsDbContextFactory : IDesignTimeDbContextFactory<TmsDbContext>
{
    public TmsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__TmsDatabase")
            ?? "Host=localhost;Database=tms;Username=postgres";
        var options = new DbContextOptionsBuilder<TmsDbContext>().UseNpgsql(connection,
            npgsql => npgsql.MapEnum<EnrollmentStatus>("enrollment_status", "public", new NpgsqlNullNameTranslator())).Options;
        return new TmsDbContext(options);
    }
}
