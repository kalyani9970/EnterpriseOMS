// Bootstrap logger: captures failures that happen before the real logger is configured.
using EnterpriseOMS.Api.Extensions;
using EnterpriseOMS.Api.Middleware;
using EnterpriseOMS.Application;
using EnterpriseOMS.Infrastructure;
using EnterpriseOMS.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddSwaggerDocumentation();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AngularClient", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    var app = builder.Build();

    // Order matters: the exception handler wraps everything registered after it.
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerDocumentation();
    }

    app.UseHttpsRedirection();
    app.UseCors("AngularClient");

    app.UseAuthentication();   // who are you?
    app.UseAuthorization();    // are you allowed?

    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        // Applies migrations and seeds roles, permissions and the admin user.
        await app.Services.InitialiseDatabaseAsync();
    }

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name is not "HostAbortedException")
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Makes Program visible to the functional-test project (WebApplicationFactory<Program>).
public partial class Program { }