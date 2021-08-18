using System.Threading.Tasks;
using Coravel.Invocable;
using IRSI.Services.AutoUpdate.Options;

namespace IRSI.Services.AutoUpdate.Jobs
{
    public class UpdateServiceJob : IInvocable, IInvocableWithPayload<UpdateServicePayload>
    {
        public Task Invoke()
        {
            return Task.CompletedTask;
        }

        public UpdateServicePayload Payload { get; set; }
    }

    public record UpdateServicePayload(ServiceDefinition ServiceDefinition, int AssetId);
}