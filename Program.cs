using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Services.AdminDashboardV2;
using Project_Keu.Services.Answers;
using Project_Keu.Services.Categories;
using Project_Keu.Services.Employees;
using Project_Keu.Services.QuestionCategories;
using Project_Keu.Services.Questions;
using Project_Keu.Services.QuestionStatuses;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AdminDashboardV2QueryService>();
builder.Services.AddScoped<AdminDashboardV2ExportService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<QuestionCategoryService>();
builder.Services.AddScoped<QuestionStatusService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<QuestionService>();
builder.Services.AddScoped<AnswerService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Behind Docker + Traefik, proxy IP can be dynamic.
    // Clear defaults so forwarded headers from trusted ingress are processed.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust single reverse proxy hop by default (Traefik -> app).
    options.ForwardLimit = 1;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// IMPORTANT: apply forwarded headers before other middleware that reads scheme/host.
app.UseForwardedHeaders();

// Only redirect to HTTPS when request is not already HTTPS after forwarded headers.
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps &&
        !string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
    {
        var httpsUrl = $"https://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(httpsUrl, permanent: true);
        return;
    }

    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
