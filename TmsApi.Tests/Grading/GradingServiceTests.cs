using TmsApi.Application.Grading;
using Xunit;

namespace TmsApi.Tests;

public class GradingServiceTests
{
    [Fact]
    public void CalculateLetterGrade_HighScore_ReturnsDistinction()
    {
        // Arrange
        var service = new GradingService();

        // Act
        var result = service.CalculateLetterGrade(score: 85m, maxScore: 100m);

        // Assert
        Assert.Equal(GradeLevel.Distinction, result);
    }

    [Theory]
    [InlineData(0, 100, GradeLevel.Fail)]
    [InlineData(70, 100, GradeLevel.Distinction)]
    [InlineData(50, 100, GradeLevel.Pass)]
    [InlineData(-1, 100, GradeLevel.Invalid)]
    [InlineData(101, 100, GradeLevel.Invalid)]
    [InlineData(50, 0, GradeLevel.Invalid)]
    public void CalculateLetterGrade_VariousInputs_ReturnsExpectedLevel(
        decimal score,
        decimal maxScore,
        GradeLevel expected
    )
    {
        // Arrange
        var service = new GradingService();

        // Act
        var result = service.CalculateLetterGrade(score, maxScore);

        // Assert
        Assert.Equal(expected, result);
    }
}
