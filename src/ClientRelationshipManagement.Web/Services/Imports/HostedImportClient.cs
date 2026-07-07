using System.Net.Http.Json;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.Imports;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Imports;

public sealed class HostedImportClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ImportWorkflowOptions> options)
    : IHostedImportClient
{
    public async ValueTask<ImportUploadSessionResponse> CreateUploadSessionAsync(
        HostedImportUploadSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/internal/imports/upload-session",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportUploadSessionResponse>(cancellationToken)
            ?? new ImportUploadSessionResponse();
    }

    public async ValueTask<ImportStatusResponse> AnalyseAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/internal/imports/{importId}/analyse", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportStatusResponse>(cancellationToken)
            ?? new ImportStatusResponse();
    }

    public async ValueTask<ImportStatusResponse> GetStatusAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateClient();
        return await client.GetFromJsonAsync<ImportStatusResponse>(
            $"/internal/imports/{importId}/upload-status",
            cancellationToken) ?? new ImportStatusResponse();
    }

    public async ValueTask DeleteFilesAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/internal/imports/{importId}/files", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    HttpClient CreateClient()
    {
        HttpClient client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(options.Value.HostedServicesBaseUrl.TrimEnd('/'));
        return client;
    }
}
