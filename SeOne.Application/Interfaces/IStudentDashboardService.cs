using SeOne.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces
{
    public interface IStudentDashboardService
    {
        Task<StudentDashboardDto?> GetMyDashboardAsync(Guid studentId);
    }
}
