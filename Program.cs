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

    // In production behind Traefik (TLS terminated at Traefik), proxy IP may vary.
    // Clear defaults so forwarded headers are processed from the trusted reverse proxy hop.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust exactly 1 proxy hop (Traefik -> app).
    options.ForwardLimit = 1;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();

// Traefik already handles HTTPS termination at edge.
// Avoid app-level HTTPS redirect in container behind reverse proxy to prevent route/scheme mismatch.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
