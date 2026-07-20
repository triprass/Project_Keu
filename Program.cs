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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.Run();