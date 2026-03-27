using DatasetStudio.Models;
using DatasetStudio.Services;

namespace DatasetStudio.Tests;

public class WorkflowStageParserTests
{
    [Test]
    public void Parse_ReturnsStrippedDisplayNameForNumberedFolder()
    {
        WorkflowStage parsedStage = WorkflowStageParser.Parse("01_Inbox");

        Assert.That(parsedStage.Order, Is.EqualTo(1));
        Assert.That(parsedStage.FolderName, Is.EqualTo("01_Inbox"));
        Assert.That(parsedStage.DisplayName, Is.EqualTo("Inbox"));
    }

    [Test]
    public void Parse_ReturnsFolderNameAsDisplayNameWhenPrefixIsMissing()
    {
        WorkflowStage parsedStage = WorkflowStageParser.Parse("Inbox");

        Assert.That(parsedStage.Order, Is.EqualTo(int.MaxValue));
        Assert.That(parsedStage.FolderName, Is.EqualTo("Inbox"));
        Assert.That(parsedStage.DisplayName, Is.EqualTo("Inbox"));
    }

    [Test]
    public void ParseAndSort_SortsNumberedFoldersByNumericPrefix()
    {
        IReadOnlyList<WorkflowStage> parsedStages = WorkflowStageParser.ParseAndSort(new[] { "03_Ready", "01_Inbox", "02_Review" });

        Assert.That(parsedStages.Select(stage => stage.DisplayName), Is.EqualTo(new[] { "Inbox", "Review", "Ready" }));
        Assert.That(parsedStages.Select(stage => stage.Order), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void ParseAndSort_SortsSingleDigitAndMultiDigitPrefixesByNumericValue()
    {
        IReadOnlyList<WorkflowStage> parsedStages = WorkflowStageParser.ParseAndSort(new[] { "1_A", "10_B", "2_C" });

        Assert.That(parsedStages.Select(stage => stage.DisplayName), Is.EqualTo(new[] { "A", "C", "B" }));
        Assert.That(parsedStages.Select(stage => stage.Order), Is.EqualTo(new[] { 1, 2, 10 }));
    }

    [Test]
    public void ParseAndSort_SortsUnnumberedFoldersAfterNumberedFolders()
    {
        IReadOnlyList<WorkflowStage> parsedStages = WorkflowStageParser.ParseAndSort(new[] { "10_Ready", "Inbox", "02_Review", "Export" });

        Assert.That(parsedStages.Select(stage => stage.DisplayName), Is.EqualTo(new[] { "Review", "Ready", "Export", "Inbox" }));
        Assert.That(parsedStages.Select(stage => stage.Order), Is.EqualTo(new[] { 2, 10, int.MaxValue, int.MaxValue }));
    }
}
