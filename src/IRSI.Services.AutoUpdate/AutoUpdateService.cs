using Coravel;
using Coravel.Queuing.Interfaces;
using IRSI.Services.AutoUpdate.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IRSI.Services.AutoUpdate
{
    public class AutoUpdateService
    {
        private readonly IHost _host;

        public AutoUpdateService(IHost host)
        {
            _host = host;
        }

        public void Start()
        {
            _host.Services.UseScheduler(scheduler => { });
            _host.Start();

            var queue = _host.Services.GetRequiredService<IQueue>();
            queue.QueueInvocable<CheckUpdatesJob>();
        }

        public void Stop()
        {
            _host.Dispose();
        }
    }
}