using System.Threading.Tasks;
using IRSI.Services.AutoUpdate.Models;
using IRSI.Services.AutoUpdate.Options;

namespace IRSI.Services.AutoUpdate.Common.Interfaces
{
    public interface IGitHubHttpClient
    {
        public Task<GetLatestReleaseResult> GetLatestRelease(string owner, string repoName);
        public Task<GetReleaseAssetResult> GetReleaseAsset(string owner, string repoName, int assetId);
    }
}