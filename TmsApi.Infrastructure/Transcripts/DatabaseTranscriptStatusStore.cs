using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Common;
using TmsApi.Application.Transcripts;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Transcripts;

public sealed class DatabaseTranscriptStatusStore(TmsDbContext db, ITranscriptNotifier notifier,
    ILogger<DatabaseTranscriptStatusStore> logger) : ITranscriptStatusStore
{
    public async Task<TranscriptJob> RequestAsync(int studentId, string requestedBy, string? idempotencyKey, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Per-account lock makes the idempotency check and insert atomic across instances.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {requestedBy} FOR UPDATE", ct);
        if (!await db.Students.AnyAsync(s => s.Id == studentId, ct))
            throw new ResourceNotFoundException("Student not found.");
        if (idempotencyKey is not null)
        {
            var existing = await db.TranscriptJobs.SingleOrDefaultAsync(j => j.RequestedBy == requestedBy && j.IdempotencyKey == idempotencyKey, ct);
            if (existing is not null)
            {
                if (existing.StudentId != studentId) throw new ResourceConflictException("Idempotency key was used with a different request.");
                return existing;
            }
        }
        if (await db.TranscriptJobs.CountAsync(j => j.State == "Queued", ct) >= 1000)
            throw new ResourceConflictException("Transcript queue is full. Retry later.");
        var job = new TranscriptJob { StudentId = studentId, RequestedBy = requestedBy, IdempotencyKey = idempotencyKey };
        db.TranscriptJobs.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return job;
    }

    public Task<TranscriptJob?> GetJobAsync(string reportId, CancellationToken ct) =>
        db.TranscriptJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == reportId, ct);

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        // Keep the row lock until generation and output commit. A crash rolls back to Queued.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var jobs = await db.TranscriptJobs.FromSqlRaw("""
            SELECT * FROM "TranscriptJobs" WHERE "State" = 'Queued'
            ORDER BY "RequestedAt" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var job = jobs.SingleOrDefault();
        if (job is null) return false;
        job.StartedAt = DateTime.UtcNow;
        var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == job.StudentId, ct);
        if (student is null)
        {
            job.State = "Failed";
            job.ErrorMessage = "Student is no longer available.";
        }
        else
        {
            var rows = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == student.Id)
                .OrderBy(e => e.Course.Code).Select(e => new { e.Course.Code, e.Course.Title, e.Status, e.Grade }).ToListAsync(ct);
            var text = new StringBuilder();
            text.AppendLine("TRAINING TRANSCRIPT");
            text.AppendLine($"Report: {job.Id}");
            text.AppendLine($"Student: {student.Name} ({student.RegistrationNumber})");
            text.AppendLine($"Generated UTC: {DateTime.UtcNow:O}");
            text.AppendLine();
            foreach (var row in rows)
                text.AppendLine($"{row.Code} | {row.Title} | {row.Status} | Grade: {row.Grade?.ToString(CultureInfo.InvariantCulture) ?? "Not assigned"}");
            job.Content = text.ToString();
            job.State = "Ready";
        }
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (job.State == "Ready")
        {
            try { await notifier.TranscriptReadyAsync(job.StudentId, job.Id, $"/api/v2/transcripts/{job.Id}/download", ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Transcript {ReportId} is ready but notification failed", job.Id); }
        }
        return true;
    }
}
