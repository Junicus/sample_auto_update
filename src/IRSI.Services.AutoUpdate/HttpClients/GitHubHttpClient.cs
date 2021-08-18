using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Models;
using IRSI.Services.AutoUpdate.Serialization;

namespace IRSI.Services.AutoUpdate.HttpClients
{
    public class GitHubHttpClient : IGitHubHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public GitHubHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _jsonSerializerOptions = new()
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNamingPolicy = new SnakeCaseNamingPolicy()
            };
        }

        public async Task<GetLatestReleaseResult> GetLatestRelease(string owner, string repoName)
        {
            var response = await _httpClient.GetAsync($"/repos/{owner}/{repoName}/releases/latest");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var release = JsonSerializer.Deserialize<GitHubRelease>(content, _jsonSerializerOptions);
                return release == null
                    ? GetLatestReleaseResult.FailedResult($"Error parsing release: {content}")
                    : GetLatestReleaseResult.SuccessResult(release);
            }

            return GetLatestReleaseResult.FailedResult(content);
        }

        public async Task<GetReleaseAssetResult> GetReleaseAsset(string owner, string repoName, int assetId)
        {
            using var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, $"/repos/{owner}/{repoName}/releases/assets/{assetId}");
            requestMessage.Headers.Accept.TryParseAdd("application/octet-stream");
            var response = await _httpClient.SendAsync(requestMessage);
            if (response.IsSuccessStatusCode)
            {
                await using var networkStream = await response.Content.ReadAsStreamAsync();
                await using var memoryStream = new MemoryStream();
                await networkStream.CopyToAsync(memoryStream);
                return GetReleaseAssetResult.SuccessResult(memoryStream.ToArray());
            }

            var content = await response.Content.ReadAsStringAsync();
            return GetReleaseAssetResult.FailedResult(content);
        }
    }
}