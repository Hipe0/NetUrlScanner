using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<UrlScannerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error404");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

// Làm nóng (warm-up) Entity Framework, kết nối DB và HTTP client trên luồng nền
// để loại bỏ độ trễ 2-3 giây ở lượt click quét URL đầu tiên.
_ = Task.Run(async () =>
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _ = await db.UrlScans.AnyAsync();
            _ = await db.TrustedBrands.AnyAsync();
            _ = await db.BlacklistedDomains.AnyAsync();
        }
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(2);
            _ = await client.GetAsync("http://ip-api.com/json/8.8.8.8");
        }
    }
    catch
    {
        // Fail silently
    }
});

app.Run();
