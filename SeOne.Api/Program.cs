using Microsoft.EntityFrameworkCore;
using SeOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using SeOne.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SeOne.Infrastructure.Authentication;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Services;
using SeOne.Domain.Enums;


var builder = WebApplication.CreateBuilder(args);
var configuredOrigins =
    builder.Configuration["AllowedOrigins"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

var allowedOrigins = configuredOrigins
    .Concat([
        "https://se-one-delta.vercel.app",
        "http://localhost:3000"
    ])
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Name = "Authorization",
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)]
                = []
        });
});

var databaseProvider = builder.Configuration["DatabaseProvider"]?.Trim().ToLowerInvariant() ?? "sqlserver";

var postgresConnection =
    Environment.GetEnvironmentVariable("SEONE_POSTGRES_CONNECTION")
    ?? builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddDbContext<SeOneDbContext>(options =>
{
    if (databaseProvider == "postgres")
    {
        if (string.IsNullOrWhiteSpace(postgresConnection))
            throw new InvalidOperationException("PostgreSQL connection string is not configured.");

        options.UseNpgsql(
            postgresConnection,
            npgsqlOptions => npgsqlOptions.MigrationsAssembly("SeOne.Migrations.Postgres"));
    }
    else if (databaseProvider == "sqlserver")
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlServerOptions => sqlServerOptions.MigrationsAssembly("SeOne.Infrastructure"));
    }
    else
    {
        throw new InvalidOperationException(
            $"Unsupported database provider: {databaseProvider}");
    }
});

builder.Services
    .AddIdentityCore<User>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<SeOneDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpSender, LoggingOtpSender>();
builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseModuleService, CourseModuleService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<ICourseLearningService, CourseLearningService>();
builder.Services.AddScoped<ITeacherService, TeacherService>();
builder.Services.AddScoped<ITeacherAvailabilityService, TeacherAvailabilityService>();
builder.Services.AddScoped<ITeacherDashboardService, TeacherDashboardService>();
builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<ICourseDetailsService, CourseDetailsService>();
builder.Services.AddScoped<ITeacherCourseStudentService, TeacherCourseStudentService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<INotificationService,NotificationService>();
builder.Services.AddScoped<IMessagingService,MessagingService>();
builder.Services.AddScoped<IPaymentGateway, DevelopmentPaymentGateway>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPracticeService, PracticeService>();
builder.Services.AddHttpClient<IImageStorageService, SupabaseImageStorageService>();

var app = builder.Build();

app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

await SeedAdminAsync(app);

app.Run();

static async Task SeedAdminAsync(WebApplication app)
{
	var email = app.Configuration["SEONE_ADMIN_EMAIL"];
	var password = app.Configuration["SEONE_ADMIN_PASSWORD"];
	var fullName = app.Configuration["SEONE_ADMIN_NAME"] ?? "SE ONE Administrator";

	if (string.IsNullOrWhiteSpace(email))
    		throw new InvalidOperationException("SEONE_ADMIN_EMAIL is missing from production configuration.");

	if (string.IsNullOrWhiteSpace(password))
    		throw new InvalidOperationException("SEONE_ADMIN_PASSWORD is missing from production configuration.");

    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

    var admin = await userManager.FindByEmailAsync(email);
    if (admin is null)
    {
        admin = new User { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, Role = UserRole.Admin, AccountStatus = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
    else
    {
        admin.Role = UserRole.Admin;
        admin.AccountStatus = "active";
        admin.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(admin);
    }
    if (!await userManager.IsInRoleAsync(admin, "Admin"))
        await userManager.AddToRoleAsync(admin, "Admin");
}
