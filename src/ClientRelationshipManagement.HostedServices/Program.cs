using ClientRelationshipManagement.Web;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.Imports;
using ClientRelationshipManagement.Web.Services.Imports;
using ClientRelationshipManagement.Web.Services.Migration;

namespace ClientRelationshipManagement.HostedServices;

public static class Program
{
    public static async Task Main(string[] args)
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
                options.IncludeMvc = false;
                options.IncludeHostedServices = true;
            });

        builder.Services.AddCors();

        WebApplication app = builder.Build();
        app.UseCors(policy =>
        {
            policy.AllowAnyOrigin();
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
        });

        MapImportEndpoints(app);

        using IServiceScope scope = app.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<ICrmDatabaseInitialiser>()
            .InitialiseAsync();

        await app.RunAsync();
    }

    static void MapImportEndpoints(WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/internal/imports");

        group.MapPost("/upload-session", async (
            HostedImportUploadSessionRequest request,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            ImportUploadSessionResponse response = await service.CreateUploadSessionAsync(request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPut("/{id:guid}/chunks/{chunkIndex:int}", async (
            Guid id,
            int chunkIndex,
            string uploadSessionId,
            HttpRequest request,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            ImportStatusResponse response = await service.SaveChunkAsync(
                id,
                uploadSessionId,
                chunkIndex,
                request.Body,
                cancellationToken);

            return Results.Ok(response);
        });

        group.MapPost("/{id:guid}/complete-upload", async (
            Guid id,
            HostedImportCompleteUploadRequest request,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            ImportStatusResponse response = await service.CompleteUploadAsync(id, request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapGet("/{id:guid}/upload-status", async (
            Guid id,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            ImportStatusResponse response = await service.GetStatusAsync(id, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/{id:guid}/analyse", async (
            Guid id,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            ImportStatusResponse response = await service.AnalyseAsync(id, cancellationToken);
            return Results.Ok(response);
        });

        group.MapDelete("/{id:guid}/files", async (
            Guid id,
            IImportFileWorkspaceService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteFilesAsync(id, cancellationToken);
            return Results.NoContent();
        });
    }
}
