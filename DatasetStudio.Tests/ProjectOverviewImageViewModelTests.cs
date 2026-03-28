using DatasetStudio.Models;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Tests;

[TestFixture]
public class ProjectOverviewImageViewModelTests
{
    [Test]
    public void SelectionChipText_FollowsSelectionState()
    {
        ProjectOverviewImageViewModel viewModel = new ProjectOverviewImageViewModel(
            "C:\\images\\cat.png",
            "cat.png",
            "C:\\images\\cat.txt",
            Array.Empty<string>(),
            TagStatus.Ready,
            null);

        Assert.That(viewModel.SelectionChipText, Is.EqualTo("Select"));

        viewModel.IsSelected = true;

        Assert.That(viewModel.SelectionChipText, Is.EqualTo("Selected"));
    }
}
