using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<Student>> GetAllAsync(
        int pageSize,
        int pageNumber,
        CancellationToken ct = default
    );
    Task<Student?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Student> CreateAsync(CreateStudentRequest request, CancellationToken ct = default);
    Task<Student?> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken ct = default
    );
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
