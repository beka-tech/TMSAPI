using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using TMSAPI.Data;

namespace TMSAPI.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (nodatabase contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);
        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);
        Console.WriteLine(">>> STEP 3: Materializing query into a C#List...");
        var results = orderedQuery.ToList(); // Execution is triggeredhere
        Console.WriteLine(">>> STEP 4: Materialization finished. Listpopulated.\n");
        return Ok(results);
    }

    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    /// <summary>
    /// The core issue: expression trees vs. compiled method calls
    ///EF Core tries to convert your LINQ query into SQL,
    /// but it does not know how to convert your custom C# method:
    /// </summary>
    /// <returns>
    /// </returns>
    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context
                .Students.Where(s => s.GPA >= 3.5m) // EF Core does not know how to map this method to SQL
                .ToList();
            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("activestudent")]
    public IActionResult ActiveStudents()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var count = context.Students.Where(s => s.IsActive && s.GPA >= 3.0m).ToList();
            return Ok(count);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }
}
