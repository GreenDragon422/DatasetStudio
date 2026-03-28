using System.Collections.Generic;

namespace DatasetStudio.ViewModels;

public static class HintBarItemListComparer
{
    public static bool AreEquivalent(IReadOnlyList<HintBarItemViewModel> first, IReadOnlyList<HintBarItemViewModel> second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first.Count != second.Count)
        {
            return false;
        }

        for (int index = 0; index < first.Count; index++)
        {
            HintBarItemViewModel firstItem = first[index];
            HintBarItemViewModel secondItem = second[index];
            if (firstItem.KeyText != secondItem.KeyText
                || firstItem.Description != secondItem.Description)
            {
                return false;
            }
        }

        return true;
    }
}
