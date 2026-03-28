using System;
using System.Collections.Generic;
using System.Linq;

namespace DatasetStudio.ViewModels;

public sealed class ProjectOverviewImageRowViewModel
{
    public ProjectOverviewImageRowViewModel(IEnumerable<ProjectOverviewImageViewModel> images)
    {
        if (images is null)
        {
            throw new ArgumentNullException(nameof(images));
        }

        Images = images.ToArray();
    }

    public IReadOnlyList<ProjectOverviewImageViewModel> Images { get; }
}
