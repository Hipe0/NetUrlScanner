using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NetURLScanner.Data;
using NetURLScanner.Options;
using NetURLScanner.Services;
using System.Reflection;

// Điểm vào ứng dụng — cấu hình DI, middleware, migrate DB + seed khi khởi động.
var builder = WebApplication.CreateBuilder(args);

// LocalDB: Server=(localdb)\MSSQLLocalDB;Database=NetURLScannerDb
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Đọc cấu hình từ appsettings.json vào các lớp Options (Admin, OCR, Safe Browsing…).
builder.Services.Configure<AdminSeedOptions>(
    builder.Configuration.GetSection(AdminSeedOptions.SectionName));
builder.Services.Configure<GoogleSafeBrowsingOptions>(
    builder.Configuration.GetSection(GoogleSafeBrowsingOptions.SectionName));
builder.Services.Configure<OcrOptions>(
    builder.Configuration.GetSection(OcrOptions.SectionName));

builder.Services.AddHttpClient("SafeBrowsing", client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
});

// MVC (Razor Views) + API Controllers + Swagger/OpenAPI.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NetURLScanner API",
        Version = "v1",
        Description = "RESTful API quét URL, phân loại rủi ro và quản lý lịch sử quét.",
        Contact = new OpenApiContact
        {
            Name = "NetURLScanner Team"
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// EF Core + tự migrate schema khi khởi động (tạo DB nếu chưa có).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<GoogleAuthOptions>(
    builder.Configuration.GetSection(GoogleAuthOptions.SectionName));

var googleAuth = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>() ?? new GoogleAuthOptions();

// Xác thực: Cookie là mặc định; Google OAuth chỉ đăng ký khi có ClientId/Secret trong config.
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";           // Chưa login → chuyển về đây
    options.AccessDeniedPath = "/Account/AccessDenied"; // Không đủ quyền role
});

if (googleAuth.Enabled &&
    !string.IsNullOrWhiteSpace(googleAuth.ClientId) &&
    !string.IsNullOrWhiteSpace(googleAuth.ClientSecret))
{
    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = googleAuth.ClientId;
        options.ClientSecret = googleAuth.ClientSecret;
        options.CallbackPath = "/signin-google";
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });
}

// Đăng ký các service nghiệp vụ — Scoped = một instance mỗi HTTP request.
builder.Services.AddScoped<UrlScannerService>();
builder.Services.AddScoped<AdminSeedService>();
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<GeminiChatService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<SampleDataSeedService>();
builder.Services.AddScoped<CmsSeedService>();
builder.Services.AddScoped<ContentCategorizationService>();
builder.Services.AddScoped<GoogleSafeBrowsingService>();
builder.Services.AddScoped<DomainVoteService>();
builder.Services.AddScoped<OcrService>();
builder.Services.AddScoped<UrlExtractionService>();
builder.Services.AddHttpClient<IBankAccountLookupService, VietQrBankService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error404");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Thứ tự bắt buộc: Routing → Auth → Authorization → Map endpoints.
app.UseAuthentication();  // Đọc cookie, gán User/Claims vào HttpContext
app.UseAuthorization();   // Kiểm tra [Authorize], Roles

// Middleware tùy chỉnh: Swagger chỉ dành cho Admin đã đăng nhập.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/swagger") || context.Request.Path.StartsWithSegments("/api/docs"))
    {
        if (context.User?.Identity?.IsAuthenticated != true || !context.User.IsInRole("Admin"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NetURLScanner API v1");
    c.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NetURLScanner API v1");
        options.RoutePrefix = "api/docs";
        options.DocumentTitle = "NetURLScanner API Docs";
    });
}

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

// Migrate DB + seed trước khi nhận request — tránh lỗi ghi dữ liệu (Google login, Premium…).
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var adminSeed = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
    await adminSeed.SeedAsync();

    var cmsSeed = scope.ServiceProvider.GetRequiredService<CmsSeedService>();
    await cmsSeed.SeedAsync();

    var sampleDataSeed = scope.ServiceProvider.GetRequiredService<SampleDataSeedService>();
    await sampleDataSeed.SeedAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error during startup database/seed: {ex.Message}");
}

app.Run();
