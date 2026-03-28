using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class HuggingFaceCliService : IHuggingFaceCliService
{
    private sealed class ExecutableCommand
    {
        public ExecutableCommand(string fileName, IReadOnlyList<string> arguments)
        {
            FileName = fileName;
            Arguments = arguments;
        }

        public string FileName { get; }

        public IReadOnlyList<string> Arguments { get; }
    }

    private readonly IProcessExecutionService processExecutionService;
    private readonly string applicationSupportRootDirectoryPath;
    private readonly string huggingFaceHomeDirectoryPath;
    private readonly string managedEnvironmentDirectoryPath;
    private readonly string managedCliExecutablePath;
    private readonly string managedPythonExecutablePath;
    private readonly string modelStoreRootDirectoryPath;

    public HuggingFaceCliService(
        IProcessExecutionService processExecutionService,
        string? applicationSupportRootDirectoryPath = null)
    {
        this.processExecutionService = processExecutionService ?? throw new ArgumentNullException(nameof(processExecutionService));
        this.applicationSupportRootDirectoryPath = string.IsNullOrWhiteSpace(applicationSupportRootDirectoryPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DatasetStudio")
            : applicationSupportRootDirectoryPath;
        huggingFaceHomeDirectoryPath = Path.Combine(this.applicationSupportRootDirectoryPath, "huggingface");
        managedEnvironmentDirectoryPath = Path.Combine(this.applicationSupportRootDirectoryPath, "tools", "hf-cli");
        managedCliExecutablePath = Path.Combine(GetManagedScriptsDirectoryPath(managedEnvironmentDirectoryPath), GetPlatformExecutableName("hf"));
        managedPythonExecutablePath = Path.Combine(GetManagedScriptsDirectoryPath(managedEnvironmentDirectoryPath), GetPlatformExecutableName("python"));
        modelStoreRootDirectoryPath = Path.Combine(this.applicationSupportRootDirectoryPath, "models");
    }

    public async Task<HuggingFaceCliStatus> EnsureCliAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(managedCliExecutablePath))
        {
            ProcessExecutionResult managedVersionResult = await processExecutionService.ExecuteAsync(
                managedCliExecutablePath,
                new[] { "version" },
                environmentVariables: BuildEnvironmentVariables(),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (managedVersionResult.ExitCode == 0)
            {
                return new HuggingFaceCliStatus
                {
                    IsAvailable = true,
                    IsAppManaged = true,
                    ExecutablePath = managedCliExecutablePath,
                    StatusMessage = string.IsNullOrWhiteSpace(managedVersionResult.StandardOutput)
                        ? "Managed Hugging Face CLI is available."
                        : managedVersionResult.StandardOutput,
                };
            }
        }

        ProcessExecutionResult? systemVersionResult = await TryExecuteAsync(
            "hf",
            new[] { "version" },
            cancellationToken).ConfigureAwait(false);

        if (systemVersionResult is not null && systemVersionResult.ExitCode == 0)
        {
            return new HuggingFaceCliStatus
            {
                IsAvailable = true,
                IsAppManaged = false,
                ExecutablePath = "hf",
                StatusMessage = string.IsNullOrWhiteSpace(systemVersionResult.StandardOutput)
                    ? "System Hugging Face CLI is available."
                    : systemVersionResult.StandardOutput,
            };
        }

        ExecutableCommand pythonCommand = await ResolvePythonCommandAsync(cancellationToken).ConfigureAwait(false);
        await BootstrapManagedCliAsync(pythonCommand, cancellationToken).ConfigureAwait(false);

        ProcessExecutionResult verificationResult = await processExecutionService.ExecuteAsync(
            managedCliExecutablePath,
            new[] { "version" },
            environmentVariables: BuildEnvironmentVariables(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (verificationResult.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(verificationResult.StandardError)
                ? verificationResult.StandardOutput
                : verificationResult.StandardError;
            throw new InvalidOperationException(string.Format("The embedded Hugging Face CLI could not be verified: {0}", errorMessage));
        }

        return new HuggingFaceCliStatus
        {
            IsAvailable = true,
            IsAppManaged = true,
            ExecutablePath = managedCliExecutablePath,
            StatusMessage = string.IsNullOrWhiteSpace(verificationResult.StandardOutput)
                ? "Embedded Hugging Face CLI installed."
                : verificationResult.StandardOutput,
        };
    }

    public string GetModelInstallDirectory(AiModelInfo model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (string.IsNullOrWhiteSpace(model.RepositoryId))
        {
            return model.ModelPath;
        }

        string[] repositorySegments = model.RepositoryId
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .ToArray();

        string installDirectoryPath = modelStoreRootDirectoryPath;
        foreach (string repositorySegment in repositorySegments)
        {
            installDirectoryPath = Path.Combine(installDirectoryPath, repositorySegment);
        }

        return installDirectoryPath;
    }

    public bool IsModelInstalled(AiModelInfo model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        string installDirectoryPath = GetModelInstallDirectory(model);
        if (string.IsNullOrWhiteSpace(installDirectoryPath) || !Directory.Exists(installDirectoryPath))
        {
            return false;
        }

        foreach (string filePath in Directory.EnumerateFiles(installDirectoryPath, "*", SearchOption.AllDirectories))
        {
            string relativeFilePath = Path.GetRelativePath(installDirectoryPath, filePath);
            if (!relativeFilePath.StartsWith(".cache", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<string> DownloadModelAsync(AiModelInfo model, CancellationToken cancellationToken = default)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (string.IsNullOrWhiteSpace(model.RepositoryId))
        {
            throw new InvalidOperationException(string.Format("Model '{0}' does not define a Hugging Face repository id.", model.Id));
        }

        HuggingFaceCliStatus cliStatus = await EnsureCliAvailableAsync(cancellationToken).ConfigureAwait(false);
        string installDirectoryPath = GetModelInstallDirectory(model);
        string? installParentDirectoryPath = Path.GetDirectoryName(installDirectoryPath);
        if (!string.IsNullOrWhiteSpace(installParentDirectoryPath))
        {
            Directory.CreateDirectory(installParentDirectoryPath);
        }

        List<string> arguments = new List<string>
        {
            "download",
            model.RepositoryId,
            "--local-dir",
            installDirectoryPath,
            "--quiet",
        };

        if (!string.IsNullOrWhiteSpace(model.Revision))
        {
            arguments.Add("--revision");
            arguments.Add(model.Revision);
        }

        foreach (string includePattern in model.IncludePatterns)
        {
            arguments.Add("--include");
            arguments.Add(includePattern);
        }

        foreach (string excludePattern in model.ExcludePatterns)
        {
            arguments.Add("--exclude");
            arguments.Add(excludePattern);
        }

        ProcessExecutionResult downloadResult = await processExecutionService.ExecuteAsync(
            cliStatus.ExecutablePath,
            arguments,
            environmentVariables: BuildEnvironmentVariables(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (downloadResult.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(downloadResult.StandardError)
                ? downloadResult.StandardOutput
                : downloadResult.StandardError;
            throw new InvalidOperationException(string.Format(
                "Hugging Face download failed for '{0}': {1}",
                model.DisplayName,
                errorMessage));
        }

        return installDirectoryPath;
    }

    private async Task BootstrapManagedCliAsync(ExecutableCommand pythonCommand, CancellationToken cancellationToken)
    {
        string? toolParentDirectoryPath = Path.GetDirectoryName(managedEnvironmentDirectoryPath);
        if (!string.IsNullOrWhiteSpace(toolParentDirectoryPath))
        {
            Directory.CreateDirectory(toolParentDirectoryPath);
        }

        List<string> createEnvironmentArguments = new List<string>(pythonCommand.Arguments)
        {
            "-m",
            "venv",
            managedEnvironmentDirectoryPath,
        };
        ProcessExecutionResult createEnvironmentResult = await processExecutionService.ExecuteAsync(
            pythonCommand.FileName,
            createEnvironmentArguments,
            environmentVariables: BuildEnvironmentVariables(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (createEnvironmentResult.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(createEnvironmentResult.StandardError)
                ? createEnvironmentResult.StandardOutput
                : createEnvironmentResult.StandardError;
            throw new InvalidOperationException(string.Format("Could not create the embedded Hugging Face CLI environment: {0}", errorMessage));
        }

        ProcessExecutionResult pipUpgradeResult = await processExecutionService.ExecuteAsync(
            managedPythonExecutablePath,
            new[] { "-m", "pip", "install", "--upgrade", "pip" },
            environmentVariables: BuildEnvironmentVariables(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (pipUpgradeResult.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(pipUpgradeResult.StandardError)
                ? pipUpgradeResult.StandardOutput
                : pipUpgradeResult.StandardError;
            throw new InvalidOperationException(string.Format("Could not update pip for the embedded Hugging Face CLI: {0}", errorMessage));
        }

        ProcessExecutionResult installResult = await processExecutionService.ExecuteAsync(
            managedPythonExecutablePath,
            new[] { "-m", "pip", "install", "--upgrade", "huggingface_hub" },
            environmentVariables: BuildEnvironmentVariables(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (installResult.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(installResult.StandardError)
                ? installResult.StandardOutput
                : installResult.StandardError;
            throw new InvalidOperationException(string.Format("Could not install the embedded Hugging Face CLI: {0}", errorMessage));
        }
    }

    private async Task<ExecutableCommand> ResolvePythonCommandAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ExecutableCommand> candidates = new[]
        {
            new ExecutableCommand("py", new[] { "-3" }),
            new ExecutableCommand("python", Array.Empty<string>()),
            new ExecutableCommand("py", Array.Empty<string>()),
        };

        foreach (ExecutableCommand candidate in candidates)
        {
            List<string> probeArguments = new List<string>(candidate.Arguments)
            {
                "--version",
            };
            ProcessExecutionResult? probeResult = await TryExecuteAsync(
                candidate.FileName,
                probeArguments,
                cancellationToken).ConfigureAwait(false);
            if (probeResult is not null && probeResult.ExitCode == 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "DatasetStudio could not find a Python runtime to bootstrap the embedded Hugging Face CLI. Install Python 3 or provide a system hf executable.");
    }

    private async Task<ProcessExecutionResult?> TryExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await processExecutionService.ExecuteAsync(
                fileName,
                arguments,
                environmentVariables: BuildEnvironmentVariables(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private Dictionary<string, string?> BuildEnvironmentVariables()
    {
        Directory.CreateDirectory(applicationSupportRootDirectoryPath);
        Directory.CreateDirectory(huggingFaceHomeDirectoryPath);
        Directory.CreateDirectory(modelStoreRootDirectoryPath);

        Dictionary<string, string?> environmentVariables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HF_HOME"] = huggingFaceHomeDirectoryPath,
        };

        return environmentVariables;
    }

    private static string GetManagedScriptsDirectoryPath(string environmentDirectoryPath)
    {
        return OperatingSystem.IsWindows()
            ? Path.Combine(environmentDirectoryPath, "Scripts")
            : Path.Combine(environmentDirectoryPath, "bin");
    }

    private static string GetPlatformExecutableName(string baseName)
    {
        return OperatingSystem.IsWindows()
            ? string.Format("{0}.exe", baseName)
            : baseName;
    }

    private static string SanitizePathSegment(string pathSegment)
    {
        char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        string sanitizedPathSegment = new string(pathSegment
            .Select(character => invalidFileNameCharacters.Contains(character) ? '_' : character)
            .ToArray());
        return sanitizedPathSegment;
    }
}
