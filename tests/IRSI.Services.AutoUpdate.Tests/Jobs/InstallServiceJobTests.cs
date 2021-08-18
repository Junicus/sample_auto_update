using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.Jobs;
using IRSI.Services.AutoUpdate.Models;
using IRSI.Services.AutoUpdate.Options;
using IRSI.Services.AutoUpdate.Services;
using Xunit;

namespace IRSI.Services.AutoUpdate.Tests.Jobs
{
    public class InstallServiceJobTests
    {
        private readonly ServiceDefinition _serviceDefinition;
        private readonly IGitHubHttpClient _gitHubHttpClient;
        private readonly IEnvironmentProxy _environmentProxy;
        private readonly IFileSystem _fileSystem;
        private readonly IProcessProxy _processProxy;
        private readonly string _servicesBasePath;

        public InstallServiceJobTests()
        {
            _serviceDefinition = new()
            {
                Owner = "Owner", RepositoryName = "RepoName", InstallationPath = "RepoName",
                ServiceExecutable = "RepoName.exe"
            };

            _gitHubHttpClient = A.Fake<IGitHubHttpClient>();
            var fileData = File.ReadAllBytes("temp.zip");
            A.CallTo(() => _gitHubHttpClient.GetReleaseAsset(A<string>._, A<string>._, A<int>._))
                .ReturnsLazily(() => GetReleaseAssetResult.SuccessResult(fileData));

            _environmentProxy = A.Fake<IEnvironmentProxy>();
            _servicesBasePath = "C:\\TService";
            A.CallTo(() => _environmentProxy.GetEnvironmentVariable(A<string>._)).Returns(_servicesBasePath);

            _processProxy = A.Fake<IProcessProxy>();
            A.CallTo(() => _processProxy.WaitForExitAsync(A<Process>._)).ReturnsLazily(() => Task.CompletedTask);

            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"C:\Temp", new MockDirectoryData() }
            });
        }

        [Fact]
        public async Task When_Invoked_CallsGitHubApi()
        {
            const int expectedAssetId = 123;
            var gitHubHttpClient = A.Fake<IGitHubHttpClient>();
            var fileData = await File.ReadAllBytesAsync("temp.zip");
            A.CallTo(() => gitHubHttpClient.GetReleaseAsset(A<string>._, A<string>._, A<int>._))
                .ReturnsLazily(() => GetReleaseAssetResult.SuccessResult(fileData));

            var sut = new InstallServiceJob(gitHubHttpClient, _fileSystem, _environmentProxy, _processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v1")
            };
            await sut.Invoke();

            A.CallTo(() => gitHubHttpClient.GetReleaseAsset("Owner", "RepoName", expectedAssetId))
                .MustHaveHappened();
        }

        [Fact]
        public async Task When_Invoked_InstallationFolder_Created()
        {
            const int expectedAssetId = 123;
            var sut = new InstallServiceJob(_gitHubHttpClient, _fileSystem, _environmentProxy, _processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v1")
            };
            await sut.Invoke();

            _fileSystem.Directory.Exists(Path.Combine(_servicesBasePath, _serviceDefinition.InstallationPath)).Should()
                .BeTrue();
        }

        [Fact]
        public async Task When_Invoked_TemporaryAssetFolder_Removed()
        {
            const int expectedAssetId = 123;
            var sut = new InstallServiceJob(_gitHubHttpClient, _fileSystem, _environmentProxy, _processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v1")
            };
            await sut.Invoke();

            _fileSystem.Directory
                .Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp", expectedAssetId.ToString()))
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task When_Invoked_VersionText_Created()
        {
            const int expectedAssetId = 123;
            var sut = new InstallServiceJob(_gitHubHttpClient, _fileSystem, _environmentProxy, _processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v2.1")
            };
            await sut.Invoke();

            _fileSystem.File.Exists(@"C:\TService\RepoName\version.txt").Should().BeTrue();
            _fileSystem.File.ReadAllTextAsync(@"C:\TService\RepoName\version.txt").Result.Should().Be("v2.1");
        }

        [Fact]
        public async Task When_Invoked_CallsInstallCommand()
        {
            const int expectedAssetId = 123;
            var processProxy = A.Fake<IProcessProxy>();

            var sut = new InstallServiceJob(_gitHubHttpClient, _fileSystem, _environmentProxy, processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v1")
            };
            await sut.Invoke();

            A.CallTo(() => processProxy.Start(@"C:\TService\RepoName\RepoName.exe",
                A<string[]>.That.Matches(x => x.ToList().Contains("install")))).MustHaveHappened();
        }

        [Fact]
        public async Task When_Invoked_CallsStartCommand()
        {
            const int expectedAssetId = 123;
            var processProxy = A.Fake<IProcessProxy>();

            var sut = new InstallServiceJob(_gitHubHttpClient, _fileSystem, _environmentProxy, processProxy)
            {
                Payload = new(_serviceDefinition, expectedAssetId, "v1")
            };
            await sut.Invoke();

            A.CallTo(() => processProxy.Start(@"C:\TService\RepoName\RepoName.exe",
                A<string[]>.That.Matches(x => x.ToList().Contains("start")))).MustHaveHappened();
        }
    }
}