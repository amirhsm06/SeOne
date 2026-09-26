using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly UserManager<User> _users;
    private readonly IWebHostEnvironment _env;
    public AdminController(SeOneDbContext db, UserManager<User> users, IWebHostEnvironment env) { _db=db; _users=users; _env=env; }

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> DashboardStats() => Ok(new {
        studentsCount = await _db.Users.CountAsync(u=>u.Role==UserRole.Student && u.AccountStatus!="deleted"),
        teachersCount = await _db.Users.CountAsync(u=>u.Role==UserRole.Teacher && u.AccountStatus!="deleted"),
        coursesCount = await _db.Set<Course>().CountAsync(),
        activeDiscountsCount = await _db.Set<Course>().CountAsync(c=>c.DiscountPercent>0 && c.IsPublished),
        publishedBlogPostsCount = await _db.BlogPosts.CountAsync(b=>b.IsPublished),
        activeEnrollmentsCount = await _db.Set<Enrollment>().CountAsync(),
        pendingTeachersCount = await _db.Users.CountAsync(u=>u.Role==UserRole.Teacher && u.AccountStatus=="pending")
    });

    [HttpGet("students")]
    public async Task<IActionResult> Students([FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,[FromQuery]string? status=null)
    {
        var q=_db.Users.AsNoTracking().Where(u=>u.Role==UserRole.Student && u.AccountStatus!="deleted"); if(!string.IsNullOrWhiteSpace(search))q=q.Where(u=>u.FullName.Contains(search)||u.Email!.Contains(search));if(!string.IsNullOrWhiteSpace(status))q=q.Where(u=>u.AccountStatus==status);return Ok(await PagedUsers(q,page,pageSize, false));
    }
    [HttpGet("students/{id:guid}")]
    public Task<IActionResult> Student(Guid id)=>GetUser(id,UserRole.Student);
    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent([FromBody] AdminCreateUserRequest r){return await CreateUser(r,UserRole.Student,"active");}
    [HttpPatch("students/{id:guid}")]
    public Task<IActionResult> UpdateStudent(Guid id,[FromBody] JsonElement body)=>UpdateUser(id,UserRole.Student,body);
    [HttpPatch("students/{id:guid}/status")]
    public Task<IActionResult> StudentStatus(Guid id,[FromBody] StatusRequest r)=>SetStatus(id,UserRole.Student,new[]{"active","suspended","banned"},r.Status);
    [HttpDelete("students/{id:guid}")]
    public Task<IActionResult> DeleteStudent(Guid id)=>SoftDelete(id,UserRole.Student);
    [HttpGet("students/{id:guid}/enrollments")]
    public Task<IActionResult> StudentEnrollments(Guid id)=>GetStudentEnrollments(id);
    [HttpGet("students/{id:guid}/progress")]
    public Task<IActionResult> StudentProgress(Guid id)=>GetStudentProgress(id);
    [HttpPost("students/{id:guid}/enrollments")]
    public Task<IActionResult> AdminEnrollStudent(Guid id,[FromBody] AdminEnrollRequest r)=>EnrollStudent(id,r.CourseId);
    [HttpDelete("students/{studentId:guid}/enrollments/{enrollmentId:guid}")]
    public async Task<IActionResult> CancelStudentEnrollment(Guid studentId,Guid enrollmentId){var e=await _db.Set<Enrollment>().FirstOrDefaultAsync(e=>e.Id==enrollmentId&&e.StudentId==studentId);if(e is null)return NotFound();await _db.Set<LearningSession>().Where(s=>s.EnrollmentId==e.Id).ExecuteDeleteAsync();_db.Remove(e);await _db.SaveChangesAsync();return NoContent();}

    [HttpGet("teachers")]
    public async Task<IActionResult> Teachers([FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,[FromQuery]string? status=null)
    {
        var q=_db.Users.AsNoTracking().Where(u=>u.Role==UserRole.Teacher && u.AccountStatus!="deleted"); if(!string.IsNullOrWhiteSpace(search))q=q.Where(u=>u.FullName.Contains(search)||u.Email!.Contains(search));if(!string.IsNullOrWhiteSpace(status))q=q.Where(u=>u.AccountStatus==status);return Ok(await PagedTeachers(q,page,pageSize));
    }
    [HttpGet("teachers/{id:guid}")]
    public async Task<IActionResult> Teacher(Guid id){var u=await _db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==id&&u.Role==UserRole.Teacher);if(u is null)return NotFound();var p=await _db.Set<TeacherProfile>().AsNoTracking().FirstOrDefaultAsync(p=>p.TeacherId==id);return Ok(MapTeacher(u,p));}
    [HttpPost("teachers")]
    public async Task<IActionResult> CreateTeacher([FromBody] JsonElement body)
    {
        var r = ReadCreateUser(body);

        var result = await CreateUser(
            new AdminCreateUserRequest
            {
                FirstName = r.firstName,
                FamilyName = r.familyName,
                Email = r.email,
                Password = r.password,
                SendWelcomeEmail = r.sendWelcome
            },
            UserRole.Teacher,
            "pending");

        return result;
    }
    [HttpPatch("teachers/{id:guid}")]
    public async Task<IActionResult> UpdateTeacher(Guid id,[FromBody] JsonElement body)
    {
        var u = await _users.FindByIdAsync(id.ToString());
        if (u is null || u.Role != UserRole.Teacher) return NotFound();
        SetIfString(body, "firstName", v => u.FirstName = v.Trim());
        SetIfString(body, "familyName", v => u.FamilyName = v.Trim());

        u.FullName = $"{u.FirstName} {u.FamilyName}".Trim();
        SetIfString(body, "avatarUrl", v => u.AvatarUrl = v);
        SetIfString(body, "bio", v => u.Bio = v);
        var profile = await _db.Set<TeacherProfile>().FirstOrDefaultAsync(p => p.TeacherId == id);
        if (profile is null) { profile = new TeacherProfile { Id = Guid.NewGuid(), TeacherId = id }; _db.Add(profile); }
        SetIfString(body, "subject", v => profile.Subject = v);
        SetIfString(body, "level", v => profile.Level = v);
        SetIfString(body, "teachingLanguage", v => profile.TeachingLanguage = v);
        if (body.TryGetProperty("email", out var ep) && ep.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var email = ep.GetString();
            if (!string.IsNullOrWhiteSpace(email) && !string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var token = await _users.GenerateChangeEmailTokenAsync(u, email);
                var emailResult = await _users.ChangeEmailAsync(u, email, token);
                if (!emailResult.Succeeded) return BadRequest(new { message = string.Join(" ", emailResult.Errors.Select(e => e.Description)) });
            }
        }
        u.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(u);
        await _db.SaveChangesAsync();
        return Ok(MapTeacher(u, profile));
    }
    [HttpPatch("teachers/{id:guid}/status")]
    public Task<IActionResult> TeacherStatus(Guid id,[FromBody] StatusRequest r)=>SetStatus(id,UserRole.Teacher,new[]{"pending","active","suspended"},r.Status);
    [HttpDelete("teachers/{id:guid}")]
    public Task<IActionResult> DeleteTeacher(Guid id)=>SoftDelete(id,UserRole.Teacher);
    [HttpGet("teachers/{id:guid}/students")]
    public async Task<IActionResult> TeacherStudents(Guid id){if(!await _db.Users.AnyAsync(u=>u.Id==id&&u.Role==UserRole.Teacher))return NotFound();var courses=await _db.Set<Course>().Where(c=>c.TeacherId==id).Select(c=>c.Id).ToListAsync();var enrollments=await _db.Set<Enrollment>().Include(e=>e.Student).Include(e=>e.Course).Where(e=>courses.Contains(e.CourseId)).ToListAsync();var result=new List<object>();foreach(var e in enrollments){var p=await ProgressForUserCourse(e.StudentId,e.CourseId);result.Add(new{studentId=e.StudentId,fullName=e.Student.FullName,email=e.Student.Email,courseId=e.CourseId,courseTitle=e.Course.Title,progress=p.progress});}return Ok(result);}
    [HttpGet("teachers/{id:guid}/availability")]
    public async Task<IActionResult> TeacherAvailability(Guid id){var slots=await _db.Set<TeacherAvailability>().Where(s=>s.TeacherId==id).OrderBy(s=>s.DayOfWeek).ThenBy(s=>s.StartTime).Select(s=>new{id=s.Id,dayOfWeek=s.DayOfWeek.ToString(),startTime=s.StartTime.ToString(@"hh\:mm"),endTime=s.EndTime.ToString(@"hh\:mm"),isBooked=_db.Set<Booking>().Any(b=>b.TeacherId==id&&b.Status!=BookingStatus.Cancelled&&((b.StartTime.TimeOfDay>=s.StartTime&&b.StartTime.TimeOfDay<s.EndTime)||(b.EndTime.TimeOfDay>s.StartTime&&b.EndTime.TimeOfDay<=s.EndTime))) }).ToListAsync();return Ok(slots);}
    [HttpPut("teachers/{id:guid}/availability")]
    public async Task<IActionResult> SetTeacherAvailability(Guid id,[FromBody] SlotsRequest r){if(!await _db.Users.AnyAsync(u=>u.Id==id&&u.Role==UserRole.Teacher))return NotFound();var existing=await _db.Set<TeacherAvailability>().Where(a=>a.TeacherId==id).ToListAsync();_db.RemoveRange(existing);foreach(var s in r.Slots??new()){if(!Enum.TryParse<DayOfWeek>(s.DayOfWeek,true,out var day))continue;if(!TimeSpan.TryParse(s.StartTime,CultureInfo.InvariantCulture,out var start)||!TimeSpan.TryParse(s.EndTime,CultureInfo.InvariantCulture,out var end)||end<=start)continue;_db.Add(new TeacherAvailability{Id=Guid.NewGuid(),TeacherId=id,DayOfWeek=day,StartTime=start,EndTime=end,IsAvailable=true,CreatedAt=DateTime.UtcNow});}await _db.SaveChangesAsync();var slots=await _db.Set<TeacherAvailability>().Where(a=>a.TeacherId==id).OrderBy(a=>a.DayOfWeek).ThenBy(a=>a.StartTime).Select(a=>new{id=a.Id,dayOfWeek=a.DayOfWeek.ToString(),startTime=a.StartTime.ToString(@"hh\:mm"),endTime=a.EndTime.ToString(@"hh\:mm"),isBooked=false}).ToListAsync();return Ok(new{slots});}

    [HttpGet("courses")]
    public async Task<IActionResult> Courses(){var list=await _db.Set<Course>().AsNoTracking().OrderByDescending(c=>c.CreatedAt).Select(c=>new{id=c.Id,title=c.Title,level=c.Level,basePrice=c.Price,currency=c.Currency,discountPercent=c.DiscountPercent,isPublished=c.IsPublished,studentCount=_db.Set<Enrollment>().Count(e=>e.CourseId==c.Id),lessonCount=_db.Set<Lesson>().Count(l=>l.CourseModule.CourseId==c.Id)}).ToListAsync();return Ok(list);}
    [HttpPatch("courses/{id:guid}/pricing")]
    public async Task<IActionResult> CoursePricing(Guid id,[FromBody] JsonElement body){var c=await _db.Set<Course>().FindAsync(id);if(c is null)return NotFound();SetDecimal(body,"basePrice",v=>c.Price=v);SetDecimal(body,"discountPercent",v=>c.DiscountPercent=Math.Clamp(v,0,100));SetIfString(body,"currency",v=>c.Currency=v);await _db.SaveChangesAsync();return await CourseResult(c.Id);}
    [HttpPatch("courses/{id:guid}")]
    public async Task<IActionResult> UpdateCourse(Guid id,[FromBody] JsonElement body){var c=await _db.Set<Course>().FindAsync(id);if(c is null)return NotFound();SetIfString(body,"title",v=>c.Title=v);SetIfString(body,"level",v=>c.Level=v);SetIfBool(body,"isPublished",v=>c.IsPublished=v);await _db.SaveChangesAsync();return await CourseResult(c.Id);}
    [HttpGet("courses/{courseId:guid}/modules")]
    public async Task<IActionResult> CourseModules(Guid courseId){return Ok(await _db.Set<CourseModule>().Where(m=>m.CourseId==courseId).OrderBy(m=>m.Order).Select(m=>new{id=m.Id,title=m.Title,description=m.Description,order=m.Order}).ToListAsync());}
    [HttpPost("courses/{courseId:guid}/modules")]
    public async Task<IActionResult> CreateModule(Guid courseId,[FromBody] ModuleRequest r){if(!await _db.Set<Course>().AnyAsync(c=>c.Id==courseId))return NotFound();var m=new CourseModule{Id=Guid.NewGuid(),CourseId=courseId, Title = r.Title ?? "Untitled", Description=r.Description??string.Empty,Order=r.Order,CreatedAt=DateTime.UtcNow};_db.Add(m);await _db.SaveChangesAsync();return Ok(new{id=m.Id,title=m.Title,description=m.Description,order=m.Order});}
    [HttpPatch("modules/{moduleId:guid}")]
    public async Task<IActionResult> UpdateModule(Guid moduleId,[FromBody] ModuleRequest r){var m=await _db.Set<CourseModule>().FindAsync(moduleId);if(m is null)return NotFound();if(r.Title is not null)m.Title=r.Title;if(r.Description is not null)m.Description=r.Description;m.Order=r.Order;await _db.SaveChangesAsync();return Ok(new{id=m.Id,title=m.Title,description=m.Description,order=m.Order});}
    [HttpDelete("modules/{moduleId:guid}")]
    public async Task<IActionResult> DeleteModule(Guid moduleId){var m=await _db.Set<CourseModule>().FindAsync(moduleId);if(m is null)return NotFound();var lessonIds=await _db.Set<Lesson>().Where(l=>l.CourseModuleId==moduleId).Select(l=>l.Id).ToListAsync();if(lessonIds.Count>0){await _db.Set<LearningSession>().Where(s=>lessonIds.Contains(s.LessonId)).ExecuteDeleteAsync();await _db.Set<LessonProgress>().Where(p=>lessonIds.Contains(p.LessonId)).ExecuteDeleteAsync();await _db.Set<Lesson>().Where(l=>lessonIds.Contains(l.Id)).ExecuteDeleteAsync();}var assignmentIds=await _db.Set<Assignment>().Where(a=>a.CourseModuleId==moduleId).Select(a=>a.Id).ToListAsync();if(assignmentIds.Count>0){await _db.Set<AssignmentSubmission>().Where(s=>assignmentIds.Contains(s.AssignmentId)).ExecuteDeleteAsync();await _db.Set<Assignment>().Where(a=>assignmentIds.Contains(a.Id)).ExecuteDeleteAsync();}var practiceIds=await _db.Set<Practice>().Where(p=>p.CourseModuleId==moduleId).Select(p=>p.Id).ToListAsync();if(practiceIds.Count>0){var attemptIds=await _db.Set<PracticeAttempt>().Where(a=>practiceIds.Contains(a.PracticeId)).Select(a=>a.Id).ToListAsync();if(attemptIds.Count>0)await _db.Set<PracticeAttemptAnswer>().Where(a=>attemptIds.Contains(a.AttemptId)).ExecuteDeleteAsync();await _db.Set<PracticeAttempt>().Where(a=>practiceIds.Contains(a.PracticeId)).ExecuteDeleteAsync();await _db.Set<PracticeQuestion>().Where(q=>practiceIds.Contains(q.PracticeId)).ExecuteDeleteAsync();await _db.Set<Practice>().Where(p=>practiceIds.Contains(p.Id)).ExecuteDeleteAsync();}_db.Remove(m);await _db.SaveChangesAsync();return NoContent();}
    [HttpGet("modules/{moduleId:guid}/lessons")]
    public async Task<IActionResult> ModuleLessons(Guid moduleId)=>Ok(await _db.Set<Lesson>().Where(l=>l.CourseModuleId==moduleId).OrderBy(l=>l.Order).Select(l=>new{id=l.Id,title=l.Title,body=l.Content,videoUrl=l.VideoUrl,order=l.Order}).ToListAsync());
    [HttpPost("modules/{moduleId:guid}/lessons")]
    public async Task<IActionResult> CreateLesson(Guid moduleId,[FromBody] LessonRequest r){if(!await _db.Set<CourseModule>().AnyAsync(m=>m.Id==moduleId))return NotFound();var l=new Lesson{Id=Guid.NewGuid(),CourseModuleId=moduleId, Title = r.Title ?? "Untitled", Content=r.Body??string.Empty,Description=r.Body??string.Empty,VideoUrl=r.VideoUrl,Duration=r.Duration, Order = r.Order ?? 0, CreatedAt =DateTime.UtcNow};_db.Add(l);await _db.SaveChangesAsync();return Ok(new{id=l.Id,title=l.Title,body=l.Content,videoUrl=l.VideoUrl,order=l.Order});}
    [HttpPatch("lessons/{lessonId:guid}")]
    public async Task<IActionResult> UpdateLesson(Guid lessonId,[FromBody] LessonRequest r){var l=await _db.Set<Lesson>().FindAsync(lessonId);if(l is null)return NotFound();if(r.Title is not null)l.Title=r.Title;if(r.Body is not null){l.Content=r.Body;l.Description=r.Body;}if(r.VideoUrl is not null)l.VideoUrl=r.VideoUrl;if(r.Duration.HasValue)l.Duration=r.Duration;if(r.Order.HasValue)l.Order=r.Order.Value;await _db.SaveChangesAsync();return Ok(new{id=l.Id,title=l.Title,body=l.Content,videoUrl=l.VideoUrl,order=l.Order});}
    [HttpDelete("lessons/{lessonId:guid}")]
    public async Task<IActionResult> DeleteLesson(Guid lessonId){var l=await _db.Set<Lesson>().FindAsync(lessonId);if(l is null)return NotFound();await _db.Set<LearningSession>().Where(s=>s.LessonId==lessonId).ExecuteDeleteAsync();await _db.Set<LessonProgress>().Where(p=>p.LessonId==lessonId).ExecuteDeleteAsync();_db.Remove(l);await _db.SaveChangesAsync();return NoContent();}

    [HttpGet("blog/posts")]
    public async Task<IActionResult> BlogPosts([FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? search=null,[FromQuery]string? status=null){var q=_db.BlogPosts.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(search))q=q.Where(b=>b.Title.Contains(search)||b.Content.Contains(search));if(!string.IsNullOrWhiteSpace(status))q=q.Where(b=>status=="published"?b.IsPublished:!b.IsPublished);var total=await q.CountAsync();var items=await q.OrderByDescending(b=>b.CreatedAt).Skip((Math.Max(page,1)-1)*Math.Clamp(pageSize,1,100)).Take(Math.Clamp(pageSize,1,100)).ToListAsync();return Ok(new{items=items.Select(MapBlog),page=Math.Max(page,1),pageSize=Math.Clamp(pageSize,1,100),totalCount=total,totalPages=(int)Math.Ceiling(total/(double)Math.Clamp(pageSize,1,100))});}
    [HttpPost("blog/posts")]
    public async Task<IActionResult> CreateBlog([FromBody] JsonElement body){var b=new BlogPost{Id=Guid.NewGuid(),Title=GetString(body,"title")??"Untitled",Excerpt=GetString(body,"excerpt")??string.Empty,Content=GetString(body,"content")??GetString(body,"body")??string.Empty,Author=GetString(body,"author")??"SE ONE Journal",Category=GetString(body,"category")??"General",Language=GetString(body,"lang")??GetString(body,"language")??"en",ReadTime=GetString(body,"readTime")??"5 min",ImageUrl=GetString(body,"imageUrl")??GetString(body,"image"),IsPublished=GetBool(body,"isPublished")||string.Equals(GetString(body,"status"),"published",StringComparison.OrdinalIgnoreCase),CreatedAt=DateTime.UtcNow};_db.Add(b);await _db.SaveChangesAsync();return Ok(MapBlog(b));}
    [HttpPatch("blog/posts/{id:guid}")]
    public async Task<IActionResult> UpdateBlog(Guid id,[FromBody] JsonElement body){var b=await _db.BlogPosts.FindAsync(id);if(b is null)return NotFound();ApplyBlog(b,body);b.UpdatedAt=DateTime.UtcNow;await _db.SaveChangesAsync();return Ok(MapBlog(b));}
    [HttpDelete("blog/posts/{id:guid}")]
    public async Task<IActionResult> DeleteBlog(Guid id){var b=await _db.BlogPosts.FindAsync(id);if(b is null)return NotFound();_db.Remove(b);await _db.SaveChangesAsync();return NoContent();}
    [HttpPatch("blog/posts/{id:guid}/status")]
    public async Task<IActionResult> BlogStatus(Guid id,[FromBody] StatusRequest r){var b=await _db.BlogPosts.FindAsync(id);if(b is null)return NotFound();b.IsPublished=string.Equals(r.Status,"published",StringComparison.OrdinalIgnoreCase);b.UpdatedAt=DateTime.UtcNow;await _db.SaveChangesAsync();return Ok(MapBlog(b));}

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(){return Ok(await ReadSettings());}
    [HttpPatch("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] JsonElement body){var settings=await ReadSettingEntities();foreach(var (key,value) in new[]{("siteName",GetString(body,"siteName")),("supportEmail",GetString(body,"supportEmail")),("defaultLocale",GetString(body,"defaultLocale")),("maintenanceMode",GetBool(body,"maintenanceMode").ToString()),("allowRegistration",GetBool(body,"allowRegistration").ToString())}){if(value is null)continue;var e=settings.FirstOrDefault(s=>s.Key==key);if(e is null){e=new SiteSetting{Id=Guid.NewGuid(),Key=key};_db.Add(e);}e.Value=value;e.UpdatedAt=DateTime.UtcNow;}await _db.SaveChangesAsync();return Ok(await ReadSettings());}

    private async Task<IActionResult> CreateUser(
     AdminCreateUserRequest r,
     UserRole role,
     string status)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) ||
            string.IsNullOrWhiteSpace(r.FamilyName) ||
            string.IsNullOrWhiteSpace(r.Email) ||
            string.IsNullOrWhiteSpace(r.Password))
        {
            return BadRequest(new
            {
                message = "First name, family name, email and password are required."
            });
        }

        var exists = await _users.FindByEmailAsync(r.Email);

        if (exists is not null)
        {
            return Conflict(new
            {
                message = "Email is already in use."
            });
        }

        var firstName = r.FirstName.Trim();
        var familyName = r.FamilyName.Trim();

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = r.Email.Trim(),
            Email = r.Email.Trim(),
            EmailConfirmed = true,
            FirstName = firstName,
            FamilyName = familyName,
            FullName = $"{firstName} {familyName}",
            Role = role,
            AccountStatus = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _users.CreateAsync(user, r.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join(" ", result.Errors.Select(e => e.Description))
            });
        }

        if (role == UserRole.Teacher)
        {
            _db.Add(new TeacherProfile
            {
                Id = Guid.NewGuid(),
                TeacherId = user.Id,
                Rating = 0
            });
        }

        await _db.SaveChangesAsync();

        return Ok(
            role == UserRole.Student
                ? MapStudent(
                    user,
                    await _db.Set<Enrollment>()
                        .CountAsync(e => e.StudentId == user.Id))
                : MapTeacher(
                    user,
                    await _db.Set<TeacherProfile>()
                        .FirstOrDefaultAsync(p => p.TeacherId == user.Id)));
    }
    private async Task<object> PagedUsers(IQueryable<User> q,int page,int pageSize,bool teacher){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var total=await q.CountAsync();var users=await q.OrderByDescending(u=>u.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).ToListAsync();var ids=users.Select(u=>u.Id).ToList();var counts=await _db.Set<Enrollment>().Where(e=>ids.Contains(e.StudentId)).GroupBy(e=>e.StudentId).Select(g=>new{g.Key,count=g.Count()}).ToListAsync();return new{items=users.Select(u=>MapStudent(u,counts.FirstOrDefault(x=>x.Key==u.Id)?.count??0)),page,pageSize,totalCount=total,totalPages=(int)Math.Ceiling(total/(double)pageSize)};}
    private async Task<object> PagedTeachers(IQueryable<User> q,int page,int pageSize){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var total=await q.CountAsync();var users=await q.OrderByDescending(u=>u.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).ToListAsync();var ids=users.Select(u=>u.Id).ToList();var profiles=await _db.Set<TeacherProfile>().Where(p=>ids.Contains(p.TeacherId)).ToDictionaryAsync(p=>p.TeacherId);return new{items=users.Select(u=>MapTeacher(u,profiles.GetValueOrDefault(u.Id))),page,pageSize,totalCount=total,totalPages=(int)Math.Ceiling(total/(double)pageSize)};}
    private async Task<IActionResult> GetUser(Guid id,UserRole role){var u=await _db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==id&&u.Role==role);if(u is null)return NotFound();return Ok(MapStudent(u,await _db.Set<Enrollment>().CountAsync(e=>e.StudentId==id)));}
    private async Task<IActionResult> SetStatus(Guid id,UserRole role,string[] allowed,string status){status=status?.Trim().ToLowerInvariant()??"";if(!allowed.Contains(status))return BadRequest(new{message="Invalid status."});var u=await _db.Users.FirstOrDefaultAsync(u=>u.Id==id&&u.Role==role);if(u is null)return NotFound();u.AccountStatus=status;u.UpdatedAt=DateTime.UtcNow;await _users.UpdateAsync(u);return Ok(role==UserRole.Student?MapStudent(u,await _db.Set<Enrollment>().CountAsync(e=>e.StudentId==id)):MapTeacher(u,await _db.Set<TeacherProfile>().FirstOrDefaultAsync(p=>p.TeacherId==id)));}
    private async Task<IActionResult> SoftDelete(Guid id,UserRole role){var u=await _db.Users.FirstOrDefaultAsync(u=>u.Id==id&&u.Role==role);if(u is null)return NotFound();u.AccountStatus="deleted";u.UpdatedAt=DateTime.UtcNow;await _users.UpdateAsync(u);return NoContent();}
    private async Task<IActionResult> UpdateUser(
    Guid id,
    UserRole role,
    JsonElement body)
    {
        var user = await _users.FindByIdAsync(id.ToString());

        if (user is null || user.Role != role)
            return NotFound();

        SetIfString(body, "firstName", value => user.FirstName = value.Trim());
        SetIfString(body, "familyName", value => user.FamilyName = value.Trim());

        user.FullName = $"{user.FirstName} {user.FamilyName}".Trim();

        SetIfString(body, "avatarUrl", value => user.AvatarUrl = value);

        if (body.TryGetProperty("email", out var emailElement) &&
            emailElement.ValueKind == JsonValueKind.String)
        {
            var email = emailElement.GetString();

            if (!string.IsNullOrWhiteSpace(email) &&
                !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var token = await _users.GenerateChangeEmailTokenAsync(user, email);

                var emailResult = await _users.ChangeEmailAsync(
                    user,
                    email,
                    token);

                if (!emailResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = string.Join(
                            " ",
                            emailResult.Errors.Select(e => e.Description))
                    });
                }
            }
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);

        return Ok(
            MapStudent(
                user,
                await _db.Set<Enrollment>()
                    .CountAsync(x => x.StudentId == id)));
    }
    private async Task<IActionResult> EnrollStudent(Guid studentId,Guid courseId){if(!await _db.Users.AnyAsync(u=>u.Id==studentId&&u.Role==UserRole.Student))return NotFound();var course=await _db.Set<Course>().FindAsync(courseId);if(course is null)return NotFound();if(await _db.Set<Enrollment>().AnyAsync(e=>e.StudentId==studentId&&e.CourseId==courseId))return Conflict(new{message="Already enrolled."});var e=new Enrollment{Id=Guid.NewGuid(),StudentId=studentId,CourseId=courseId,EnrolledAt=DateTime.UtcNow};_db.Add(e);await _db.SaveChangesAsync();return Ok(new{enrollment=new{id=e.Id,courseId=e.CourseId,userId=e.StudentId,enrolledAt=e.EnrolledAt,status="active",progress=0}});}
    private async Task<IActionResult> GetStudentEnrollments(Guid id){var list=await _db.Set<Enrollment>().AsNoTracking().Where(e=>e.StudentId==id).Include(e=>e.Course).OrderByDescending(e=>e.EnrolledAt).ToListAsync();return Ok(await Task.WhenAll(list.Select(async e=>new{id=e.Id,courseId=e.CourseId,userId=e.StudentId,enrolledAt=e.EnrolledAt,status="active",progress=(await ProgressForUserCourse(e.StudentId,e.CourseId)).progress})));}
    private async Task<IActionResult> GetStudentProgress(Guid id){var ids=await _db.Set<Enrollment>().Where(e=>e.StudentId==id).Select(e=>e.CourseId).ToListAsync();var list=new List<object>();foreach(var courseId in ids){var c=await _db.Set<Course>().FindAsync(courseId);var p=await ProgressForUserCourse(id,courseId);list.Add(new{courseId,courseTitle=c?.Title??string.Empty,progress=p.progress,completedLessons=p.completed,totalLessons=p.total,lastActivityAt=p.last.ToString("O")});}return Ok(list);}
    private async Task<(double progress,int completed,int total,DateTime last)> ProgressForUserCourse(Guid userId,Guid courseId){var total=await _db.Set<Lesson>().CountAsync(l=>l.CourseModule.CourseId==courseId);var completed=await _db.Set<LessonProgress>().CountAsync(p=>p.StudentId==userId&&p.IsCompleted&&p.Lesson.CourseModule.CourseId==courseId);var last=await _db.Set<LessonProgress>().Where(p=>p.StudentId==userId&&p.Lesson.CourseModule.CourseId==courseId&&p.LastAccessedAt!=null).OrderByDescending(p=>p.LastAccessedAt).Select(p=>p.LastAccessedAt!.Value).FirstOrDefaultAsync();return(total==0?0:Math.Round(completed*100d/total,2),completed,total,last);}
    private async Task<Dictionary<string,string>> ReadSettings(){var entities=await ReadSettingEntities();var map=entities.ToDictionary(s=>s.Key,s=>s.Value,StringComparer.OrdinalIgnoreCase);return new Dictionary<string,string>{["siteName"]=map.GetValueOrDefault("siteName","SE ONE"),["supportEmail"]=map.GetValueOrDefault("supportEmail","support@seone.com"),["defaultLocale"]=map.GetValueOrDefault("defaultLocale","fa"),["maintenanceMode"]=map.GetValueOrDefault("maintenanceMode","false"),["allowRegistration"]=map.GetValueOrDefault("allowRegistration","true")};}
    private Task<List<SiteSetting>> ReadSettingEntities()=>_db.Set<SiteSetting>().ToListAsync();
    private async Task<IActionResult> CourseResult(Guid id){return Ok(await _db.Set<Course>().Where(c=>c.Id==id).Select(c=>new{id=c.Id,title=c.Title,level=c.Level,basePrice=c.Price,currency=c.Currency,discountPercent=c.DiscountPercent,isPublished=c.IsPublished,studentCount=_db.Set<Enrollment>().Count(e=>e.CourseId==c.Id),lessonCount=_db.Set<Lesson>().Count(l=>l.CourseModule.CourseId==c.Id)}).FirstAsync());}
    private static object MapStudent(User u,int courses)=>new{id=u.Id,fullName=u.FullName,email=u.Email,avatarUrl=u.AvatarUrl??string.Empty,joinedAt=u.CreatedAt,status=u.AccountStatus,coursesEnrolled=courses};
    private static object MapTeacher(User u,TeacherProfile? p)=>new{id=u.Id,userId=u.Id,fullName=u.FullName,email=u.Email,avatarUrl=p?.Avatar??u.AvatarUrl??string.Empty,teachingLanguage=p?.TeachingLanguage??"english",subject=p?.Subject??string.Empty,level=p?.Level??string.Empty,rating=p?.Rating??0,bio=p?.Bio??u.Bio??string.Empty,status=u.AccountStatus};
    private static object MapBlog(BlogPost b)=>new{id=b.Id,title=b.Title,excerpt=b.Excerpt,content=b.Content,author=b.Author,category=b.Category,status=b.IsPublished?"published":"draft",publishedAt=b.IsPublished?b.CreatedAt:(DateTime?)null,readTimeMinutes=ParseReadTime(b.ReadTime),imageUrl=b.ImageUrl??string.Empty,views=0,lang=b.Language};
    private static int ParseReadTime(string s){var digits=new string(s.Where(char.IsDigit).ToArray());return int.TryParse(digits,out var n)?n:5;}
    private static string? GetString(System.Text.Json.JsonElement b,string name)=>b.TryGetProperty(name,out var p)&&p.ValueKind==System.Text.Json.JsonValueKind.String?p.GetString():null;
    private static bool GetBool(System.Text.Json.JsonElement b,string name)=>b.TryGetProperty(name,out var p)&&(p.ValueKind==System.Text.Json.JsonValueKind.True||(p.ValueKind==System.Text.Json.JsonValueKind.String&&bool.TryParse(p.GetString(),out var x)&&x));
    private static void SetIfString(JsonElement b,string name,Action<string> setter){if(b.TryGetProperty(name,out var p)&&p.ValueKind==System.Text.Json.JsonValueKind.String&&p.GetString() is { } v)setter(v);}
    private static void SetDecimal(JsonElement b,string name,Action<decimal> setter){if(b.TryGetProperty(name,out var p)&&p.ValueKind==System.Text.Json.JsonValueKind.Number&&p.TryGetDecimal(out var v))setter(v);}
    private static void SetIfBool(JsonElement b,string name,Action<bool> setter){if(b.TryGetProperty(name,out var p)&&(p.ValueKind==System.Text.Json.JsonValueKind.True||p.ValueKind==System.Text.Json.JsonValueKind.False))setter(p.GetBoolean());}
    private static void ApplyBlog(BlogPost b,JsonElement body){SetIfString(body,"title",v=>b.Title=v);SetIfString(body,"excerpt",v=>b.Excerpt=v);SetIfString(body,"content",v=>b.Content=v);SetIfString(body,"body",v=>b.Content=v);SetIfString(body,"author",v=>b.Author=v);SetIfString(body,"category",v=>b.Category=v);SetIfString(body,"imageUrl",v=>b.ImageUrl=v);SetIfString(body,"lang",v=>b.Language=v);SetIfString(body,"language",v=>b.Language=v);SetIfString(body,"readTime",v=>b.ReadTime=v);if(body.TryGetProperty("isPublished",out var p)&&(p.ValueKind==System.Text.Json.JsonValueKind.True||p.ValueKind==System.Text.Json.JsonValueKind.False))b.IsPublished=p.GetBoolean();}
    private async Task SetTeacherProfileField(Guid teacherId,Action<TeacherProfile> setter){var p=await _db.Set<TeacherProfile>().FirstOrDefaultAsync(p=>p.TeacherId==teacherId);if(p is null){p=new TeacherProfile{Id=Guid.NewGuid(),TeacherId=teacherId};_db.Add(p);}setter(p);}
    private static (
    string firstName,
    string familyName,
    string email,
    string password,
    bool sendWelcome
) ReadCreateUser(JsonElement body)
    {
        return (
            GetString(body, "firstName") ?? "",
            GetString(body, "familyName") ?? "",
            GetString(body, "email") ?? "",
            GetString(body, "password") ?? "",
            GetBool(body, "sendWelcomeEmail")
        );
    }
}

public sealed class AdminCreateUserRequest
{
    public string FirstName { get; set; } = "";

    public string FamilyName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Password { get; set; } = "";

    public bool SendWelcomeEmail { get; set; }
}
public sealed class StatusRequest { public string Status {get;set;}=""; }
public sealed class AdminEnrollRequest { public Guid CourseId {get;set;} public bool WaivePayment {get;set;}=true; }
public sealed class ModuleRequest { public string? Title {get;set;} public string? Description {get;set;} public int Order {get;set;} }
public sealed class LessonRequest { public string? Title {get;set;} public string? Body {get;set;} public string? VideoUrl {get;set;} public int? Duration {get;set;} public int? Order {get;set;} }
public sealed class SlotsRequest { public List<AdminSlotRequest>? Slots {get;set;} }
public sealed class AdminSlotRequest { public string DayOfWeek {get;set;}="Monday"; public string StartTime {get;set;}="09:00"; public string EndTime {get;set;}="10:00"; }
