using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project_Keu.Services.Notifications;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Authorization;
using Project_Keu.Infrastructure.Notifications;
using Project_Keu.Services;
using Project_Keu.Services.Admin;
using Project_Keu.Services.Answers;
using Project_Keu.Services.Categories;
using Project_Keu.Services.Employees;
using Project_Keu.Services.PageQuestion;
using Project_Keu.Services.QuestionCategories;
using Project_Keu.Services.Questions;
using Project_Keu.Services.QuestionStatuses;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

// Utilitas baris perintah untuk menyiapkan kredensial administrator:
//   dotnet Project_Keu.dll --hash-password "kata-sandi"
// Keluarannya diisikan ke environment variable Admin__PasswordHash.
if (args is ["--hash-password", var plainPassword, ..])
{
    Console.WriteLine(PasswordHasher.Hash(plainPassword));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Konfigurasi
// ---------------------------------------------------------------------------

// Connection string TIDAK boleh dihardcode di appsettings.json yang ikut ter-commit.
// Set lewat environment variable: ConnectionStrings__DefaultConnection=...
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' belum diset. " +
        "Isi lewat environment variable ConnectionStrings__DefaultConnection " +
        "(atau 'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"' untuk pengembangan lokal).");
}

var isBehindReverseProxy = builder.Configuration.GetValue("ReverseProxy:Enabled", false);
var forwardLimit = builder.Configuration.GetValue("ReverseProxy:ForwardLimit", 1);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

        // Entity EF punya navigasi dua arah (Question <-> Category <-> Question).
        // Tanpa ini serialisasi melempar "object cycle detected" -> 500.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/Login";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Cookie.Name = "PilarKeuangan.Auth";
        options.Cookie.HttpOnly = true;
        // Lax, bukan Strict: cookie tetap terkirim saat pengguna diarahkan balik
        // ke halaman admin setelah login berhasil.
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = forwardLimit;

    // Di dalam Docker, IP proxy (Traefik) tidak diketahui saat startup, sehingga
    // daftar bawaan harus dikosongkan agar header X-Forwarded-* diterima.
    // Konsekuensinya: container TIDAK boleh diekspos langsung ke internet.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        npgsql.CommandTimeout(30);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Batas umum per alamat IP, cukup longgar untuk pemakaian normal.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Halaman login. Hanya percobaan kirim (POST) yang dibatasi ketat untuk
    // memperlambat tebak sandi; sekadar membuka atau me-refresh halamannya tidak
    // boleh ikut mengunci administrator yang sah.
    options.AddPolicy("login", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isSubmit = HttpMethods.IsPost(context.Request.Method);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: isSubmit ? $"login-submit:{clientIp}" : $"login-view:{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isSubmit ? 10 : 60,
                Window = TimeSpan.FromMinutes(5)
            });
    });

    // Pencarian pegawai lewat NIP bisa dipakai untuk enumerasi data pegawai,
    // jadi diberi batas yang jauh lebih ketat.
    options.AddPolicy("employee-lookup", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddSingleton<AppTimeZone>();

// Otorisasi berbasis izin: policy dibentuk otomatis dari kode izin, sehingga
// [Authorize(Policy = "questions.export")] langsung berlaku tanpa pendaftaran manual.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---------------------------------------------------------------------------
// Pemberitahuan WhatsApp (WAHA)
// ---------------------------------------------------------------------------

builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));

builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddHttpClient<IWhatsAppSender, WahaWhatsAppSender>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value;

    // BaseUrl bisa saja belum diisi (pemberitahuan dimatikan). Uri("") melempar galat
    // saat klien dibuat, jadi alamatnya hanya dipasang bila memang sah; pengirimnya
    // sendiri sudah menolak bekerja saat konfigurasinya belum lengkap.
    if (Uri.TryCreate(options.Waha.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        // Garis miring penutup wajib: tanpa itu segmen terakhir path akan tergantikan
        // oleh alamat relatif "api/sendText".
        client.BaseAddress = baseAddress.AbsoluteUri.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/");
    }

    if (!string.IsNullOrWhiteSpace(options.Waha.ApiKey))
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", options.Waha.ApiKey);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.Waha.TimeoutSeconds, 5, 120));
});

builder.Services.AddScoped<AdminAccountService>();
builder.Services.AddScoped<AdminDashboardService>();

builder.Services.AddScoped<PageQuestionQueryService>();
builder.Services.AddScoped<PageQuestionExportService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<QuestionCategoryService>();
builder.Services.AddScoped<QuestionStatusService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<QuestionService>();
builder.Services.AddScoped<AnswerService>();
builder.Services.AddScoped<QuestionGroupsService>();
builder.Services.AddScoped<QuestionsLandingService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------

// Harus paling awal: middleware setelahnya perlu melihat skema & IP asli klien.
if (isBehindReverseProxy)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Request API dibalas ProblemDetails (JSON), halaman biasa dibalas /Error.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api"),
        branch => branch.UseExceptionHandler());

    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePages();

// TLS diterminasi di reverse proxy; redirect di dalam container hanya
// menyebabkan redirect loop, jadi dilewati saat berjalan di belakang proxy.
if (!isBehindReverseProxy)
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Liveness: proses hidup, tanpa menyentuh database.
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

var activeUsers = new ConcurrentDictionary<string, DateTime>();



app.Run();
