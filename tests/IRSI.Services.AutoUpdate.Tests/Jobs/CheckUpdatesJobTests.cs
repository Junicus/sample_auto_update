using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using Coravel.Queuing.Interfaces;
using FakeItEasy;
using FluentAssertions;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Jobs;
using IRSI.Services.AutoUpdate.Models;
using IRSI.Services.AutoUpdate.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace IRSI.Services.AutoUpdate.Tests.Jobs
{
    public class CheckUpdatesJobTests
    {
        private readonly IEnvironmentProxy _environmentProxy;
        private readonly IFileSystem _fileSystem;
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IOptions<ServiceSettings> _optionsMock;
        private readonly IQueue _queue;

        public CheckUpdatesJobTests()
        {
            ServiceSettings settings = new()
            {
                ServiceDefinitions = new()
                {
                    new() { Owner = "Owner", RepositoryName = "RepositoryName", InstallationPath = "InstallPath" }
                }
            };
            _optionsMock = A.Fake<IOptions<ServiceSettings>>();
            A.CallTo(() => _optionsMock.Value).Returns(settings);

            _gitHubHttpClient = A.Fake<IGitHubHttpClient>();
            A.CallTo(() => _gitHubHttpClient.GetLatestRelease("Owner", "RepositoryName"))
                .ReturnsLazily(() => GetLatestReleaseResult.SuccessResult(new()
                {
                    Id = 1,
                    Name = "v2",
                    TagName = "v2",
                    Assets = new()
                    {
                        new() { Id = 123, Name = "data.zip", ContentType = "raw" }
                    }
                }));

            _queue = A.Fake<IQueue>();

            _environmentProxy = A.Fake<IEnvironmentProxy>();
            A.CallTo(() => _environmentProxy.GetEnvironmentVariable("SERVICES_PATH")).Returns(@"C:\TServices");

            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"C:\Temp", new MockDirectoryData() },
                { @"C:\TServices\SomeService", new MockDirectoryData() },
                { @"C:\TServices\SomeService\version.txt", new MockFileData("v1") }
            });
        }

        [Fact]
        public async Task When_Invoke_GetEnvironmentVariable()
        {
            var environmentProxy = A.Fake<IEnvironmentProxy>();
            A.CallTo(() => environmentProxy.GetEnvironmentVariable("SERVICES_PATH")).Returns(@"C:\TService");

            var sut = new CheckUpdatesJob(_optionsMock, _gitHubHttpClient, _queue, _fileSystem, environmentProxy);
            await sut.Invoke();

            A.CallTo(() => environmentProxy.GetEnvironmentVariable("SERVICES_PATH")).MustHaveHappened();
        }

        [Fact]
        public async Task When_Invoke_GetEnvironmentVariable_ThrowsIfNotSet()
        {
            var environmentProxy = A.Fake<IEnvironmentProxy>();

            var sut = new CheckUpdatesJob(_optionsMock, _gitHubHttpClient, _queue, _fileSystem, environmentProxy);
            await FluentActions.Awaiting(async () => await sut.Invoke()).Should().ThrowAsync<Exception>()
                .WithMessage("Environment Variable SERVICES_PATH not set");
        }

        [Fact]
        public async Task When_Invoke_CallsGitHubApi()
        {
            var gitHubHttpClient = A.Fake<IGitHubHttpClient>();
            A.CallTo(() => gitHubHttpClient.GetLatestRelease("Owner", "RepositoryName"))
                .ReturnsLazily(() => GetLatestReleaseResult.SuccessResult(new()
                {
                    Id = 1,
                    Name = "v1",
                    TagName = "v1",
                    Assets = new()
                    {
                        new() { Id = 123, Name = "data.zip", ContentType = "raw" }
                    }
                }));

            var sut = new CheckUpdatesJob(_optionsMock, gitHubHttpClient, _queue, _fileSystem, _environmentProxy);
            await sut.Invoke();

            A.CallTo(() => gitHubHttpClient.GetLatestRelease("Owner", "RepositoryName"))
                .MustHaveHappened();
        }

        [Fact]
        public async Task When_Invoke_InstallJob_GetsQueued()
        {
            var queue = A.Fake<IQueue>();
            var sut = new CheckUpdatesJob(_optionsMock, _gitHubHttpClient, queue, _fileSystem, _environmentProxy);
            await sut.Invoke();

            A.CallTo(() =>
                    queue.QueueInvocableWithPayload<InstallServiceJob, InstallServicePayload>(
                        A<InstallServicePayload>.That.Matches(x =>
                            x.AssetId == 123
                            && x.VersionName == "v2"
                            && x.ServiceDefinition.Owner == "Owner"
                            && x.ServiceDefinition.RepositoryName == "RepositoryName"
                            && x.ServiceDefinition.InstallationPath == "InstallPath")))
                .MustHaveHappened();
        }

        [Fact]
        public async Task When_Invoke_UpdateJob_GetsQueued()
        {
            ServiceSettings settings = new()
            {
                ServiceDefinitions = new()
                {
                    new() { Owner = "Owner", RepositoryName = "RepositoryName", InstallationPath = "SomeService" }
                }
            };
            var optionsMock = A.Fake<IOptions<ServiceSettings>>();
            A.CallTo(() => optionsMock.Value).Returns(settings);
            var queue = A.Fake<IQueue>();

            var sut = new CheckUpdatesJob(optionsMock, _gitHubHttpClient, queue, _fileSystem, _environmentProxy);
            await sut.Invoke();

            A.CallTo(() =>
                    queue.QueueInvocableWithPayload<UpdateServiceJob, UpdateServicePayload>(
                        A<UpdateServicePayload>.That.Matches(x =>
                            x.AssetId == 123
                            && x.VersionName == "v2"
                            && x.ServiceDefinition.Owner == "Owner"
                            && x.ServiceDefinition.RepositoryName == "RepositoryName"
                            && x.ServiceDefinition.InstallationPath == "SomeService")))
                .MustHaveHappened();
        }
    }
}