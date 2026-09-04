using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Tests.Students;

public class StudentModelConfigurationTests
{
    [Fact]
    public void StudentModel_HasSoftDeleteFilterAndUniqueRegistrationNumber()
    {
        var options = new DbContextOptionsBuilder<TmsDbContext>()
            .UseNpgsql("Host=localhost;Database=tms_model_test;Username=postgres")
            .Options;

        using var context = new TmsDbContext(options);
        var studentType = context.Model.FindEntityType(typeof(Student));

        Assert.NotNull(studentType);
        Assert.NotEmpty(studentType.GetDeclaredQueryFilters());

        var registrationIndex = Assert.Single(
            studentType.GetIndexes(),
            index =>
                index.Properties.Count == 1
                && index.Properties[0].Name == nameof(Student.RegistrationNumber)
        );

        Assert.True(registrationIndex.IsUnique);
    }
}
