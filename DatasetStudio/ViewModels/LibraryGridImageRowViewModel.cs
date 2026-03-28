using System;
using System.Collections.Generic;
using System.Linq;

namespace DatasetStudio.ViewModels;

public sealed class LibraryGridImageRowViewModel
{
    public LibraryGridImageRowViewModel(IEnumerable<LibraryGridImageViewModel> images)
    {
        if (images is null)
        {
            throw new ArgumentNullException(nameof(images));
        }

        Images = images.ToArray();
    }

    public IReadOnlyList<LibraryGridImageViewModel> Images { get; }
}
