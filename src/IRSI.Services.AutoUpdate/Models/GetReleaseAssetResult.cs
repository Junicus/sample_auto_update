using IRSI.Services.AutoUpdate.Common.Models;

namespace IRSI.Services.AutoUpdate.Models
{
    public class GetReleaseAssetResult : Result
    {
        public byte[] File { get; set; }

        public static GetReleaseAssetResult FailedResult(string errorMessage)
        {
            return new()
            {
                Status = ResultStatus.Failure,
                Error = errorMessage
            };
        }

        public static GetReleaseAssetResult SuccessResult(byte[] file)
        {
            return new()
            {
                Status = ResultStatus.Success,
                File = file
            };
        }
    }
}