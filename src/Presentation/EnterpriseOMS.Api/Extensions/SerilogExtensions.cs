using Serilog;

namespace EnterpriseOMS.Api.Extensions
{
    public static class SerilogExtensions
    {
        /// <summary>
        /// Replaces the default logger with Serilog, configured from the "Serilog" section of
        /// appsettings. Sinks and levels can change without recompiling.
        /// ReadFrom.Services lets enrichers and sinks use DI services.
        /// </summary>
        public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "EnterpriseOMS.Api"));

            return builder;
        }
    }
}
