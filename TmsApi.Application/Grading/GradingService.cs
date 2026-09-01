namespace TmsApi.Application.Grading;

public class GradingService
{
    public const decimal DistinctionThreshold = 70m;
    public const decimal PassThreshold = 50m;

    // Pure mapping: one score against one maximum.
    // Uses M5's Assessment.MaxScore and the decimal part of
    //Enrollment.Grade.
    public GradeLevel CalculateLetterGrade(decimal score, decimal maxScore)
    {
        if (maxScore <= 0m || score < 0m || score > maxScore)
            return GradeLevel.Invalid;
        var pct = score / maxScore * 100m;
        return pct >= DistinctionThreshold ? GradeLevel.Distinction
            : pct >= PassThreshold ? GradeLevel.Pass
            : GradeLevel.Fail;
    }

    // Single-decimal path: maps an Enrollment.Grade percentage to aGradeLevel.
    // Enrollment.Grade is nullable per the M5 entity; null => Invalid.
    public GradeLevel CalculateFromEnrollmentGrade(decimal? enrollmentGradePercent)
    {
        if (enrollmentGradePercent is null)
            return GradeLevel.Invalid;
        return CalculateLetterGrade(enrollmentGradePercent.Value, maxScore: 100m);
    }
}
