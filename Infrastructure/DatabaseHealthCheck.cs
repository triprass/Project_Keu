using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Project_Keu.Data;

namespace Project_Keu.Infrastructure;

/// <summary>
/// Health check untuk probe orchestrator (Docker/Traefik/Kubernetes):
/// memastikan koneksi ke PostgreSQL benar-benar bisa dibuka, bukan hanya proses hidup.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Koneksi database tersedia.")
                : HealthCheckResult.Unhealthy("Database tidak dapat dihubungi.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database tidak dapat dihubungi.", ex);
        }
    }
}
