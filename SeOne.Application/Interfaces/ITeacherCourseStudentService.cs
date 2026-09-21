using SeOne.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces
{
    public interface ITeacherCourseStudentService
    {
        Task<List<TeacherCourseStudentDto>> GetStudentsForCourseAsync(Guid teacherId, Guid courseId);
    }
}
