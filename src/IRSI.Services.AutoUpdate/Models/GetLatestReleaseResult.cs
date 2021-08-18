using IRSI.Services.AutoUpdate.Common.Models;

namespace IRSI.Services.AutoUpdate.Models
{
    public class GetLatestReleaseResult : Result
    {
        public GitHubRelease Release { get; set; }

        public static GetLatestReleaseResult FailedResult(string errorMessage)
        {
            return new()
            {
                Status = ResultStatus.Failure,
                Error = errorMessage
            };
        }

        public static GetLatestReleaseResult SuccessResult(GitHubRelease release)
        {
            return new()
            {
                Status = ResultStatus.Success,
                Release = release
            };
        }
    }
}