using DatasetStudio.Models;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Tests;

[TestFixture]
public class ProjectOverviewImageViewModelTests
{
    [Test]
    public void TagsPreview_ReflectsUpdatedTags()
    {
        ProjectOverviewImageViewModel viewModel = new ProjectOverviewImageViewModel(
            "C:\\images\\cat.png",
            "cat.png",
            "C:\\images\\cat.txt",
            new[] { "cat" },
            TagStatus.Ready,
            null);

        Assert.That(viewModel.TagsPreview, Is.EqualTo("cat"));

        viewModel.Tags = new[] { "cat", "orange", "studio" };

        Assert.That(viewModel.TagsPreview, Is.EqualTo("cat, orange, studio"));
    }
}
