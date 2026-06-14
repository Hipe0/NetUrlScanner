using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NetURLScanner.Data;
using NetURLScanner.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<UrlScannerService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NetURLScanner API v1");
    c.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NetURLScanner API v1");
        options.RoutePrefix = "api/docs";
        options.DocumentTitle = "NetURLScanner API Docs";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error404");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

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

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _ = await client.GetAsync("http://ip-api.com/json/8.8.8.8");
    }
    catch
    {
        // Fail silently
    }
});

app.Run();
