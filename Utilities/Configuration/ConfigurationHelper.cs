// Utilities/Configuration/ConfigurationHelper.cs
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;

namespace SubashaVentures.Utilities.Configuration
{
    public static class ConfigurationHelper
    {
        public static IConfiguration BuildConfiguration(WebAssemblyHostBuilder builder)
        {
            var environment = builder.HostEnvironment.Environment;
            
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

            return configBuilder.Build();
        }

        public static bool IsProduction(this IConfiguration configuration)
        {
            return configuration["App:Environment"]?.Equals("Production", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static bool IsAnalyticsEnabled(this IConfiguration configuration)
        {
            return bool.TryParse(configuration["App:EnableAnalytics"], out var enabled) && enabled;
        }
    }
}
