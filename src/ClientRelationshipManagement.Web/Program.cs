using cCoder.Security;
using cCoder.Security.Data.EF;
using cCoder.Security.Exposures;
using cCoder.ClientRelationshipManagement.Runtime;
using cCoder.ClientRelationshipManagement.Runtime.Services.Migration;
using ClientRelationshipManagement.Web.Models;

namespace ClientRelationshipManagement.Web;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        AppConfiguration configuration = new();
        builder.Configuration.Bind(instance: configuration);

        builder.Services.AddSecurityData(configuration.SecurityData);
        builder.Services.AddSecurityWeb(configuration.Security);
        builder.Services.AddCrmData(configuration.CRMData);

        builder.Services.AddCrmApplication(
            rootConfiguration: builder.Configuration,
            aiConfiguration: configuration.AI,
            configure: options =>
            {
                options.IncludeMvc = true;
                options.IncludeHostedServices = false;
            });

        WebApplication app = builder.Build();

        ILogger log = app.Services
            .GetService<ILoggerFactory>()?
            .CreateLogger("CRM");

        app.UseStaticFiles();
        app.UseSession();
        app.UseCors(policy =>
        {
            policy.AllowAnyOrigin();
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
        });

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (System.Security.SecurityException)
            {
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
        });

        app.UseSecurityExposure(log);
        app.ListenToSecurityEvents();

        app.MapControllers();

        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(
            "/swagger/ClientRelationshipManagement/swagger.json",
            "Client Relationship Management"));

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Services
            .InitialiseCrmApplicationAsync()
            .GetAwaiter()
            .GetResult();

        app.Run();
    }
}