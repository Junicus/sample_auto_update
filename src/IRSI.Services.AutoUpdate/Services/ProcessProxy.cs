using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using IRSI.Services.AutoUpdate.Common.Interfaces;

namespace IRSI.Services.AutoUpdate.Services
{
    public class ProcessProxy : IProcessProxy
    {
        public Process Start(string application) => Process.Start(application);

        public Process Start(string application, IEnumerable<string> arguments) =>
            Process.Start(application, arguments);

        public Task WaitForExitAsync(Process process) => process.WaitForExitAsync();
    }
}