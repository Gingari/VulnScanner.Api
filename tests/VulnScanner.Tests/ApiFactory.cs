using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace VulnScanner.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var backgroundServices = services
                .Where(descriptor =>
                    descriptor.ImplementationType ==
                    typeof(ScanBackgroundService))
                .ToArray();

            foreach (var descriptor in backgroundServices)
            {
                services.Remove(descriptor);
            }

            services.PostConfigure<ScanningOptions>(options =>
            {
                options.MaxParallelScans = 1;
                options.QueueCapacity = 1;
            });

            services.PostConfigure<ReposOptions>(options =>
            {
                options.AllowedHosts = ["github.com"];
                options.CloneTimeoutSeconds = 30;
            });
        });
    }
}