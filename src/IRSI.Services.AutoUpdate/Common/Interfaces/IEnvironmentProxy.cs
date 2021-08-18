namespace IRSI.Services.AutoUpdate.Common.Interfaces
{
    public interface IEnvironmentProxy
    {
        string? GetEnvironmentVariable(string variableName);
    }
}