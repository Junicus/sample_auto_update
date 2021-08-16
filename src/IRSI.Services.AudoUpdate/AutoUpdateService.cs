using Microsoft.Extensions.Hosting;

namespace IRSI.Services.AudoUpdate
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
            _host.Start();
        }

        public void Stop()
        {
            _host.Dispose();
        }
    }
}