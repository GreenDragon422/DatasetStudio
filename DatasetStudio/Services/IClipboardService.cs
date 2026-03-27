using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface IClipboardService
{
    Task CopyTagsAsync(IReadOnlyList<string> tags);

    Task<IReadOnlyList<string>> PasteTagsAsync();
}
