using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers.V2;

[Authorize(Roles = "Instructor,Admin")]
[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[ApiVersion("2.0")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CourseControllers(
    ICourseService courseService,
    LinkGenerator linkGenerator,
    TmsDbContext context,
    IAuthorizationService authorizationService
) : ControllerBase
{
    private readonly ICourseService _courseService = courseService;
    private readonly LinkGenerator _linkGenerator = linkGenerator;
    private readonly TmsDbContext _context = context;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    // ============================================================
    // GET: api/courses
    // ============================================================

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription(
        "Returns a paginated, optionally filtered list of TMS courses. PageSize is capped at 50."
    )]
    public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request,
        CancellationToken ct
    )
    {
        var result = await _courseService.GetCoursesAsync(request, ct);

        return Ok(result);
    }

    // ============================================================
    // GET: api/courses/{id}
    // ============================================================

    [HttpGet("{id:int}", Name = "GetCourseByIdV2")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription(
        "Returns course details with HATEOAS links. Returns 404 if the course does not exist."
    )]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await _courseService.GetByIdAsync(id, ct);

        if (course is null)
            return NotFound();

        var selfPath = _linkGenerator.GetPathByName(HttpContext, "GetCourseByIdV2", new { id })!;

        var enrollmentsPath = _linkGenerator.GetPathByName(
            HttpContext,
            "ListCourseEnrollments",
            new { courseId = id }
        )!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE"),
            new(enrollmentsPath, "enrollments", "GET"),
        };

        if (course.EnrollmentCount < course.MaxCapacity)
        {
            links.Add(new LinkDto(enrollmentsPath, "enroll", "POST"));
        }

        var detail = new CourseDetailDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            MaxCapacity = course.MaxCapacity,
            EnrollmentCount = course.EnrollmentCount,
            Links = links,
        };

        return Ok(detail);
    }

    // ============================================================
    // POST: api/courses
    // ============================================================

    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription(
        "Creates a course with a unique code. Returns 409 if the course code already exists."
    )]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
        if (await _courseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course code already exists",
                    Detail = $"A course with code '{request.Code}' is already registered.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        var result = await _courseService.CreateAsync(request, ct);

        return CreatedAtRoute("GetCourseByIdV2", new { id = result.Id }, result);
    }

    // ============================================================
    // PUT: api/courses/{id}
    // Resource-based authorization
    // ============================================================

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Update a course")]
    [EndpointDescription(
        "Updates a course only when the authenticated instructor owns the course or the authorization policy allows access."
    )]
    public async Task<IActionResult> UpdateCourse(
        int id,
        [FromBody] UpdateCourseDto dto,
        CancellationToken ct
    )
    {
        // 1. Find the actual resource
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
        {
            return NotFound();
        }

        // 2. Ask ASP.NET Core authorization system:
        // "Is this user allowed to edit THIS specific course?"
        var authResult = await _authorizationService.AuthorizeAsync(User, course, "CanEditCourse");

        // 3. User is authenticated but does not own/have permission
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        // 4. Authorized → modify resource
        course.Title = dto.Title;

        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}


// public class CoursesController(TmsDbContext context) : ControllerBase
// {
//     [HttpGet]
//     public async Task<IActionResult> GetCourses(
//         [FromQuery] int page = 1,
//         [FromQuery] int pageSize = 20,
//         CancellationToken ct = default
//     )
//     {
//         page = Math.Max(1, page);
//         pageSize = Math.Clamp(pageSize, 1, 50);
//         var baseQuery = context.Courses.AsNoTracking();
//         var totalCount = await baseQuery.CountAsync(ct);
//         var rows = await baseQuery
//             .OrderBy(c => c.Title)
//             .Skip((page - 1) * pageSize)
//             .Take(pageSize)
//             .Select(c => new
//             {
//                 c.Id,
//                 c.Title,
//                 c.Code,
//                 c.MaxCapacity,
//                 EnrollmentCount = c.Enrollments.Count,
//             })
//             .ToListAsync(ct);
//         var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
//         var hasNext = page < totalPages;
//         var hasPrevious = page > 1;
//         return Ok(
//             new
//             {
//                 data = rows,
//                 meta = new
//                 {
//                     totalCount,
//                     page,
//                     pageSize,
//                     totalPages,
//                     hasNext,
//                     hasPrevious,
//                 },
//                 links = new
//                 {
//                     self = $"/api/v2/courses?page={page}&pageSize={pageSize}",
//                     next = hasNext
//                         ? $"/api/v2/courses?page={page + 1}&pageSize={pageSize}"
//                         : (string?)null,
//                     prev = hasPrevious
//                         ? $"/api/v2/courses?page={page - 1}&pageSize={pageSize}"
//                         : (string?)null,
//                     enroll = "/api/v2/enrollments",
//                 },
//             }
//         );
//     }
// }
