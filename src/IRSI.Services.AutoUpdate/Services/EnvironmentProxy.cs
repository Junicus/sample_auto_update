using System;
using IRSI.Services.AutoUpdate.Common.Interfaces;

namespace IRSI.Services.AutoUpdate.Services
{
    public class EnvironmentProxy : IEnvironmentProxy
    {
        public string? GetEnvironmentVariable(string variableName) => Environment.GetEnvironmentVariable(variableName);
    }
}