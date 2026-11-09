using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;
using SeOne.Api.Services;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/dashboard")]
[Authorize(Roles = "Student")]
public class StudentDashboardCompatibilityController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    public StudentDashboardCompatibilityController(SeOneDbContext db) => _db = db;

    [HttpGet("upcoming-classes")]
    public async Task<IActionResult> Upcoming([FromQuery] int limit = 10)
        => Ok(await UpcomingValues(RequireUserId(), Math.Clamp(limit, 1, 50)));
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery]string? startDate,[FromQuery]string? endDate,[FromQuery]string? type)
    {
        var userId=RequireUserId(); var start=ParseDate(startDate,DateTime.UtcNow.Date.AddDays(-30));var end=ParseDate(endDate,DateTime.UtcNow.Date.AddDays(60));var events=new List<object>();
        var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Where(b=>b.StudentId==userId&&b.StartTime<end.AddDays(1)&&b.EndTime>=start).ToListAsync();events.AddRange(bookings.Select(b=>new{ id=b.Id,title=b.Course?.Title??"Class",description="Teacher session",start=b.StartTime,end=b.EndTime,type="class",courseId=b.CourseId,courseTitle=b.Course?.Title,color=(string?)null }));
        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => x.StudentId == userId && x.Status == "confirmed" &&
                x.CourseClass.Status != "cancelled" &&
                x.CourseClass.ScheduledDate >= start.Date && x.CourseClass.ScheduledDate < end.Date.AddDays(1))
            .ToListAsync();
        var classIds = classEnrollments.Select(x => x.ClassId).Distinct().ToArray();
        var classSlotAssignments = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.Teacher)
            .Where(x => classIds.Contains(x.ClassSlotId) && x.Status == "confirmed")
            .ToListAsync();
        foreach (var enrollment in classEnrollments)
        {
            var slot = enrollment.CourseClass;
            var matchingAssignments = classSlotAssignments.Where(x => x.ClassSlotId == slot.Id).ToList();
            if (matchingAssignments.Count == 0)
                events.Add(new { id = slot.Id, title = slot.Course.Title, description = slot.Title, start = slot.ScheduledDate.Date + slot.StartTime, end = slot.ScheduledDate.Date + slot.EndTime, type = "class", courseId = (Guid?)slot.CourseId, courseTitle = (string?)slot.Course.Title, color = (string?)null, teacherId = (Guid?)null, teacherName = (string?)null, meetingUrl = slot.MeetingUrl, classType = "group_class" });
            else
                foreach (var assignment in matchingAssignments)
                    events.Add(new { id = slot.Id, title = slot.Course.Title, description = slot.Title, start = slot.ScheduledDate.Date + slot.StartTime, end = slot.ScheduledDate.Date + slot.EndTime, type = "class", courseId = (Guid?)slot.CourseId, courseTitle = (string?)slot.Course.Title, color = (string?)null, teacherId = (Guid?)assignment.TeacherId, teacherName = (string?)assignment.Teacher.FullName, meetingUrl = slot.MeetingUrl, classType = "group_class" });
        }
        var courseIds = (await _db.Set<Enrollment>().AsNoTracking()
                .Where(e => e.StudentId == userId)
                .Select(e => e.CourseId).ToListAsync())
            .Concat(await _db.Set<ClassEnrollment>().AsNoTracking()
                .Where(e => e.StudentId == userId && e.Status == "confirmed")
                .Select(e => e.CourseClass.CourseId).ToListAsync())
            .Distinct().ToList();
        var assignments = await _db.Set<Assignment>().AsNoTracking()
            .Where(a => courseIds.Contains(a.CourseId) && a.DueDate >= start && a.DueDate < end && a.IsPublished)
            .ToListAsync();
        events.AddRange(assignments.Select(a => new { id = a.Id, title = a.Title, description = a.Description, start = a.DueDate.AddHours(-1), end = a.DueDate, type = "assignment_due", courseId = (Guid?)a.CourseId, courseTitle = (string?)null, color = (string?)null }));
        var practices = await _db.Set<Practice>().AsNoTracking()
            .Where(p => courseIds.Contains(p.CourseId) && p.IsPublished).ToListAsync();
        foreach (var p in practices)
            events.Add(new { id = p.Id, title = p.Title, description = p.Description, start = p.CreatedAt, end = p.CreatedAt.AddMinutes(p.TimeLimit ?? 15), type = "practice", courseId = (Guid?)p.CourseId, courseTitle = (string?)null, color = (string?)null });
        if(!string.IsNullOrWhiteSpace(type))events=events.Where(e=>e.GetType().GetProperty("type")?.GetValue(e)?.ToString()==type).ToList();return Ok(events.OrderBy(e=>(DateTime)(e.GetType().GetProperty("start")?.GetValue(e)??DateTime.MinValue)));
    }
    [HttpGet("recent-activity")]
    public async Task<IActionResult> Recent([FromQuery]int limit=10)
    {
        var userId=RequireUserId();var all=new List<(DateTime date,object value)>();
        var lessons=await _db.LessonProgress.AsNoTracking().Include(p=>p.Lesson).ThenInclude(l=>l.CourseModule).Where(p=>p.StudentId==userId&&p.IsCompleted&&p.CompletedAt!=null).OrderByDescending(p=>p.CompletedAt).Take(limit).ToListAsync();foreach(var p in lessons)all.Add((p.CompletedAt!.Value,new{id=$"lesson-{p.LessonId}",type="lesson_completed",title=$"Completed {p.Lesson.Title}",description="Lesson completed",courseId=(Guid?)p.Lesson.CourseModule.CourseId,courseTitle=(string?)null,timestamp=p.CompletedAt.Value,metadata=new{lessonId=p.LessonId}}));
        var enroll=await _db.Set<Enrollment>().AsNoTracking().Include(e=>e.Course).Where(e=>e.StudentId==userId).OrderByDescending(e=>e.EnrolledAt).Take(limit).ToListAsync();foreach(var e in enroll)all.Add((e.EnrolledAt,new{id=$"enrollment-{e.Id}",type="course_enrolled",title=$"Enrolled in {e.Course.Title}",description="Course enrollment created",courseId=(Guid?)e.CourseId,courseTitle=(string?)e.Course.Title,timestamp=e.EnrolledAt,metadata=(object?)null}));
        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => x.StudentId == userId)
            .OrderByDescending(x => x.EnrolledAt).Take(limit).ToListAsync();
        foreach (var enrollment in classEnrollments)
            all.Add((enrollment.EnrolledAt, new { id = $"class-enrollment-{enrollment.Id}", type = "class_enrolled", title = $"Joined {enrollment.CourseClass.Title}", description = "Group class enrollment created", courseId = (Guid?)enrollment.CourseClass.CourseId, courseTitle = (string?)enrollment.CourseClass.Course.Title, timestamp = enrollment.EnrolledAt, metadata = (object?)new { classId = enrollment.ClassId, status = enrollment.Status } }));
        var submissions=await _db.AssignmentSubmissions.AsNoTracking().Include(s=>s.Assignment).Where(s=>s.StudentId==userId).OrderByDescending(s=>s.SubmittedAt).Take(limit).ToListAsync();foreach(var s in submissions)all.Add((s.SubmittedAt,new{id=$"assignment-{s.Id}",type="assignment_submitted",title=$"Submitted {s.Assignment.Title}",description="Assignment submitted",courseId=(Guid?)s.Assignment.CourseId,courseTitle=(string?)null,timestamp=s.SubmittedAt,metadata=new{submissionId=s.Id}}));
        var attempts=await _db.PracticeAttempts.AsNoTracking().Include(a=>a.Practice).Where(a=>a.StudentId==userId&&a.SubmittedAt!=null).OrderByDescending(a=>a.SubmittedAt).Take(limit).ToListAsync();foreach(var a in attempts)all.Add((a.SubmittedAt!.Value,new{id=$"practice-{a.Id}",type="practice_completed",title=$"Completed {a.Practice.Title}",description=$"Score {a.Score}/{a.MaxScore}",courseId=(Guid?)a.Practice.CourseId,courseTitle=(string?)null,timestamp=a.SubmittedAt.Value,metadata=new{attemptId=a.Id,score=a.Score,maxScore=a.MaxScore}}));
        var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Where(b=>b.StudentId==userId).OrderByDescending(b=>b.CreatedAt).Take(limit).ToListAsync();foreach(var b in bookings)all.Add((b.CreatedAt,new{id=$"booking-{b.Id}",type="booking_created",title="Teacher booking created",description=b.Course?.Title??"Booking",courseId=b.CourseId,courseTitle=b.Course?.Title,timestamp=b.CreatedAt,metadata=new{bookingId=b.Id}}));
        return Ok(all.OrderByDescending(x=>x.date).Take(Math.Clamp(limit,1,50)).Select(x=>x.value));
    }
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics()=>Ok(await BuildStatisticsAsync(RequireUserId()));
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(){var id=RequireUserId();return Ok(new{upcomingClasses=await UpcomingValues(id),recentActivity=await RecentValues(id),statistics=await BuildStatisticsAsync(id)});}

    private async Task<object[]> UpcomingValues(Guid id) => await UpcomingValues(id, 10);

    private async Task<object[]> UpcomingValues(Guid id, int limit)
    {
        var now = DateTime.UtcNow;
        var items = new List<(DateTime StartsAt, object Value)>();
        var bookings = await _db.Bookings.AsNoTracking().Include(x => x.Course).Include(x => x.Teacher)
            .Where(x => x.StudentId == id && x.EndTime >= now)
            .OrderBy(x => x.StartTime).Take(limit).ToListAsync();
        foreach (var booking in bookings)
            items.Add((booking.StartTime, await MapBookingAsync(booking)));

        var enrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => x.StudentId == id && x.Status == "confirmed" && x.CourseClass.Status != "cancelled" &&
                (x.CourseClass.ScheduledDate.Date > now.Date || (x.CourseClass.ScheduledDate.Date == now.Date && x.CourseClass.EndTime >= now.TimeOfDay)))
            .OrderBy(x => x.CourseClass.ScheduledDate).ThenBy(x => x.CourseClass.StartTime)
            .Take(limit).ToListAsync();
        var slotIds = enrollments.Select(x => x.ClassId).Distinct().ToArray();
        var assignedTeachers = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.Teacher).Where(x => slotIds.Contains(x.ClassSlotId) && x.Status == "confirmed")
            .ToListAsync();
        foreach (var enrollment in enrollments)
        {
            var slot = enrollment.CourseClass;
            var start = slot.ScheduledDate.Date + slot.StartTime;
            var end = slot.ScheduledDate.Date + slot.EndTime;
            var teachers = assignedTeachers.Where(x => x.ClassSlotId == slot.Id).ToList();
            if (teachers.Count == 0)
                items.Add((start, new { id = slot.Id, courseId = slot.CourseId, courseTitle = slot.Course.Title, teacherId = (Guid?)null, teacherName = (string?)null, teacherAvatar = (string?)null, subject = slot.Subject, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm"), location = (string?)null, meetingUrl = slot.MeetingUrl, status = ClassSlotStatus(slot.Status, start, end), type = "group_class", classId = slot.Id, assignmentId = (Guid?)null, timeZone = slot.TimeZone }));
            else
                foreach (var teacher in teachers)
                    items.Add((start, new { id = slot.Id, courseId = slot.CourseId, courseTitle = slot.Course.Title, teacherId = (Guid?)teacher.TeacherId, teacherName = (string?)teacher.Teacher.FullName, teacherAvatar = (string?)teacher.Teacher.AvatarUrl, subject = slot.Subject, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm"), location = (string?)null, meetingUrl = slot.MeetingUrl, status = ClassSlotStatus(slot.Status, start, end), type = "group_class", classId = slot.Id, assignmentId = (Guid?)teacher.Id, timeZone = slot.TimeZone }));
        }

        return items.OrderBy(x => x.StartsAt).Take(limit).Select(x => x.Value).ToArray();
    }
    private async Task<object[]> RecentValues(Guid id)
    {
        var events = new List<(DateTime At, object Value)>();
        var lessons = await _db.LessonProgress.AsNoTracking()
            .Where(x => x.StudentId == id && x.IsCompleted && x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt).Take(5)
            .Select(x => new { x.LessonId, x.CompletedAt }).ToListAsync();
        foreach (var lesson in lessons)
            events.Add((lesson.CompletedAt!.Value, new { id = $"lesson-{lesson.LessonId}", type = "lesson_completed", title = "Lesson completed", description = "Lesson completed", timestamp = lesson.CompletedAt }));

        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => x.StudentId == id)
            .OrderByDescending(x => x.EnrolledAt).Take(5).ToListAsync();
        foreach (var enrollment in classEnrollments)
            events.Add((enrollment.EnrolledAt, new { id = $"class-enrollment-{enrollment.Id}", type = "class_enrolled", title = $"Joined {enrollment.CourseClass.Title}", description = "Group class enrollment created", courseId = (Guid?)enrollment.CourseClass.CourseId, courseTitle = (string?)enrollment.CourseClass.Course.Title, timestamp = enrollment.EnrolledAt }));

        return events.OrderByDescending(x => x.At).Take(5).Select(x => x.Value).ToArray();
    }
    private async Task<object> BuildStatisticsAsync(Guid userId)
    {
        var today=DateTime.UtcNow.Date;var week=today.AddDays(-6);var month=today.AddDays(-29);
        var sessions=await _db.LearningSessions.AsNoTracking().Where(s=>s.StudentId==userId&&s.EndedAt!=null&&s.StartedAt>=month).Select(s=>new{s.StartedAt,s.EndedAt}).ToListAsync();
        var total=await _db.LessonProgress.Where(p=>p.StudentId==userId).SumAsync(p=>(long?)p.TimeSpent)??0;var todayTime=sessions.Where(s=>s.StartedAt>=today).Sum(s=>Duration(s.StartedAt,s.EndedAt));var weekTime=sessions.Where(s=>s.StartedAt>=week).Sum(s=>Duration(s.StartedAt,s.EndedAt));var monthTime=sessions.Sum(s=>Duration(s.StartedAt,s.EndedAt));
        var enrolled = (await _db.Set<Enrollment>().Where(e => e.StudentId == userId).Select(e => e.CourseId).ToListAsync())
            .Concat(await _db.Set<ClassEnrollment>().Where(e => e.StudentId == userId && e.Status == "confirmed").Select(e => e.CourseClass.CourseId).ToListAsync())
            .Distinct().ToList();
        var lessonsCompleted = await _db.LessonProgress.CountAsync(p => p.StudentId == userId && p.IsCompleted);
        var coursesCompleted = 0;
        foreach (var courseId in enrolled)
        {
            var totalLessons = await _db.Lessons.CountAsync(l => l.CourseModule.CourseId == courseId);
            var doneLessons = await _db.LessonProgress.CountAsync(p => p.StudentId == userId && p.IsCompleted && p.Lesson.CourseModule.CourseId == courseId);
            if (totalLessons > 0 && totalLessons == doneLessons) coursesCompleted++;
        }
        var attempts=await _db.PracticeAttempts.Where(a=>a.StudentId==userId&&a.SubmittedAt!=null&&a.MaxScore>0).ToListAsync();var avg=attempts.Count==0?0:Math.Round(attempts.Average(a=>a.Score*100d/a.MaxScore),2);var activity=new List<object>();for(var i=6;i>=0;i--){var day=today.AddDays(-i);activity.Add(new{date=day.ToString("yyyy-MM-dd"),minutes=sessions.Where(s=>s.StartedAt.Date==day).Sum(s=>Duration(s.StartedAt,s.EndedAt))});}
        var currentStreak=await Streak(userId,false);var longest=await Streak(userId,true);return new{totalStudyTime=(int)Math.Min(int.MaxValue,Math.Max(total,monthTime)),todayStudyTime=todayTime,weekStudyTime=weekTime,monthStudyTime=monthTime,dailyGoal=30,dailyGoalProgress=Math.Min(100,Math.Round(todayTime*100d/30d,2)),currentStreak,longestStreak=longest,lessonsCompleted,coursesInProgress=Math.Max(0,enrolled.Count-coursesCompleted),coursesCompleted,averageScore=avg,weeklyActivity=activity,activityByType=new{lessons=lessonsCompleted,practice=attempts.Count,assignments=await _db.AssignmentSubmissions.CountAsync(s=>s.StudentId==userId),classes=(await _db.Bookings.CountAsync(b=>b.StudentId==userId&&b.Status==BookingStatus.Completed)) + (await _db.Set<ClassEnrollment>().CountAsync(e=>e.StudentId==userId&&e.Status=="confirmed"&&e.CourseClass.Status!="cancelled"&&(e.CourseClass.Status=="completed"||e.CourseClass.ScheduledDate.Date<today||(e.CourseClass.ScheduledDate.Date==today&&e.CourseClass.EndTime<=DateTime.UtcNow.TimeOfDay))))}};
    }
    private async Task<object> MapBookingAsync(Booking b)=>new{id=b.Id,courseId=b.CourseId??Guid.Empty,courseTitle=b.Course?.Title??string.Empty,teacherId=b.TeacherId,teacherName=b.Teacher.FullName,teacherAvatar=b.Teacher.AvatarUrl,subject=await _db.Set<TeacherProfile>().Where(p=>p.TeacherId==b.TeacherId).Select(p=>p.Subject).FirstOrDefaultAsync()??string.Empty,scheduledDate=b.StartTime.ToString("yyyy-MM-dd"),startTime=b.StartTime.ToString("HH:mm"),endTime=b.EndTime.ToString("HH:mm"),location=(string?)null,meetingUrl=(string?)null,status=BookingStatusName(b.Status,b.StartTime,b.EndTime),type="one_on_one"};
    private static string BookingStatusName(BookingStatus status,DateTime start,DateTime end)=>status==BookingStatus.Cancelled?"cancelled":end<=DateTime.UtcNow?"completed":start<=DateTime.UtcNow?"ongoing":"scheduled";
    private static string ClassSlotStatus(string status, DateTime start, DateTime end)
    {
        if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            return status.ToLowerInvariant();
        return end <= DateTime.UtcNow ? "completed" : start <= DateTime.UtcNow ? "ongoing" : "scheduled";
    }
    private static int Duration(DateTime start,DateTime? end)=>(int)Math.Max(0,(end.GetValueOrDefault(start)-start).TotalMinutes);
    private async Task<bool> Completed(Guid userId,Guid lessonId)=>await _db.LessonProgress.AnyAsync(p=>p.StudentId==userId&&p.LessonId==lessonId&&p.IsCompleted);
    private async Task<int> Streak(Guid id,bool longest){var dates=(await _db.LessonProgress.Where(p=>p.StudentId==id&&p.LastAccessedAt!=null).Select(p=>p.LastAccessedAt!.Value.Date).ToListAsync()).Concat(await _db.LearningSessions.Where(s=>s.StudentId==id).Select(s=>s.StartedAt.Date).ToListAsync()).Distinct().OrderBy(d=>d).ToList();if(dates.Count==0)return 0;var best = 1; var cur=1;for(int i=1;i<dates.Count;i++){if(dates[i]==dates[i-1].AddDays(1))cur++;else cur=1;best=Math.Max(best,cur);}if(longest)return best;var today=DateTime.UtcNow.Date;var cursor=dates.Contains(today)?today:today.AddDays(-1);var result=0;var set=dates.ToHashSet();while(set.Contains(cursor)){result++;cursor=cursor.AddDays(-1);}return result;}
    private static DateTime ParseDate(string? v,DateTime fallback)=>DateTime.TryParse(v,out var d)?d:DateTime.SpecifyKind(fallback,DateTimeKind.Utc);
}

[ApiController]
[Route("api/teacher/dashboard")]
[Authorize(Roles = "Teacher")]
public class TeacherDashboardCompatibilityController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    public TeacherDashboardCompatibilityController(SeOneDbContext db)=>_db=db;
    [HttpGet("classes")]
    public async Task<IActionResult> Classes([FromQuery]string? status,[FromQuery]string? startDate,[FromQuery]string? endDate)=>Ok(await GetClasses(RequireUserId(),status,startDate,endDate));
    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule([FromQuery]string? startDate,[FromQuery]string? endDate)=>Ok(await GetClasses(RequireUserId(),null,startDate,endDate));
    [HttpGet("students")]
    public async Task<IActionResult> Students([FromQuery] Guid? courseId,
        [FromQuery] bool activeOnly = false)
    {
        var teacherId = RequireUserId();
        var result = new List<object>();

        var legacyCourseIds = await _db.Set<CourseInstanceTeacher>().AsNoTracking()
            .Where(x => x.TeacherId == teacherId && (courseId == null || x.CourseInstance.CourseId == courseId))
            .Select(x => x.CourseInstance.CourseId).Distinct().ToListAsync();
        var legacyEnrollments = await _db.Set<Enrollment>().AsNoTracking()
            .Include(x => x.Student).Include(x => x.Course)
            .Where(x => legacyCourseIds.Contains(x.CourseId)).Take(200).ToListAsync();
        foreach (var enrollment in legacyEnrollments)
        {
            var total = await _db.Lessons.CountAsync(x => x.CourseModule.CourseId == enrollment.CourseId);
            var completed = await _db.LessonProgress.CountAsync(x => x.StudentId == enrollment.StudentId && x.IsCompleted && x.Lesson.CourseModule.CourseId == enrollment.CourseId);
            var lastActivity = await _db.LessonProgress.Where(x => x.StudentId == enrollment.StudentId && x.Lesson.CourseModule.CourseId == enrollment.CourseId && x.LastAccessedAt != null)
                .OrderByDescending(x => x.LastAccessedAt).Select(x => x.LastAccessedAt).FirstOrDefaultAsync();
            var upcoming = await _db.Bookings.AsNoTracking().Where(x => x.TeacherId == teacherId && x.StudentId == enrollment.StudentId && x.CourseId == enrollment.CourseId && x.StartTime > DateTime.UtcNow && x.Status != BookingStatus.Cancelled)
                .OrderBy(x => x.StartTime).Select(x => new { id = x.Id, scheduledDate = x.StartTime.ToString("yyyy-MM-dd"), startTime = x.StartTime.ToString("HH:mm") }).FirstOrDefaultAsync();
            if (activeOnly && upcoming is null && lastActivity is null) continue;
            result.Add(new { studentId = enrollment.StudentId, studentName = enrollment.Student.FullName, studentAvatar = enrollment.Student.AvatarUrl, studentEmail = enrollment.Student.Email, courseId = enrollment.CourseId, courseTitle = enrollment.Course.Title, enrolledAt = enrollment.EnrolledAt, progress = total == 0 ? 0 : Math.Round(completed * 100d / total, 2), completedLessons = completed, totalLessons = total, lastActivity = lastActivity ?? enrollment.EnrolledAt, upcomingClass = upcoming });
        }

        var assignedSlots = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.ClassSlot).ThenInclude(x => x.Course)
            .Where(x => x.TeacherId == teacherId && x.Status == "confirmed" && (courseId == null || x.ClassSlot.CourseId == courseId))
            .ToListAsync();
        var slotIds = assignedSlots.Select(x => x.ClassSlotId).Distinct().ToArray();
        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.Student).Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => slotIds.Contains(x.ClassId) && x.Status == "confirmed")
            .OrderBy(x => x.EnrolledAt).Take(500).ToListAsync();

        foreach (var enrollment in classEnrollments)
        {
            var slot = enrollment.CourseClass;
            var total = await _db.Lessons.CountAsync(x => x.CourseModule.CourseId == slot.CourseId);
            var completed = await _db.LessonProgress.CountAsync(x => x.StudentId == enrollment.StudentId && x.IsCompleted && x.Lesson.CourseModule.CourseId == slot.CourseId);
            var lastActivity = await _db.LessonProgress.Where(x => x.StudentId == enrollment.StudentId && x.Lesson.CourseModule.CourseId == slot.CourseId && x.LastAccessedAt != null)
                .OrderByDescending(x => x.LastAccessedAt).Select(x => x.LastAccessedAt).FirstOrDefaultAsync();
            var start = slot.ScheduledDate.Date + slot.StartTime;
            var upcoming = start > DateTime.UtcNow
                ? new { id = slot.Id, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm") }
                : null;
            if (activeOnly && upcoming is null && lastActivity is null) continue;
            var assignmentId = assignedSlots.First(x => x.ClassSlotId == slot.Id).Id;
            result.Add(new { studentId = enrollment.StudentId, studentName = enrollment.Student.FullName, studentAvatar = enrollment.Student.AvatarUrl, studentEmail = enrollment.Student.Email, courseId = slot.CourseId, courseTitle = slot.Course.Title, enrolledAt = enrollment.EnrolledAt, progress = total == 0 ? 0 : Math.Round(completed * 100d / total, 2), completedLessons = completed, totalLessons = total, lastActivity = lastActivity ?? (DateTime?)null, upcomingClass = upcoming, classId = slot.Id, assignmentId, classTitle = slot.Title });
        }

        return Ok(result);
    }
    [HttpGet("earnings")]
    public async Task<IActionResult> Earnings([FromQuery]string? period="month"){var id=RequireUserId();var start=DateTime.UtcNow.AddMonths(-1).Date;if(string.Equals(period,"week",StringComparison.OrdinalIgnoreCase))start=DateTime.UtcNow.AddDays(-7).Date;var bookings=await _db.Bookings.Include(b=>b.Course).Where(b=>b.TeacherId==id&&b.Status==BookingStatus.Completed&&b.EndTime>=start).ToListAsync();var rows=bookings.GroupBy(b=>b.StartTime.Date).OrderBy(g=>g.Key).Select(g=>new{date=g.Key.ToString("yyyy-MM-dd"),earnings=g.Sum(EarningsForBooking),hours=Math.Round(g.Sum(b=>(b.EndTime-b.StartTime).TotalHours),2),classes=g.Count()}).ToList();var total=rows.Sum(r=>r.earnings);var hours=rows.Sum(r=>r.hours);return Ok(new{teacherId=id,period=period??"month",totalEarnings=total,currency=bookings.Select(b=>b.Course?.Currency).FirstOrDefault(c=>!string.IsNullOrWhiteSpace(c))??"IRR",completedClasses=bookings.Count,availableBalance=await TeacherWithdrawalLedger.GetAvailableBalanceAsync(_db,id),totalHours=hours,hourlyRate=hours==0?0:Math.Round(total/(decimal)hours,2),breakdown=rows,groupClassEarningsIncluded=false,groupClassEarningsNote="Group-class income is excluded until payment processing and teacher commission rules are configured."});}
        [HttpGet("progression")]
    public async Task<IActionResult> Progression([FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var teacherId = RequireUserId();
        var assignedClassIds = await _db.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.Status == "confirmed")
            .Select(x => x.ClassSlotId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var classRows = await _db.Set<CourseClass>().AsNoTracking()
            .Where(x => assignedClassIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CourseId, x.Status })
            .ToListAsync(cancellationToken);

        var countedClasses = classRows.Where(x => !string.Equals(x.Status, "cancelled", StringComparison.OrdinalIgnoreCase)).ToList();
        var completedClasses = countedClasses.Count(x => string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase));
        var classCompletionRate = countedClasses.Count == 0
            ? 0d
            : Math.Round(completedClasses * 100d / countedClasses.Count, 2);

        var courseIds = await _db.Set<CourseInstanceTeacher>().AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .Select(x => x.CourseInstance.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);
        courseIds = courseIds.Concat(classRows.Select(x => x.CourseId)).Distinct().ToList();

        var averageRating = courseIds.Count == 0
            ? 0d
            : Math.Round(await _db.Reviews.AsNoTracking()
                .Where(r => courseIds.Contains(r.CourseId))
                .Select(r => (double?)r.Rating)
                .AverageAsync(cancellationToken) ?? 0d, 2);

        var fa = string.Equals(lang, "fa", StringComparison.OrdinalIgnoreCase);
        // Payout share remains null because the repository contains no authoritative commission policy.
        var tiers = new[]
        {
            new { level = 1, name = fa ? "شروع" : "Starter", minCompletedClasses = 0, minAverageRating = 0d, minClassCompletionRate = 0d, teacherSharePercent = (decimal?)null, benefits = Array.Empty<string>() },
            new { level = 2, name = fa ? "در حال رشد" : "Growing", minCompletedClasses = 25, minAverageRating = 4.6d, minClassCompletionRate = 90d, teacherSharePercent = (decimal?)null, benefits = Array.Empty<string>() },
            new { level = 3, name = fa ? "حرفه‌ای" : "Expert", minCompletedClasses = 100, minAverageRating = 4.8d, minClassCompletionRate = 95d, teacherSharePercent = (decimal?)null, benefits = Array.Empty<string>() }
        };

        var currentLevel = 1;
        foreach (var tier in tiers)
        {
            if (completedClasses >= tier.minCompletedClasses &&
                averageRating >= tier.minAverageRating &&
                classCompletionRate >= tier.minClassCompletionRate)
                currentLevel = tier.level;
        }

        return Ok(new { currentLevel, completedClasses, averageRating, classCompletionRate, tiers });
    }

[HttpGet("analytics")]
    public async Task<IActionResult> Analytics() => Ok(await BuildAnalyticsObject(RequireUserId()));
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(){var id=RequireUserId();var classes=await GetClasses(id,null,null,null);var students=await StudentsValues(id);var earnings=await EarningsValue(id);var analytics=await AnalyticsValue(id);return Ok(new{classes,students,earnings,analytics});}
    private async Task<object[]> GetClasses(Guid id, string? status, string? startDate, string? endDate)
    {
        var from = DateTime.TryParse(startDate, out var parsedStart) ? parsedStart : (DateTime?)null;
        var to = DateTime.TryParse(endDate, out var parsedEnd) ? parsedEnd.Date.AddDays(1) : (DateTime?)null;
        var now = DateTime.UtcNow;
        var items = new List<(DateTime StartsAt, object Value)>();

        var bookingQuery = _db.Bookings.AsNoTracking().Include(x => x.Course).Include(x => x.Student)
            .Where(x => x.TeacherId == id);
        if (from.HasValue) bookingQuery = bookingQuery.Where(x => x.StartTime >= from.Value);
        if (to.HasValue) bookingQuery = bookingQuery.Where(x => x.StartTime < to.Value);
        var bookings = await bookingQuery.OrderBy(x => x.StartTime).Take(100).ToListAsync();
        foreach (var booking in bookings)
        {
            var bookingStatus = StudentBookingStatus(booking.Status, booking.StartTime, booking.EndTime);
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(bookingStatus, status, StringComparison.OrdinalIgnoreCase)) continue;
            items.Add((booking.StartTime, new { id = booking.Id, courseId = booking.CourseId ?? Guid.Empty, courseTitle = booking.Course?.Title ?? string.Empty, teacherId = booking.TeacherId, subject = "", scheduledDate = booking.StartTime.ToString("yyyy-MM-dd"), startTime = booking.StartTime.ToString("HH:mm"), endTime = booking.EndTime.ToString("HH:mm"), studentId = booking.StudentId, studentName = booking.Student.FullName, studentAvatar = booking.Student.AvatarUrl, status = bookingStatus, type = "one_on_one", meetingUrl = (string?)null, notes = (string?)null }));
        }

        var slotQuery = _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.Teacher).Include(x => x.ClassSlot).ThenInclude(x => x.Course)
            .Where(x => x.TeacherId == id && x.Status == "confirmed" && x.ClassSlot.Status != "cancelled");
        if (from.HasValue) slotQuery = slotQuery.Where(x => x.ClassSlot.ScheduledDate >= from.Value.Date);
        if (to.HasValue) slotQuery = slotQuery.Where(x => x.ClassSlot.ScheduledDate < to.Value.Date);
        var assignments = await slotQuery.OrderBy(x => x.ClassSlot.ScheduledDate).ThenBy(x => x.ClassSlot.StartTime).Take(100).ToListAsync();
        var slotIds = assignments.Select(x => x.ClassSlotId).Distinct().ToArray();
        var enrollments = await _db.Set<ClassEnrollment>().AsNoTracking().Include(x => x.Student)
            .Where(x => slotIds.Contains(x.ClassId) && x.Status == "confirmed").ToListAsync();
        var enrollmentGroups = enrollments.GroupBy(x => x.ClassId).ToDictionary(x => x.Key, x => x.ToList());

        foreach (var assignment in assignments)
        {
            var slot = assignment.ClassSlot;
            var start = slot.ScheduledDate.Date + slot.StartTime;
            var end = slot.ScheduledDate.Date + slot.EndTime;
            var slotStatus = string.Equals(slot.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ? "cancelled"
                : string.Equals(slot.Status, "completed", StringComparison.OrdinalIgnoreCase) || end <= now ? "completed"
                : start <= now ? "ongoing" : "scheduled";
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(slotStatus, status, StringComparison.OrdinalIgnoreCase)) continue;

            if (!enrollmentGroups.TryGetValue(slot.Id, out var slotEnrollments) || slotEnrollments.Count == 0)
            {
                items.Add((start, new { id = slot.Id, classId = slot.Id, assignmentId = assignment.Id, courseId = slot.CourseId, courseTitle = slot.Course.Title, teacherId = assignment.TeacherId, subject = slot.Subject, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm"), studentId = (Guid?)null, studentName = (string?)null, studentAvatar = (string?)null, status = slotStatus, type = "group_class", meetingUrl = slot.MeetingUrl, notes = slot.Description, capacity = slot.MaxCapacity, currentEnrollment = 0, currency = slot.Currency, finalPrice = slot.FinalPrice, timeZone = slot.TimeZone }));
                continue;
            }

            foreach (var enrollment in slotEnrollments)
                items.Add((start, new { id = enrollment.Id, classId = slot.Id, assignmentId = assignment.Id, courseId = slot.CourseId, courseTitle = slot.Course.Title, teacherId = assignment.TeacherId, subject = slot.Subject, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm"), studentId = (Guid?)enrollment.StudentId, studentName = (string?)enrollment.Student.FullName, studentAvatar = (string?)enrollment.Student.AvatarUrl, status = slotStatus, type = "group_class", meetingUrl = slot.MeetingUrl, notes = slot.Description, capacity = slot.MaxCapacity, currentEnrollment = slotEnrollments.Count, currency = slot.Currency, finalPrice = slot.FinalPrice, timeZone = slot.TimeZone }));
        }

        return items.OrderBy(x => x.StartsAt).Take(100).Select(x => x.Value).ToArray();
    }
    private async Task<object[]> StudentsValues(Guid id)
    {
        var result = new List<object>();
        var legacyCourses = await _db.Set<CourseInstanceTeacher>().AsNoTracking()
            .Where(x => x.TeacherId == id).Select(x => x.CourseInstance.CourseId)
            .Distinct().ToListAsync();
        var legacyEnrollments = await _db.Set<Enrollment>().AsNoTracking()
            .Include(x => x.Student).Include(x => x.Course)
            .Where(x => legacyCourses.Contains(x.CourseId)).Take(200).ToListAsync();

        foreach (var enrollment in legacyEnrollments)
        {
            var total = await _db.Lessons.CountAsync(x => x.CourseModule.CourseId == enrollment.CourseId);
            var done = await _db.LessonProgress.CountAsync(x => x.StudentId == enrollment.StudentId && x.IsCompleted && x.Lesson.CourseModule.CourseId == enrollment.CourseId);
            result.Add(new { studentId = enrollment.StudentId, studentName = enrollment.Student.FullName, studentAvatar = enrollment.Student.AvatarUrl, studentEmail = enrollment.Student.Email, courseId = enrollment.CourseId, courseTitle = enrollment.Course.Title, enrolledAt = enrollment.EnrolledAt, progress = total == 0 ? 0 : Math.Round(done * 100d / total, 2), completedLessons = done, totalLessons = total, lastActivity = enrollment.EnrolledAt, upcomingClass = (object?)null });
        }

        var assignedSlots = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Where(x => x.TeacherId == id && x.Status == "confirmed")
            .Select(x => new { x.Id, x.ClassSlotId, x.ClassSlot.CourseId })
            .ToListAsync();
        var slotIds = assignedSlots.Select(x => x.ClassSlotId).Distinct().ToArray();
        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.Student).Include(x => x.CourseClass).ThenInclude(x => x.Course)
            .Where(x => slotIds.Contains(x.ClassId) && x.Status == "confirmed")
            .Take(500).ToListAsync();

        foreach (var enrollment in classEnrollments)
        {
            var slot = enrollment.CourseClass;
            var total = await _db.Lessons.CountAsync(x => x.CourseModule.CourseId == slot.CourseId);
            var done = await _db.LessonProgress.CountAsync(x => x.StudentId == enrollment.StudentId && x.IsCompleted && x.Lesson.CourseModule.CourseId == slot.CourseId);
            var lastActivity = await _db.LessonProgress.Where(x => x.StudentId == enrollment.StudentId && x.Lesson.CourseModule.CourseId == slot.CourseId && x.LastAccessedAt != null)
                .OrderByDescending(x => x.LastAccessedAt).Select(x => x.LastAccessedAt).FirstOrDefaultAsync();
            var upcoming = slot.ScheduledDate.Date + slot.EndTime >= DateTime.UtcNow
                ? new { id = slot.Id, scheduledDate = slot.ScheduledDate.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm") }
                : null;
            var assignmentId = assignedSlots.First(x => x.ClassSlotId == slot.Id).Id;
            result.Add(new { studentId = enrollment.StudentId, studentName = enrollment.Student.FullName, studentAvatar = enrollment.Student.AvatarUrl, studentEmail = enrollment.Student.Email, courseId = slot.CourseId, courseTitle = slot.Course.Title, enrolledAt = enrollment.EnrolledAt, progress = total == 0 ? 0 : Math.Round(done * 100d / total, 2), completedLessons = done, totalLessons = total, lastActivity = lastActivity ?? enrollment.EnrolledAt, upcomingClass = upcoming, classId = slot.Id, assignmentId, classTitle = slot.Title });
        }

        return result.ToArray();
    }
    private async Task<object> EarningsValue(Guid id)=>await BuildEarningsObject(id);
    private async Task<object> AnalyticsValue(Guid id){return await BuildAnalyticsObject(id);}
    private async Task<object> BuildEarningsObject(Guid id){var bs=await _db.Bookings.Include(b=>b.Course).Where(b=>b.TeacherId==id&&b.Status==BookingStatus.Completed&&b.StartTime>=DateTime.UtcNow.AddMonths(-1)).ToListAsync();var hours=bs.Sum(b=>(b.EndTime-b.StartTime).TotalHours);return new{teacherId=id,period="month",totalEarnings=bs.Sum(EarningsForBooking),currency=bs.Select(b=>b.Course?.Currency).FirstOrDefault(c=>!string.IsNullOrWhiteSpace(c))??"IRR",completedClasses=bs.Count,availableBalance=await TeacherWithdrawalLedger.GetAvailableBalanceAsync(_db,id),totalHours=Math.Round(hours,2),hourlyRate=hours==0?0:Math.Round(bs.Sum(EarningsForBooking)/(decimal)hours,2),breakdown=Array.Empty<object>(),groupClassEarningsIncluded=false,groupClassEarningsNote="Group-class income is excluded until payment processing and teacher commission rules are configured."};}
    private async Task<object> BuildAnalyticsObject(Guid id)
    {
        var legacyCourses = await _db.Set<CourseInstanceTeacher>().AsNoTracking()
            .Where(x => x.TeacherId == id).Select(x => x.CourseInstance.CourseId).Distinct().ToListAsync();
        var assignedSlots = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.ClassSlot)
            .Where(x => x.TeacherId == id && x.Status == "confirmed").ToListAsync();
        var groupClasses = assignedSlots.Select(x => x.ClassSlot).DistinctBy(x => x.Id).ToList();
        var groupClassIds = groupClasses.Select(x => x.Id).ToArray();
        var classEnrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Where(x => groupClassIds.Contains(x.ClassId) && x.Status == "confirmed")
            .Select(x => new { x.StudentId, x.ClassId, CourseId = x.CourseClass.CourseId })
            .ToListAsync();

        var courses = legacyCourses.Concat(groupClasses.Select(x => x.CourseId)).Distinct().ToList();
        var legacyStudentIds = await _db.Set<Enrollment>().AsNoTracking()
            .Where(x => courses.Contains(x.CourseId)).Select(x => x.StudentId).Distinct().ToListAsync();
        var studentIds = legacyStudentIds.Concat(classEnrollments.Select(x => x.StudentId)).Distinct().ToList();
        var bookings = await _db.Bookings.AsNoTracking().Where(x => x.TeacherId == id).ToListAsync();
        var now = DateTime.UtcNow;
        var groupCompleted = groupClasses.Count(x => string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(x.Status, "cancelled", StringComparison.OrdinalIgnoreCase) && x.ScheduledDate.Date + x.EndTime <= now));
        var groupCancelled = groupClasses.Count(x => string.Equals(x.Status, "cancelled", StringComparison.OrdinalIgnoreCase));
        var groupUpcoming = groupClasses.Count(x => !string.Equals(x.Status, "cancelled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase) && x.ScheduledDate.Date + x.StartTime > now);
        var completed = bookings.Count(x => x.Status == BookingStatus.Completed) + groupCompleted;
        var cancelled = bookings.Count(x => x.Status == BookingStatus.Cancelled) + groupCancelled;
        var upcoming = bookings.Count(x => x.StartTime > now && x.Status != BookingStatus.Cancelled) + groupUpcoming;
        var rating = courses.Count == 0 ? 0d : await _db.Reviews.AsNoTracking()
            .Where(x => courses.Contains(x.CourseId)).Select(x => (double?)x.Rating).AverageAsync() ?? 0d;

        var progressValues = new List<double>();
        foreach (var courseId in courses)
        {
            var studentsForCourse = (await _db.Set<Enrollment>().AsNoTracking()
                .Where(x => x.CourseId == courseId).Select(x => x.StudentId).Distinct().ToListAsync())
                .Concat(classEnrollments.Where(x => x.CourseId == courseId).Select(x => x.StudentId)).Distinct().ToList();
            var totalLessons = await _db.Lessons.CountAsync(x => x.CourseModule.CourseId == courseId);
            if (totalLessons == 0)
            {
                progressValues.AddRange(studentsForCourse.Select(_ => 0d));
                continue;
            }
            foreach (var studentId in studentsForCourse)
            {
                var done = await _db.LessonProgress.CountAsync(x => x.StudentId == studentId && x.IsCompleted && x.Lesson.CourseModule.CourseId == courseId);
                progressValues.Add(done * 100d / totalLessons);
            }
        }

        var currentStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastStart = currentStart.AddMonths(-1);
        var currentBookings = bookings.Where(x => x.Status == BookingStatus.Completed && x.StartTime >= currentStart).ToList();
        var lastBookings = bookings.Where(x => x.Status == BookingStatus.Completed && x.StartTime >= lastStart && x.StartTime < currentStart).ToList();
        var currentEarnings = currentBookings.Sum(EarningsForBooking);
        var lastEarnings = lastBookings.Sum(EarningsForBooking);
        var growth = lastEarnings == 0 ? 0 : Math.Round((double)((currentEarnings - lastEarnings) * 100m / lastEarnings), 2);

        var weekly = Enumerable.Range(0, 7).Select(index =>
        {
            var day = now.Date.AddDays(-index);
            var dayBookings = bookings.Where(x => x.StartTime.Date == day).ToList();
            var daySlots = groupClasses.Where(x => x.ScheduledDate.Date == day).ToList();
            var hours = dayBookings.Sum(x => (x.EndTime - x.StartTime).TotalHours) + daySlots.Sum(x => (x.EndTime - x.StartTime).TotalHours);
            return new { day = day.ToString("ddd"), classes = dayBookings.Count + daySlots.Count, hours = Math.Round(hours, 2) };
        }).Reverse().ToList();

        var monthly = Enumerable.Range(0, 6).Select(index =>
        {
            var month = currentStart.AddMonths(-5 + index);
            var monthBookings = bookings.Where(x => x.Status == BookingStatus.Completed && x.StartTime.Year == month.Year && x.StartTime.Month == month.Month).ToList();
            var monthSlots = groupClasses.Where(x => x.ScheduledDate.Year == month.Year && x.ScheduledDate.Month == month.Month &&
                (string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase) || x.ScheduledDate.Date + x.EndTime <= now)).ToList();
            return new { month = month.ToString("yyyy-MM"), earnings = monthBookings.Sum(EarningsForBooking), classes = monthBookings.Count + monthSlots.Count, students = monthBookings.Select(x => x.StudentId).Concat(classEnrollments.Where(x => monthSlots.Any(slot => slot.Id == x.ClassId)).Select(x => x.StudentId)).Distinct().Count() };
        }).ToList();

        var totalClassCount = bookings.Count + groupClasses.Count;
        var totalHours = bookings.Where(x => x.Status == BookingStatus.Completed).Sum(x => (x.EndTime - x.StartTime).TotalHours) +
            groupClasses.Where(x => string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase) || x.ScheduledDate.Date + x.EndTime <= now).Sum(x => (x.EndTime - x.StartTime).TotalHours);

        return new
        {
            totalStudents = studentIds.Count,
            activeStudents = studentIds.Count,
            totalClasses = totalClassCount,
            completedClasses = completed,
            cancelledClasses = cancelled,
            averageRating = Math.Round(rating, 2),
            totalHoursTaught = Math.Round(totalHours, 2),
            currentMonthEarnings = currentEarnings,
            lastMonthEarnings = lastEarnings,
            earningsGrowth = growth,
            studentProgressRate = progressValues.Count == 0 ? 0 : Math.Round(progressValues.Average(), 2),
            classCompletionRate = totalClassCount == 0 ? 0 : Math.Round(completed * 100d / totalClassCount, 2),
            upcomingClasses = upcoming,
            weeklySchedule = weekly,
            monthlyTrends = monthly,
            groupClassEarningsIncluded = false,
            groupClassEarningsNote = "Group-class revenue is excluded until payment processing and teacher commission rules are configured."
        };
    }
    private static decimal EarningsForBooking(Booking b)=>b.Course?.Price??0m;
    private static string StudentBookingStatus(BookingStatus s,DateTime start,DateTime end)=>s==BookingStatus.Cancelled?"cancelled":end<=DateTime.UtcNow?"completed":start<=DateTime.UtcNow?"ongoing":"scheduled";
}
