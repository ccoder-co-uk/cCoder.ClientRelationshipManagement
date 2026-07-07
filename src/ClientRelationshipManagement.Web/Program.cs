using cCoder.Security;
using cCoder.Security.Exposures;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Migration;

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

        string crmConnection = ConfigurationValueResolver.GetRequired(
            builder.Configuration,
            "ConnectionStrings:CRM");

        string crmAdminConnection = ConfigurationValueResolver.GetOptional(
            builder.Configuration,
            "ConnectionStrings:CRMAdmin")
            ?? crmConnection;

        string ssoConnection = ConfigurationValueResolver.GetRequired(
            builder.Configuration,
            "ConnectionStrings:SSO");

        string decryptionKey = ConfigurationValueResolver.GetRequired(
            builder.Configuration,
            "Settings:DecryptionKey");

        builder.Services.AddCrmApplication(
            builder.Configuration,
            crmConnection,
            crmAdminConnection,
            ssoConnection,
            decryptionKey,
            options =>
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

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        using (IServiceScope scope = app.Services.CreateScope())
        {
            scope.ServiceProvider
                .GetRequiredService<ICrmDatabaseInitialiser>()
                .InitialiseAsync()
                .GetAwaiter()
                .GetResult();
        }

        app.Run();
    }
}
