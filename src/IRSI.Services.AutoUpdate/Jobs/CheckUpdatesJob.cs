using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Coravel.Invocable;
using Coravel.Queuing.Interfaces;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Common.Models;
using IRSI.Services.AutoUpdate.Options;
using Microsoft.Extensions.Options;

namespace IRSI.Services.AutoUpdate.Jobs
{
    public class CheckUpdatesJob : IInvocable
    {
        private readonly IEnvironmentProxy _environment;
        private readonly IFileSystem _fileSystem;
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IQueue _queue;
        private readonly ServiceSettings _serviceSettings;

        private readonly string SERVICES_PATH = "SERVICES_PATH";

        public CheckUpdatesJob(IOptions<ServiceSettings> serviceSettingsOptions, IGitHubHttpClient gitHubHttpClient,
            IQueue queue, IFileSystem fileSystem, IEnvironmentProxy environment)
        {
            _serviceSettings = serviceSettingsOptions.Value;
            _gitHubHttpClient = gitHubHttpClient;
            _queue = queue;
            _fileSystem = fileSystem;
            _environment = environment;
        }

        public async Task Invoke()
        {
            foreach (var serviceDefinition in _serviceSettings.ServiceDefinitions)
            {
                var baseServicePath = _environment.GetEnvironmentVariable(SERVICES_PATH);
                if (string.IsNullOrEmpty(baseServicePath)) throw new($"Environment Variable {SERVICES_PATH} not set");

                var response =
                    await _gitHubHttpClient.GetLatestRelease(serviceDefinition.Owner,
                        serviceDefinition.RepositoryName);

                var servicePath = _fileSystem.Path.Combine(baseServicePath, serviceDefinition.InstallationPath);
                if (!_fileSystem.Directory.Exists(servicePath))
                {
                    _fileSystem.Directory.CreateDirectory(servicePath);
                    if (response.Status == ResultStatus.Success)
                        _queue.QueueInvocableWithPayload<InstallServiceJob, InstallServicePayload>(
                            new(serviceDefinition, response.Release.Assets.First().Id, response.Release.Name));
                }
                else
                {
                    var version =
                        await _fileSystem.File.ReadAllTextAsync(_fileSystem.Path.Combine(servicePath, "version.txt"));
                    if (version != response.Release.Name)
                        _queue.QueueInvocableWithPayload<UpdateServiceJob, UpdateServicePayload>(new(serviceDefinition,
                            response.Release.Assets.First().Id, response.Release.Name));
                }
            }
        }
    }
}