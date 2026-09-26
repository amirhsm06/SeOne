using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/dashboard")]
[Authorize(Roles = "Student")]
public class StudentDashboardCompatibilityController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    public StudentDashboardCompatibilityController(SeOneDbContext db) => _db = db;

    [HttpGet("upcoming-classes")]
    public async Task<IActionResult> Upcoming([FromQuery]int limit=10)
    {
        var userId=RequireUserId(); var now=DateTime.UtcNow; var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Include(b=>b.Teacher).Where(b=>b.StudentId==userId&&b.EndTime>=now).OrderBy(b=>b.StartTime).Take(Math.Clamp(limit,1,50)).ToListAsync();return Ok(await Task.WhenAll(bookings.Select(MapBookingAsync)));
    }
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery]string? startDate,[FromQuery]string? endDate,[FromQuery]string? type)
    {
        var userId=RequireUserId(); var start=ParseDate(startDate,DateTime.UtcNow.Date.AddDays(-30));var end=ParseDate(endDate,DateTime.UtcNow.Date.AddDays(60));var events=new List<object>();
        var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Where(b=>b.StudentId==userId&&b.StartTime<end.AddDays(1)&&b.EndTime>=start).ToListAsync();events.AddRange(bookings.Select(b=>new{ id=b.Id,title=b.Course?.Title??"Class",description="Teacher session",start=b.StartTime,end=b.EndTime,type="class",courseId=b.CourseId,courseTitle=b.Course?.Title,color=(string?)null }));
        var courseIds=await _db.Set<Enrollment>().Where(e=>e.StudentId==userId).Select(e=>e.CourseId).ToListAsync();var assignments=await _db.Set<Assignment>().Where(a=>courseIds.Contains(a.CourseId)&&a.DueDate>=start&&a.DueDate<end&&a.IsPublished).ToListAsync();events.AddRange(assignments.Select(a=>new{id=a.Id,title=a.Title,description=a.Description,start=a.DueDate.AddHours(-1),end=a.DueDate,type="assignment_due",courseId=(Guid?)a.CourseId,courseTitle=(string?)null,color=(string?)null}));
        var practices=await _db.Set<Practice>().Where(p=>courseIds.Contains(p.CourseId)&&p.IsPublished).ToListAsync();foreach(var p in practices){events.Add(new{id=p.Id,title=p.Title,description=p.Description,start=p.CreatedAt,end=p.CreatedAt.AddMinutes(p.TimeLimit??15),type="practice",courseId=(Guid?)p.CourseId,courseTitle=(string?)null,color=(string?)null});}
        if(!string.IsNullOrWhiteSpace(type))events=events.Where(e=>e.GetType().GetProperty("type")?.GetValue(e)?.ToString()==type).ToList();return Ok(events.OrderBy(e=>(DateTime)(e.GetType().GetProperty("start")?.GetValue(e)??DateTime.MinValue)));
    }
    [HttpGet("recent-activity")]
    public async Task<IActionResult> Recent([FromQuery]int limit=10)
    {
        var userId=RequireUserId();var all=new List<(DateTime date,object value)>();
        var lessons=await _db.LessonProgress.AsNoTracking().Include(p=>p.Lesson).ThenInclude(l=>l.CourseModule).Where(p=>p.StudentId==userId&&p.IsCompleted&&p.CompletedAt!=null).OrderByDescending(p=>p.CompletedAt).Take(limit).ToListAsync();foreach(var p in lessons)all.Add((p.CompletedAt!.Value,new{id=$"lesson-{p.LessonId}",type="lesson_completed",title=$"Completed {p.Lesson.Title}",description="Lesson completed",courseId=(Guid?)p.Lesson.CourseModule.CourseId,courseTitle=(string?)null,timestamp=p.CompletedAt.Value,metadata=new{lessonId=p.LessonId}}));
        var enroll=await _db.Set<Enrollment>().AsNoTracking().Include(e=>e.Course).Where(e=>e.StudentId==userId).OrderByDescending(e=>e.EnrolledAt).Take(limit).ToListAsync();foreach(var e in enroll)all.Add((e.EnrolledAt,new{id=$"enrollment-{e.Id}",type="course_enrolled",title=$"Enrolled in {e.Course.Title}",description="Course enrollment created",courseId=(Guid?)e.CourseId,courseTitle=(string?)e.Course.Title,timestamp=e.EnrolledAt,metadata=(object?)null}));
        var submissions=await _db.AssignmentSubmissions.AsNoTracking().Include(s=>s.Assignment).Where(s=>s.StudentId==userId).OrderByDescending(s=>s.SubmittedAt).Take(limit).ToListAsync();foreach(var s in submissions)all.Add((s.SubmittedAt,new{id=$"assignment-{s.Id}",type="assignment_submitted",title=$"Submitted {s.Assignment.Title}",description="Assignment submitted",courseId=(Guid?)s.Assignment.CourseId,courseTitle=(string?)null,timestamp=s.SubmittedAt,metadata=new{submissionId=s.Id}}));
        var attempts=await _db.PracticeAttempts.AsNoTracking().Include(a=>a.Practice).Where(a=>a.StudentId==userId&&a.SubmittedAt!=null).OrderByDescending(a=>a.SubmittedAt).Take(limit).ToListAsync();foreach(var a in attempts)all.Add((a.SubmittedAt!.Value,new{id=$"practice-{a.Id}",type="practice_completed",title=$"Completed {a.Practice.Title}",description=$"Score {a.Score}/{a.MaxScore}",courseId=(Guid?)a.Practice.CourseId,courseTitle=(string?)null,timestamp=a.SubmittedAt.Value,metadata=new{attemptId=a.Id,score=a.Score,maxScore=a.MaxScore}}));
        var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Where(b=>b.StudentId==userId).OrderByDescending(b=>b.CreatedAt).Take(limit).ToListAsync();foreach(var b in bookings)all.Add((b.CreatedAt,new{id=$"booking-{b.Id}",type="booking_created",title="Teacher booking created",description=b.Course?.Title??"Booking",courseId=b.CourseId,courseTitle=b.Course?.Title,timestamp=b.CreatedAt,metadata=new{bookingId=b.Id}}));
        return Ok(all.OrderByDescending(x=>x.date).Take(Math.Clamp(limit,1,50)).Select(x=>x.value));
    }
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics()=>Ok(await BuildStatisticsAsync(RequireUserId()));
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(){var id=RequireUserId();return Ok(new{upcomingClasses=await UpcomingValues(id),recentActivity=await RecentValues(id),statistics=await BuildStatisticsAsync(id)});}

    private async Task<object[]> UpcomingValues(Guid id){var bookings=await _db.Bookings.AsNoTracking().Include(b=>b.Course).Where(b=>b.StudentId==id&&b.EndTime>=DateTime.UtcNow).OrderBy(b=>b.StartTime).Take(10).ToListAsync();return await Task.WhenAll(bookings.Select(MapBookingAsync));}
    private async Task<object[]> RecentValues(Guid id){var lessons=await _db.LessonProgress.AsNoTracking().Where(p=>p.StudentId==id&&p.IsCompleted&&p.CompletedAt!=null).OrderByDescending(p=>p.CompletedAt).Take(5).Select(p=>new{p.LessonId,p.CompletedAt}).ToListAsync();return lessons.Select(x=>(object)new{id=$"lesson-{x.LessonId}",type="lesson_completed",title="Lesson completed",description="Lesson completed",timestamp=x.CompletedAt}).ToArray();}
    private async Task<object> BuildStatisticsAsync(Guid userId)
    {
        var today=DateTime.UtcNow.Date;var week=today.AddDays(-6);var month=today.AddDays(-29);
        var sessions=await _db.LearningSessions.AsNoTracking().Where(s=>s.StudentId==userId&&s.EndedAt!=null&&s.StartedAt>=month).Select(s=>new{s.StartedAt,s.EndedAt}).ToListAsync();
        var total=await _db.LessonProgress.Where(p=>p.StudentId==userId).SumAsync(p=>(long?)p.TimeSpent)??0;var todayTime=sessions.Where(s=>s.StartedAt>=today).Sum(s=>Duration(s.StartedAt,s.EndedAt));var weekTime=sessions.Where(s=>s.StartedAt>=week).Sum(s=>Duration(s.StartedAt,s.EndedAt));var monthTime=sessions.Sum(s=>Duration(s.StartedAt,s.EndedAt));
        var enrolled=await _db.Set<Enrollment>().Where(e=>e.StudentId==userId).Select(e=>e.CourseId).ToListAsync();var lessonsCompleted=await _db.LessonProgress.CountAsync(p=>p.StudentId==userId&&p.IsCompleted);var coursesCompleted=0;foreach(var c in enrolled){var t=await _db.Lessons.CountAsync(l=>l.CourseModule.CourseId==c);var d=await _db.LessonProgress.CountAsync(p=>p.StudentId==userId&&p.IsCompleted&&p.Lesson.CourseModule.CourseId==c);if(t>0&&t==d)coursesCompleted++;}
        var attempts=await _db.PracticeAttempts.Where(a=>a.StudentId==userId&&a.SubmittedAt!=null&&a.MaxScore>0).ToListAsync();var avg=attempts.Count==0?0:Math.Round(attempts.Average(a=>a.Score*100d/a.MaxScore),2);var activity=new List<object>();for(var i=6;i>=0;i--){var day=today.AddDays(-i);activity.Add(new{date=day.ToString("yyyy-MM-dd"),minutes=sessions.Where(s=>s.StartedAt.Date==day).Sum(s=>Duration(s.StartedAt,s.EndedAt))});}
        var currentStreak=await Streak(userId,false);var longest=await Streak(userId,true);return new{totalStudyTime=(int)Math.Min(int.MaxValue,Math.Max(total,monthTime)),todayStudyTime=todayTime,weekStudyTime=weekTime,monthStudyTime=monthTime,dailyGoal=30,dailyGoalProgress=Math.Min(100,Math.Round(todayTime*100d/30d,2)),currentStreak,longestStreak=longest,lessonsCompleted,coursesInProgress=Math.Max(0,enrolled.Count-coursesCompleted),coursesCompleted,averageScore=avg,weeklyActivity=activity,activityByType=new{lessons=lessonsCompleted,practice=attempts.Count,assignments=await _db.AssignmentSubmissions.CountAsync(s=>s.StudentId==userId),classes=await _db.Bookings.CountAsync(b=>b.StudentId==userId&&b.Status==BookingStatus.Completed)}};
    }
    private async Task<object> MapBookingAsync(Booking b)=>new{id=b.Id,courseId=b.CourseId??Guid.Empty,courseTitle=b.Course?.Title??string.Empty,teacherId=b.TeacherId,teacherName=b.Teacher.FullName,teacherAvatar=b.Teacher.AvatarUrl,subject=await _db.Set<TeacherProfile>().Where(p=>p.TeacherId==b.TeacherId).Select(p=>p.Subject).FirstOrDefaultAsync()??string.Empty,scheduledDate=b.StartTime.ToString("yyyy-MM-dd"),startTime=b.StartTime.ToString("HH:mm"),endTime=b.EndTime.ToString("HH:mm"),location=(string?)null,meetingUrl=(string?)null,status=BookingStatusName(b.Status,b.StartTime,b.EndTime),type="one_on_one"};
    private static string BookingStatusName(BookingStatus status,DateTime start,DateTime end)=>status==BookingStatus.Cancelled?"cancelled":end<=DateTime.UtcNow?"completed":start<=DateTime.UtcNow?"ongoing":"scheduled";
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
    public async Task<IActionResult> Students([FromQuery]Guid? courseId,[FromQuery]bool activeOnly=false)
    {
        var teacherId=RequireUserId();var courses=await _db.Set<Course>().Where(c=>c.TeacherId==teacherId&&(courseId==null||c.Id==courseId)).ToListAsync();var ids=courses.Select(c=>c.Id).ToList();var es=await _db.Set<Enrollment>().Include(e=>e.Student).Include(e=>e.Course).Where(e=>ids.Contains(e.CourseId)).ToListAsync();var result=new List<object>();foreach(var e in es){var total=await _db.Lessons.CountAsync(l=>l.CourseModule.CourseId==e.CourseId);var done=await _db.LessonProgress.CountAsync(p=>p.StudentId==e.StudentId&&p.IsCompleted&&p.Lesson.CourseModule.CourseId==e.CourseId);var last=await _db.LessonProgress.Where(p=>p.StudentId==e.StudentId&&p.Lesson.CourseModule.CourseId==e.CourseId&&p.LastAccessedAt!=null).OrderByDescending(p=>p.LastAccessedAt).Select(p=>p.LastAccessedAt).FirstOrDefaultAsync();var upcoming=await _db.Bookings.Where(b=>b.TeacherId==teacherId&&b.StudentId==e.StudentId&&b.CourseId==e.CourseId&&b.StartTime>DateTime.UtcNow&&b.Status!=BookingStatus.Cancelled).OrderBy(b=>b.StartTime).FirstOrDefaultAsync();if(activeOnly&&upcoming==null&&last==null)continue;result.Add(new{studentId=e.StudentId,studentName=e.Student.FullName,studentAvatar=e.Student.AvatarUrl,studentEmail=e.Student.Email,courseId=e.CourseId,courseTitle=e.Course.Title,enrolledAt=e.EnrolledAt,progress=total==0?0:Math.Round(done*100d/total,2),completedLessons=done,totalLessons=total,lastActivity=last??e.EnrolledAt,upcomingClass=upcoming==null?null:new{id=upcoming.Id,scheduledDate=upcoming.StartTime.ToString("yyyy-MM-dd"),startTime=upcoming.StartTime.ToString("HH:mm")}});}return Ok(result);
    }
    [HttpGet("earnings")]
    public async Task<IActionResult> Earnings([FromQuery]string? period="month"){var id=RequireUserId();var start=DateTime.UtcNow.AddMonths(-1).Date;if(string.Equals(period,"week",StringComparison.OrdinalIgnoreCase))start=DateTime.UtcNow.AddDays(-7).Date;var bookings=await _db.Bookings.Include(b=>b.Course).Where(b=>b.TeacherId==id&&b.Status==BookingStatus.Completed&&b.EndTime>=start).ToListAsync();var rows=bookings.GroupBy(b=>b.StartTime.Date).OrderBy(g=>g.Key).Select(g=>new{date=g.Key.ToString("yyyy-MM-dd"),earnings=g.Sum(EarningsForBooking),hours=Math.Round(g.Sum(b=>(b.EndTime-b.StartTime).TotalHours),2),classes=g.Count()}).ToList();var total=rows.Sum(r=>r.earnings);var hours=rows.Sum(r=>r.hours);return Ok(new{teacherId=id,period=period??"month",totalEarnings=total,currency=bookings.Select(b=>b.Course?.Currency).FirstOrDefault(c=>!string.IsNullOrWhiteSpace(c))??"IRR",completedClasses=bookings.Count,totalHours=hours,hourlyRate=hours==0?0:Math.Round(total/(decimal)hours,2),breakdown=rows});}
    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics() => Ok(await BuildAnalyticsObject(RequireUserId()));
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(){var id=RequireUserId();var classes=await GetClasses(id,null,null,null);var students=await StudentsValues(id);var earnings=await EarningsValue(id);var analytics=await AnalyticsValue(id);return Ok(new{classes,students,earnings,analytics});}
    private async Task<object[]> GetClasses(Guid id,string? status,string? startDate,string? endDate){var q=_db.Bookings.AsNoTracking().Include(b=>b.Course).Include(b=>b.Student).Where(b=>b.TeacherId==id).AsQueryable();if(!string.IsNullOrWhiteSpace(startDate)&&DateTime.TryParse(startDate,out var sd))q=q.Where(b=>b.StartTime>=sd);if(!string.IsNullOrWhiteSpace(endDate)&&DateTime.TryParse(endDate,out var ed))q=q.Where(b=>b.StartTime<ed.AddDays(1));var bs=await q.OrderBy(b=>b.StartTime).Take(100).ToListAsync();return bs.Select(b=>new{ id=b.Id,courseId=b.CourseId??Guid.Empty,courseTitle=b.Course?.Title??string.Empty,teacherId=b.TeacherId,subject="",scheduledDate=b.StartTime.ToString("yyyy-MM-dd"),startTime=b.StartTime.ToString("HH:mm"),endTime=b.EndTime.ToString("HH:mm"),studentId=b.StudentId,studentName=b.Student.FullName,studentAvatar=b.Student.AvatarUrl,status=StudentBookingStatus(b.Status,b.StartTime,b.EndTime),type="one_on_one",meetingUrl=(string?)null,notes=(string?)null}).Where(x=>string.IsNullOrWhiteSpace(status)||x.status==status).Cast<object>().ToArray();}
    private async Task<object[]> StudentsValues(Guid id){var courses=await _db.Set<Course>().Where(c=>c.TeacherId==id).Select(c=>c.Id).ToListAsync();var es=await _db.Set<Enrollment>().Include(e=>e.Student).Include(e=>e.Course).Where(e=>courses.Contains(e.CourseId)).Take(200).ToListAsync();var res=new List<object>();foreach(var e in es){var total=await _db.Lessons.CountAsync(l=>l.CourseModule.CourseId==e.CourseId);var done=await _db.LessonProgress.CountAsync(p=>p.StudentId==e.StudentId&&p.IsCompleted&&p.Lesson.CourseModule.CourseId==e.CourseId);res.Add(new{studentId=e.StudentId,studentName=e.Student.FullName,studentAvatar=e.Student.AvatarUrl,studentEmail=e.Student.Email,courseId=e.CourseId,courseTitle=e.Course.Title,enrolledAt=e.EnrolledAt,progress=total==0?0:Math.Round(done*100d/total,2),completedLessons=done,totalLessons=total,lastActivity=e.EnrolledAt,upcomingClass=(object?)null});}return res.ToArray();}
    private async Task<object> EarningsValue(Guid id)=>await BuildEarningsObject(id);
    private async Task<object> AnalyticsValue(Guid id){return await BuildAnalyticsObject(id);}
    private async Task<object> BuildEarningsObject(Guid id){var bs=await _db.Bookings.Include(b=>b.Course).Where(b=>b.TeacherId==id&&b.Status==BookingStatus.Completed&&b.StartTime>=DateTime.UtcNow.AddMonths(-1)).ToListAsync();var hours=bs.Sum(b=>(b.EndTime-b.StartTime).TotalHours);return new{teacherId=id,period="month",totalEarnings=bs.Sum(EarningsForBooking),currency=bs.Select(b=>b.Course?.Currency).FirstOrDefault(c=>!string.IsNullOrWhiteSpace(c))??"IRR",completedClasses=bs.Count,totalHours=Math.Round(hours,2),hourlyRate=hours==0?0:Math.Round(bs.Sum(EarningsForBooking)/(decimal)hours,2),breakdown=Array.Empty<object>()};}
    private async Task<object> BuildAnalyticsObject(Guid id)
    {
        var courses=await _db.Set<Course>().Where(c=>c.TeacherId==id).Select(c=>c.Id).ToListAsync();
        var studentIds=await _db.Set<Enrollment>().Where(e=>courses.Contains(e.CourseId)).Select(e=>e.StudentId).Distinct().ToListAsync();
        var bookings=await _db.Bookings.AsNoTracking().Where(b=>b.TeacherId==id).ToListAsync();
        var completed=bookings.Count(b=>b.Status==BookingStatus.Completed);
        var cancelled=bookings.Count(b=>b.Status==BookingStatus.Cancelled);
        var upcoming=bookings.Count(b=>b.StartTime>DateTime.UtcNow&&b.Status!=BookingStatus.Cancelled);
        var rating=await _db.Reviews.Where(r=>courses.Contains(r.CourseId)).Select(r=>(double?)r.Rating).AverageAsync()??0;
        var progressValues=new List<double>();
        foreach(var courseId in courses)
        {
            var students=await _db.Set<Enrollment>().Where(e=>e.CourseId==courseId).Select(e=>e.StudentId).Distinct().ToListAsync();
            var total=await _db.Lessons.CountAsync(l=>l.CourseModule.CourseId==courseId);
            if (total == 0)
            {
                progressValues.AddRange(students.Select(_ => 0d));
                continue;
            }
            foreach (var studentId in students)
            {
                var done=await _db.LessonProgress.CountAsync(p=>p.StudentId==studentId&&p.IsCompleted&&p.Lesson.CourseModule.CourseId==courseId);
                progressValues.Add(done*100d/total);
            }
        }
        var currentStart=new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1,0,0,0,DateTimeKind.Utc);
        var lastStart=currentStart.AddMonths(-1);
        var currentBookings=bookings.Where(b=>b.Status==BookingStatus.Completed&&b.StartTime>=currentStart).ToList();
        var lastBookings=bookings.Where(b=>b.Status==BookingStatus.Completed&&b.StartTime>=lastStart&&b.StartTime<currentStart).ToList();
        var curEarn=currentBookings.Sum(EarningsForBooking);
        var lastEarn=lastBookings.Sum(EarningsForBooking);
        var growth=lastEarn==0?0:Math.Round((double)((curEarn-lastEarn)*100m/lastEarn),2);
        var weekly=Enumerable.Range(0,7).Select(i=>{var day=DateTime.UtcNow.Date.AddDays(-i);var dayBookings=bookings.Where(b=>b.StartTime.Date==day);return new{day=day.ToString("ddd"),classes=dayBookings.Count(),hours=Math.Round(dayBookings.Sum(b=>(b.EndTime-b.StartTime).TotalHours),2)};}).Reverse().ToList();
        var monthly=Enumerable.Range(0,6).Select(i=>{var month=currentStart.AddMonths(-5+i);var monthBookings=bookings.Where(b=>b.Status==BookingStatus.Completed&&b.StartTime.Year==month.Year&&b.StartTime.Month==month.Month).ToList();return new{month=month.ToString("yyyy-MM"),earnings=monthBookings.Sum(EarningsForBooking),classes=monthBookings.Count,students=monthBookings.Select(b=>b.StudentId).Distinct().Count()};}).ToList();
        return new{totalStudents=studentIds.Count,activeStudents=studentIds.Count,totalClasses=bookings.Count,completedClasses=completed,cancelledClasses=cancelled,averageRating=Math.Round(rating,2),totalHoursTaught=Math.Round(bookings.Where(b=>b.Status==BookingStatus.Completed).Sum(b=>(b.EndTime-b.StartTime).TotalHours),2),currentMonthEarnings=curEarn,lastMonthEarnings=lastEarn,earningsGrowth=growth,studentProgressRate=progressValues.Count==0?0:Math.Round(progressValues.Average(),2),classCompletionRate=bookings.Count==0?0:Math.Round(completed*100d/bookings.Count,2),upcomingClasses=upcoming,weeklySchedule=weekly,monthlyTrends=monthly};
    }
    private static decimal EarningsForBooking(Booking b)=>b.Course?.Price??0m;
    private static string StudentBookingStatus(BookingStatus s,DateTime start,DateTime end)=>s==BookingStatus.Cancelled?"cancelled":end<=DateTime.UtcNow?"completed":start<=DateTime.UtcNow?"ongoing":"scheduled";
}
