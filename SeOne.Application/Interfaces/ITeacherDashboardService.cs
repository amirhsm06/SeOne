using SeOne.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces
{
    public interface ITeacherDashboardService
    {
        Task<TeacherDashboardDto?> GetMyDashboardAsync(Guid teacherId);
    }
}
