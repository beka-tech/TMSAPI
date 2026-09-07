using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TmsApi.Api.Authorization;
using TmsApi.Application.Hubs;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Hubs;

[Authorize]
public class TmsHub(TmsAccess access, TmsDbContext db) : Hub<ITmsHubClient>
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User!.IsInRole("Admin"))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Administrators);
        if (Context.User.IsInRole("Student") && await access.StudentIdAsync(Context.User, Context.ConnectionAborted) is int studentId)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Student(studentId.ToString()));
        await base.OnConnectedAsync();
    }

    public async Task JoinCourseGroup(string courseCode)
    {
        var id = await db.Courses.Where(c => c.Code == courseCode).Select(c => (int?)c.Id).SingleOrDefaultAsync(Context.ConnectionAborted);
        if (id is null || !await access.ManagesCourseAsync(Context.User!, id.Value, Context.ConnectionAborted))
            throw new HubException("You cannot subscribe to this course.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
    }

    public Task LeaveCourseGroup(string courseCode) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
}

public static class GroupNames
{
    public const string Administrators = "administrators";
    public static string Student(string studentId) => $"student-{studentId}";
    public static string Course(string courseCode) => $"course-{courseCode}";
}
