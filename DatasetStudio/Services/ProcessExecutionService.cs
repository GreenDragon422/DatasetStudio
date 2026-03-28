using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class ProcessExecutionService : IProcessExecutionService
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A process executable path is required.", nameof(fileName));
        }

        ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            processStartInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach ((string key, string? value) in environmentVariables)
            {
                if (value is null)
                {
                    processStartInfo.Environment.Remove(key);
                    continue;
                }

                processStartInfo.Environment[key] = value;
            }
        }

        using Process process = new Process
        {
            StartInfo = processStartInfo,
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(string.Format("Could not start process '{0}'.", fileName));
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(string.Format("Could not start process '{0}': {1}", fileName, exception.Message), exception);
        }

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        ProcessExecutionResult result = new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = (await standardOutputTask.ConfigureAwait(false)).Trim(),
            StandardError = (await standardErrorTask.ConfigureAwait(false)).Trim(),
        };

        return result;
    }
}
