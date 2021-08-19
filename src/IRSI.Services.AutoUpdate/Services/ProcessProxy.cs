using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using IRSI.Services.AutoUpdate.Common.Interfaces;

namespace IRSI.Services.AutoUpdate.Services
{
    public class ProcessProxy : IProcessProxy
    {
        public Process Start(string application)
        {
            return Process.Start(application);
        }

        public Process Start(string application, IEnumerable<string> arguments)
        {
            return Process.Start(application, arguments);
        }

        public Task WaitForExitAsync(Process process)
        {
            return process.WaitForExitAsync();
        }
    }
}