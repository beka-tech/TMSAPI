// public class EnrollmentWorker
// {
//     private readonly IServiceScopeFactory _scopeFactory;
//     private readonly ILogger<EnrollmentWorker> _logger;

//     public EnrollmentWorker(IServiceScopeFactory scopeFactory, ILogger<EnrollmentWorker> logger)
//     {
//         _scopeFactory = scopeFactory;
//         _logger = logger;
//     }

//     public async Task ProcessBatchAsync()
//     {
//         using var scope = _scopeFactory.CreateScope();
//         var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
//         var enrollments = await enrollmentService.GetAllAsync();
//         _logger.LogInformation("Processing batch of {Count} enrollments", enrollments.Count);
//     }
// }

public class EnrollmentWorker
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<EnrollmentWorker> _logger;

    public EnrollmentWorker(IEnrollmentService enrollmentService, ILogger<EnrollmentWorker> logger)
    {
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public async Task ProcessBatchAsync()
    {
        _logger.LogInformation("Processing scholarship recalculation batch...");

        var enrollments = await _enrollmentService.GetAllAsync();

        foreach (var enrollment in enrollments)
        {
            // Simulate scholarship calculation work
            _logger.LogDebug(
                "Processing enrollment {EnrollmentId} for student {StudentId}",
                enrollment.Id,
                enrollment.StudentId
            );
        }

        _logger.LogInformation(
            "Batch processing completed. Processed {Count} enrollments",
            enrollments.Count
        );
    }
}
