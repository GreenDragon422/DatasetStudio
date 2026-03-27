using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatasetStudio.Services;

public static class WorkflowStageParser
{
    private static readonly Regex NumericPrefixPattern = new(@"^(?<order>\d+)[_-](?<name>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WorkflowStage Parse(string folderName)
    {
        if (folderName is null)
        {
            throw new ArgumentNullException(nameof(folderName));
        }

        Match match = NumericPrefixPattern.Match(folderName);
        if (!match.Success)
        {
            return new WorkflowStage
            {
                Order = int.MaxValue,
                FolderName = folderName,
                DisplayName = folderName,
            };
        }

        string orderText = match.Groups["order"].Value;
        string displayName = match.Groups["name"].Value;
        int order = int.Parse(orderText);

        return new WorkflowStage
        {
            Order = order,
            FolderName = folderName,
            DisplayName = displayName,
        };
    }

    public static IReadOnlyList<WorkflowStage> ParseAndSort(IEnumerable<string> folderNames)
    {
        if (folderNames is null)
        {
            throw new ArgumentNullException(nameof(folderNames));
        }

        List<WorkflowStage> stages = folderNames
            .Select(Parse)
            .OrderBy(stage => stage.Order == int.MaxValue ? 1 : 0)
            .ThenBy(stage => stage.Order)
            .ThenBy(stage => stage.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stage => stage.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return stages;
    }
}
