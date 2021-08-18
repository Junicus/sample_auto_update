using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Threading.Tasks;
using Coravel.Invocable;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Options;

namespace IRSI.Services.AutoUpdate.Jobs
{
    public class InstallServiceJob : IInvocable, IInvocableWithPayload<InstallServicePayload>
    {
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentProxy _environment;
        private readonly IProcessProxy _processProxy;

        private string SERVICES_PATH = "SERVICES_PATH";
        private string TempBasePath => _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        private string AssetBasePath => _fileSystem.Path.Combine(TempBasePath, $"{Payload.AssetId}");
        private string AssetFilePath => _fileSystem.Path.Combine(AssetBasePath, $"{Payload.AssetId}.zip");
        private string ExtractPath => _fileSystem.Path.Combine(AssetBasePath, "extract");

        private string InstallPath => _fileSystem.Path.Combine(_environment.GetEnvironmentVariable(SERVICES_PATH),
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

            CreateRequiredFolders();
            await SaveTempFile(memoryStream);
            await ExtractTempFileAsync();
            CopyTempFilesToInstallationFolder();

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

        private void CreateRequiredFolders()
        {
            if (!_fileSystem.Directory.Exists(TempBasePath)) _fileSystem.Directory.CreateDirectory(TempBasePath);
            if (!_fileSystem.Directory.Exists(AssetBasePath)) _fileSystem.Directory.CreateDirectory(AssetBasePath);
            if (!_fileSystem.Directory.Exists(ExtractPath)) _fileSystem.Directory.CreateDirectory(ExtractPath);
            if (!_fileSystem.Directory.Exists(InstallPath)) _fileSystem.Directory.CreateDirectory(InstallPath);
        }

        private async Task ExtractTempFileAsync()
        {
            await using var archiveFile = _fileSystem.File.OpenRead(AssetFilePath);
            var archive = new ZipArchive(archiveFile, ZipArchiveMode.Read, true);
            foreach (var entry in archive.Entries)
            {
                var destination = Path.GetFullPath(Path.Combine(ExtractPath, entry.FullName));
                var archiveStream = entry.Open();
                var destinationFile = _fileSystem.File.Create(destination);
                await archiveStream.CopyToAsync(destinationFile);

                destinationFile.Close();
                archiveStream.Close();
            }
        }

        private async Task SaveTempFile(Stream memoryStream)
        {
            await using var fs = _fileSystem.FileStream.Create(AssetFilePath, FileMode.Create);
            await memoryStream.CopyToAsync(fs);
        }

        private void CopyTempFilesToInstallationFolder()
        {
            var files = _fileSystem.Directory.GetFiles(ExtractPath);
            foreach (var fileName in files)
            {
                var destination =
                    _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(InstallPath,
                        _fileSystem.Path.GetFileName(fileName)));
                _fileSystem.File.Copy(fileName, destination);
            }
        }

        public InstallServicePayload Payload { get; set; }
    }

    public record InstallServicePayload(ServiceDefinition ServiceDefinition, int AssetId, string VersionName);
}

//var t = await _gitHubHttpClient.GetReleaseAsset(serviceDefinition.Owner, serviceDefinition.RepositoryName,
//    response.Release.Assets.First().Id);