using SeOne.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces
{
    public interface ICourseDetailsService
    {
        Task<CourseDetailsDto?> GetCourseDetailsAsync(Guid courseId, Guid? userId, string userRole);
    }
}
