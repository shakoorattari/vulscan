using Microsoft.Extensions.DependencyInjection;
using Vulscan.Application.Interfaces;
using Vulscan.Application.Services;

namespace Vulscan.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IVulnerabilityService, VulnerabilityService>();
        services.AddScoped<IInstanceService, InstanceService>();
        services.AddScoped<IScanProcessor, ScanProcessor>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
