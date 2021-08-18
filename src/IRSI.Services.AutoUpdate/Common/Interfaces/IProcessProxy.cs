using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace IRSI.Services.AutoUpdate.Common.Interfaces
{
    public interface IProcessProxy
    {
        Process Start(string application);
        Process Start(string application, IEnumerable<string> arguments);
        Task WaitForExitAsync(Process process);
    }
}