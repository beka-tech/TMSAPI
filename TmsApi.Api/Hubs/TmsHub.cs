using Microsoft.AspNetCore.SignalR;
using TmsApi.Application.Hubs;

namespace TmsApi.Api.Hubs;

public class TmsHub : Hub<ITmsHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var studentId = Context.GetHttpContext()?.Request.Query["studentId"].ToString();
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Student(studentId));
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinCourseGroup(string courseCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
    }

    public async Task LeaveCourseGroup(string courseCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR removes the connection from all groups automatically.
        await base.OnDisconnectedAsync(exception);
    }
}

public static class GroupNames
{
    public static string Student(string studentId) => $"student-{studentId}";

    public static string Course(string courseCode) => $"course-{courseCode}";
}
