namespace TmsApi.Application.Hubs;

public interface ITmsHubClient
{
    Task ReceiveTranscriptReady(string reportId, string downloadUrl);
    Task ReceiveCourseUpdate(string courseCode, string message);
    Task ReceiveGradePosted(string courseCode, int studentId, decimal grade);
    Task ReceiveEnrollmentStatusUpdated(string v1, string v2);
}
