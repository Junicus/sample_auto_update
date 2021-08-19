using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;
using Coravel.Invocable;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Models;
using IRSI.Services.AutoUpdate.Options;
using IRSI.Services.AutoUpdate.Utilities;
using Microsoft.Extensions.Options;

namespace IRSI.Services.AutoUpdate.Jobs
{
    public class InstallServiceJob : IInvocable, IInvocableWithPayload<InstallServicePayload>
    {
        private const string ServicesPath = "SERVICES_PATH";
        private readonly IEnvironmentProxy _environment;
        private readonly IFileSystem _fileSystem;
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IProcessProxy _processProxy;
        private readonly ServiceBusSettings _serviceBusSettings;
        private readonly StoreSettings _storeSettings;

        public InstallServiceJob(IGitHubHttpClient gitHubHttpClient, IFileSystem fileSystem,
            IEnvironmentProxy environment, IProcessProxy processProxy, IOptions<StoreSettings> storeOptions,
            IOptions<ServiceBusSettings> serviceBusOptions)
        {
            _gitHubHttpClient = gitHubHttpClient;
            _fileSystem = fileSystem;
            _environment = environment;
            _processProxy = processProxy;
            _storeSettings = storeOptions.Value;
            _serviceBusSettings = serviceBusOptions.Value;
        }

        private string TempBasePath => _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        private string AssetBasePath => _fileSystem.Path.Combine(TempBasePath, $"{Payload.AssetId}");
        private string AssetFilePath => _fileSystem.Path.Combine(AssetBasePath, $"{Payload.AssetId}.zip");
        private string ExtractPath => _fileSystem.Path.Combine(AssetBasePath, "extract");

        private string InstallPath => _fileSystem.Path.Combine(_environment.GetEnvironmentVariable(ServicesPath),
            Payload.ServiceDefinition.InstallationPath);

        private string ExecutablePath =>
            _fileSystem.Path.Combine(InstallPath, Payload.ServiceDefinition.ServiceExecutable);

        public async Task Invoke()
        {
            var result = await _gitHubHttpClient.GetReleaseAsset(Payload.ServiceDefinition.Owner,
                Payload.ServiceDefinition.RepositoryName, Payload.AssetId);
            await using var memoryStream = new MemoryStream(result.File);

            IO.CreateFolders(_fileSystem, new[] { TempBasePath, AssetBasePath, ExtractPath, InstallPath });
            await IO.SaveBytesToFile(_fileSystem, AssetFilePath, result.File);
            await IO.ExtractArchiveToPath(_fileSystem, AssetFilePath, ExtractPath);
            IO.CopyFilesRecursively(_fileSystem.DirectoryInfo.FromDirectoryName(ExtractPath),
                _fileSystem.DirectoryInfo.FromDirectoryName(InstallPath));

            await _fileSystem.File.WriteAllTextAsync(_fileSystem.Path.Combine(InstallPath, "version.txt"),
                Payload.VersionName);

            if (Payload.ServiceDefinition.SetupStoreId)
            {
                var storeSettingsFile = new StoreSettingsFile { StoreSettings = _storeSettings };
                var json = JsonSerializer.Serialize(storeSettingsFile, new() { WriteIndented = true });
                await _fileSystem.File.WriteAllTextAsync(_fileSystem.Path.Combine(InstallPath, "storeid.json"), json);
            }

            if (Payload.ServiceDefinition.SetupServiceBus)
            {
                var serviceBusSettingsFile = new ServiceBusSettingsFile { ServiceBusSettings = _serviceBusSettings };
                var json = JsonSerializer.Serialize(serviceBusSettingsFile, new() { WriteIndented = true });
                await _fileSystem.File.WriteAllTextAsync(_fileSystem.Path.Combine(InstallPath, "servicebus.json"),
                    json);
            }

            var installProcess = _processProxy.Start(ExecutablePath, new[] { "install" });
            await _processProxy.WaitForExitAsync(installProcess);
            var startProcess = _processProxy.Start(ExecutablePath, new[] { "start" });
            await _processProxy.WaitForExitAsync(startProcess);

            CleanTemporaryFiles();
        }

        public InstallServicePayload Payload { get; set; }

        private void CleanTemporaryFiles()
        {
            _fileSystem.Directory.Delete(AssetBasePath, true);
        }
    }

    public record InstallServicePayload(ServiceDefinition ServiceDefinition, int AssetId, string VersionName);
}