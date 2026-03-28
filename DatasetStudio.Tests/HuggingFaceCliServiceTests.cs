using DatasetStudio.Models;
using DatasetStudio.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatasetStudio.Tests;

[TestFixture]
public class HuggingFaceCliServiceTests
{
    [Test]
    public async Task DownloadModelAsync_UsesSystemCliWithExpectedArgumentsAndEnvironment()
    {
        string tempRootDirectoryPath = Path.Combine(Path.GetTempPath(), "DatasetStudioTests", Guid.NewGuid().ToString("N"));
        FakeProcessExecutionService processExecutionService = new FakeProcessExecutionService();
        processExecutionService.Handler = invocation =>
        {
            if (string.Equals(invocation.FileName, "hf", StringComparison.OrdinalIgnoreCase)
                && invocation.Arguments.SequenceEqual(new[] { "version" }))
            {
                return new ProcessExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = "1.0.0",
                };
            }

            return new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = invocation.Arguments.Last(),
            };
        };

        HuggingFaceCliService service = new HuggingFaceCliService(processExecutionService, tempRootDirectoryPath);
        AiModelInfo model = new AiModelInfo
        {
            Id = "wd-eva02-large",
            DisplayName = "WD EVA02 Large",
            RepositoryId = "SmilingWolf/wd-eva02-large-tagger-v3",
            IncludePatterns = new[] { "*.onnx", "selected_tags.csv" },
            ExcludePatterns = new[] { "*.bin" },
        };

        string installDirectoryPath = await service.DownloadModelAsync(model).ConfigureAwait(false);

        ProcessInvocation downloadInvocation = processExecutionService.Invocations.Last();
        Assert.That(downloadInvocation.FileName, Is.EqualTo("hf"));
        Assert.That(downloadInvocation.Arguments, Is.EqualTo(new[]
        {
            "download",
            "SmilingWolf/wd-eva02-large-tagger-v3",
            "--local-dir",
            installDirectoryPath,
            "--quiet",
            "--include",
            "*.onnx",
            "--include",
            "selected_tags.csv",
            "--exclude",
            "*.bin",
        }));
        Assert.That(downloadInvocation.EnvironmentVariables["HF_HOME"], Is.EqualTo(Path.Combine(tempRootDirectoryPath, "huggingface")));
    }

    [Test]
    public async Task EnsureCliAvailableAsync_BootstrapsManagedCliWhenSystemCliIsMissing()
    {
        string tempRootDirectoryPath = Path.Combine(Path.GetTempPath(), "DatasetStudioTests", Guid.NewGuid().ToString("N"));
        string managedScriptsDirectoryPath = OperatingSystem.IsWindows()
            ? Path.Combine(tempRootDirectoryPath, "tools", "hf-cli", "Scripts")
            : Path.Combine(tempRootDirectoryPath, "tools", "hf-cli", "bin");
        string managedPythonExecutablePath = Path.Combine(
            managedScriptsDirectoryPath,
            OperatingSystem.IsWindows() ? "python.exe" : "python");
        string managedCliExecutablePath = Path.Combine(
            managedScriptsDirectoryPath,
            OperatingSystem.IsWindows() ? "hf.exe" : "hf");

        FakeProcessExecutionService processExecutionService = new FakeProcessExecutionService();
        processExecutionService.Handler = invocation =>
        {
            if (string.Equals(invocation.FileName, "hf", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("hf not found");
            }

            if (string.Equals(invocation.FileName, "py", StringComparison.OrdinalIgnoreCase)
                && invocation.Arguments.SequenceEqual(new[] { "-3", "--version" }))
            {
                return new ProcessExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = "Python 3.11.8",
                };
            }

            if (string.Equals(invocation.FileName, "py", StringComparison.OrdinalIgnoreCase)
                && invocation.Arguments.Count >= 4
                && string.Equals(invocation.Arguments[0], "-3", StringComparison.Ordinal))
            {
                return new ProcessExecutionResult
                {
                    ExitCode = 0,
                };
            }

            if (string.Equals(invocation.FileName, managedPythonExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessExecutionResult
                {
                    ExitCode = 0,
                };
            }

            if (string.Equals(invocation.FileName, managedCliExecutablePath, StringComparison.OrdinalIgnoreCase)
                && invocation.Arguments.SequenceEqual(new[] { "version" }))
            {
                return new ProcessExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = "1.0.0",
                };
            }

            throw new AssertionException(string.Format(
                "Unexpected command: {0} {1}",
                invocation.FileName,
                string.Join(" ", invocation.Arguments)));
        };

        HuggingFaceCliService service = new HuggingFaceCliService(processExecutionService, tempRootDirectoryPath);

        HuggingFaceCliStatus status = await service.EnsureCliAvailableAsync().ConfigureAwait(false);

        Assert.That(status.IsAvailable, Is.True);
        Assert.That(status.IsAppManaged, Is.True);
        Assert.That(status.ExecutablePath, Is.EqualTo(managedCliExecutablePath));
        Assert.That(processExecutionService.Invocations.Select(invocation => invocation.FileName), Does.Contain(managedPythonExecutablePath));
        Assert.That(processExecutionService.Invocations.Select(invocation => invocation.FileName), Does.Contain(managedCliExecutablePath));
    }

    private sealed class FakeProcessExecutionService : IProcessExecutionService
    {
        public Func<ProcessInvocation, ProcessExecutionResult> Handler { get; set; } = invocation =>
        {
            throw new AssertionException(string.Format(
                "Unexpected command: {0} {1}",
                invocation.FileName,
                string.Join(" ", invocation.Arguments)));
        };

        public List<ProcessInvocation> Invocations { get; } = new List<ProcessInvocation>();

        public Task<ProcessExecutionResult> ExecuteAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string?>? environmentVariables = null,
            CancellationToken cancellationToken = default)
        {
            ProcessInvocation invocation = new ProcessInvocation(
                fileName,
                arguments.ToArray(),
                workingDirectory,
                environmentVariables is null
                    ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(environmentVariables, StringComparer.OrdinalIgnoreCase));
            Invocations.Add(invocation);
            return Task.FromResult(Handler(invocation));
        }
    }

    private sealed record ProcessInvocation(
        string FileName,
        IReadOnlyList<string> Arguments,
        string? WorkingDirectory,
        IReadOnlyDictionary<string, string?> EnvironmentVariables);
}
