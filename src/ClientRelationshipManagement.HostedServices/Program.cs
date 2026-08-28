using cCoder.ClientRelationshipManagement.Runtime;
using cCoder.ClientRelationshipManagement.Runtime.Models.Imports;
using cCoder.ClientRelationshipManagement.Runtime.Services.Imports;
using cCoder.ClientRelationshipManagement.Runtime.Services.Migration;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.Security;
using cCoder.Security.Data.EF;
using ClientRelationshipManagement.HostedServices.Models;

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
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        AppConfiguration configuration = new();
        builder.Configuration.Bind(instance: configuration);

        builder.Services.AddSecurityData(configuration.SecurityData);
        builder.Services.AddSecurityHostedServices(configuration.Security);
        builder.Services.AddCrmData(configuration.CRMData);

        builder.Services.AddCrmApplication(
            rootConfiguration: builder.Configuration,
            aiConfiguration: configuration.AI,
            configure: options =>
            {
                options.IncludeMvc = false;
                options.IncludeHostedServices = true;
            });

        builder.Services.AddSingleton<ICRMAuthInfo>(new HostedAuthorizationBroker(
            configuration.CRMData.ConnectionString,
            builder.Configuration["CRM:AgentWorkflows:ExecutionUserId"]));

        builder.Services.AddCors();
        builder.Services.AddDistributedMemoryCache();

        WebApplication app = builder.Build();
        app.UseCors(policy =>
        {
            policy.AllowAnyOrigin();
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
        });

        MapServiceEndpoints(app);
        MapImportEndpoints(app);

        await app.Services.InitialiseCrmApplicationAsync();

        await app.RunAsync();
    }

    static void MapServiceEndpoints(WebApplication app)
    {
        app.MapGet("/", (HttpRequest request) =>
        {
            int webPort = request.Host.Port == 7295 ? 7294 : 5294;
            string host = request.Host.Host;

            return Results.Redirect($"{request.Scheme}://{host}:{webPort}/");
        });

        app.MapGet("/health", () => Results.Ok(new
        {
            Service = "ClientRelationshipManagement.HostedServices",
            Status = "Healthy"
        }));
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