using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<Student>> GetAllAsync(int pageSize, int pageNumber);
    Task<Student?> GetByIdAsync(int id);
    Task<bool> DeleteAsync(int id);
}
