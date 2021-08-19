﻿using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Threading.Tasks;
using Coravel.Invocable;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Options;
using IRSI.Services.AutoUpdate.Utilities;

namespace IRSI.Services.AutoUpdate.Jobs
{
    public class InstallServiceJob : IInvocable, IInvocableWithPayload<InstallServicePayload>
    {
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentProxy _environment;
        private readonly IProcessProxy _processProxy;

        private const string ServicesPath = "SERVICES_PATH";

        private string TempBasePath => _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        private string AssetBasePath => _fileSystem.Path.Combine(TempBasePath, $"{Payload.AssetId}");
        private string AssetFilePath => _fileSystem.Path.Combine(AssetBasePath, $"{Payload.AssetId}.zip");
        private string ExtractPath => _fileSystem.Path.Combine(AssetBasePath, "extract");

        private string InstallPath => _fileSystem.Path.Combine(_environment.GetEnvironmentVariable(ServicesPath),
            Payload.ServiceDefinition.InstallationPath);

        private string ExecutablePath =>
            _fileSystem.Path.Combine(InstallPath, Payload.ServiceDefinition.ServiceExecutable);

        public InstallServiceJob(IGitHubHttpClient gitHubHttpClient, IFileSystem fileSystem,
            IEnvironmentProxy environment, IProcessProxy processProxy)
        {
            _gitHubHttpClient = gitHubHttpClient;
            _fileSystem = fileSystem;
            _environment = environment;
            _processProxy = processProxy;
        }

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
            var installProcess = _processProxy.Start(ExecutablePath, new[] { "install" });
            await _processProxy.WaitForExitAsync(installProcess);
            var startProcess = _processProxy.Start(ExecutablePath, new[] { "start" });
            await _processProxy.WaitForExitAsync(startProcess);

            CleanTemporaryFiles();
        }

        private void CleanTemporaryFiles()
        {
            _fileSystem.Directory.Delete(AssetBasePath, true);
        }

        public InstallServicePayload Payload { get; set; }
    }

    public record InstallServicePayload(ServiceDefinition ServiceDefinition, int AssetId, string VersionName);
}