using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql.NameTranslation;
using NSubstitute;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Transcripts;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;

namespace TmsApi.Tests.Integration;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TMS_TEST_DB")))
            Skip = "Set TMS_TEST_DB to an isolated PostgreSQL test database.";
    }
}

public class PostgresWorkflowTests
{
    private static TmsDbContext Context()
    {
        var connection = Environment.GetEnvironmentVariable("TMS_TEST_DB")!;
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (builder.Database?.StartsWith("tms_test_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Integration tests require a database named tms_test_*.");
        return new TmsDbContext(new DbContextOptionsBuilder<TmsDbContext>().UseNpgsql(connection,
            o => o.MapEnum<EnrollmentStatus>("enrollment_status", "public", new NpgsqlNullNameTranslator())).Options);
    }

    private static EnrollmentService Service(TmsDbContext db) => new(db, NullLogger<EnrollmentService>.Instance,
        Substitute.For<IEnrollmentStatusNotifier>());

    private static async Task<(int CourseId, int First, int Second)> SeedAsync(TmsDbContext db)
    {
        var code = Guid.NewGuid().ToString("N")[..8];
        var course = new Course { Code = code, Title = "Concurrency course", MaxCapacity = 1 };
        var first = new Student { Name = "First", RegistrationNumber = "A" + code };
        var second = new Student { Name = "Second", RegistrationNumber = "B" + code };
        db.AddRange(course, first, second);
        await db.SaveChangesAsync();
        return (course.Id, first.Id, second.Id);
    }

    [PostgresFact]
    public async Task LastSeat_CannotBeAllocatedTwiceAcrossCreationPaths()
    {
        await using var seed = Context();
        var ids = await SeedAsync(seed);
        async Task<bool> Enroll(int studentId, bool legacy)
        {
            await using var db = Context();
            try
            {
                if (legacy) await Service(db).CreateAsync(ids.CourseId, new EnrollStudentRequest { StudentId = studentId }, default);
                else await Service(db).AddAsync(new Enrollment { CourseId = ids.CourseId, StudentId = studentId }, default);
                return true;
            }
            catch (EnrollmentRejectedException ex) when (ex.Error.Code == "course_full") { return false; }
        }
        var results = await Task.WhenAll(Enroll(ids.First, false), Enroll(ids.Second, true));
        Assert.Single(results, r => r);
        Assert.Equal(1, await seed.Enrollments.CountAsync(e => e.CourseId == ids.CourseId));
    }

    [PostgresFact]
    public async Task StatusRules_ReleaseSeatsAndPersistAuditHistory()
    {
        await using var db = Context();
        var ids = await SeedAsync(db);
        var enrollment = new Enrollment { CourseId = ids.CourseId, StudentId = ids.First };
        var service = Service(db);
        await service.AddAsync(enrollment, default);
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.UpdateGradeAsync(enrollment.Id, 80, default));
        await service.RejectAsync(enrollment.Id, default);
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApproveAsync(enrollment.Id, default));
        await service.AddAsync(new Enrollment { CourseId = ids.CourseId, StudentId = ids.Second }, default);
        Assert.True(await db.AuditEntries.AnyAsync(a => a.EntityType == "Enrollment" && a.EntityId == enrollment.Id.ToString() && a.Changes.Contains("Status")));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.ApproveAsync(int.MaxValue, default));
    }

    [PostgresFact]
    public async Task SimultaneousApprovalAndRejection_AllowOnlyOneTransition()
    {
        await using var seed = Context();
        var ids = await SeedAsync(seed);
        var enrollment = new Enrollment { CourseId = ids.CourseId, StudentId = ids.First };
        await Service(seed).AddAsync(enrollment, default);
        await using var first = Context();
        await using var second = Context();
        await first.Enrollments.SingleAsync(e => e.Id == enrollment.Id);
        await second.Enrollments.SingleAsync(e => e.Id == enrollment.Id);
        await Service(first).ApproveAsync(enrollment.Id, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(second).RejectAsync(enrollment.Id, default));
    }

    [PostgresFact]
    public async Task Transcript_SurvivesNewScopeAndProducesDownloadableContent()
    {
        await using var db = Context();
        var ids = await SeedAsync(db);
        var user = new TmsUser { UserName = Guid.NewGuid().ToString(), StudentId = ids.First };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var notifier = Substitute.For<ITranscriptNotifier>();
        var store = new DatabaseTranscriptStatusStore(db, notifier, NullLogger<DatabaseTranscriptStatusStore>.Instance);
        var job = await store.RequestAsync(ids.First, user.Id, "same-key", default);
        Assert.Equal(job.Id, (await store.RequestAsync(ids.First, user.Id, "same-key", default)).Id);
        await Assert.ThrowsAsync<ResourceConflictException>(() => store.RequestAsync(ids.Second, user.Id, "same-key", default));
        await using var restarted = Context();
        var worker = new DatabaseTranscriptStatusStore(restarted, notifier, NullLogger<DatabaseTranscriptStatusStore>.Instance);
        Assert.True(await worker.ProcessNextAsync(default));
        var ready = await worker.GetJobAsync(job.Id, default);
        Assert.Equal("Ready", ready!.State);
        Assert.Contains("TRAINING TRANSCRIPT", ready.Content);
        Assert.Contains("First", ready.Content);
    }
}
