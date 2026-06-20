using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NetURLScanner.Data;
using NetURLScanner.Options;
using NetURLScanner.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<OcrOptions>(
    builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(
    builder.Configuration.GetSection(GoogleAuthOptions.SectionName));

var googleAuth = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>() ?? new GoogleAuthOptions();

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
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

builder.Services.AddScoped<UrlScannerService>();
builder.Services.AddScoped<AdminSeedService>();
builder.Services.AddHttpClient<GeminiChatService>();
builder.Services.AddScoped<CmsSeedService>();
builder.Services.AddScoped<ContentCategorizationService>();
builder.Services.AddScoped<GoogleSafeBrowsingService>();
builder.Services.AddScoped<DomainVoteService>();
builder.Services.AddScoped<OcrService>();
builder.Services.AddScoped<UrlExtractionService>();

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

app.UseAuthentication();
app.UseAuthorization();

// Chỉ cho phép tài khoản có quyền Admin truy cập Swagger UI và API Docs
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

_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _ = await db.UrlScans.AnyAsync();
        _ = await db.TrustedBrands.AnyAsync();
        _ = await db.BlacklistedDomains.AnyAsync();

        var adminSeed = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
        await adminSeed.SeedAsync();

        var cmsSeed = scope.ServiceProvider.GetRequiredService<CmsSeedService>();
        await cmsSeed.SeedAsync();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _ = await client.GetAsync("http://ip-api.com/json/8.8.8.8");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during startup: {ex.Message}");
        // Fail silently
    }
});

app.Run();
