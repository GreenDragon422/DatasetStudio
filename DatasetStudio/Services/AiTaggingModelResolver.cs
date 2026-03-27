using DatasetStudio.Models;

namespace DatasetStudio.Services;

public static class AiTaggingModelResolver
{
    public static string? ResolveConfiguredModelName(Project project)
    {
        if (!string.IsNullOrWhiteSpace(project.State.SelectedAiModelName))
        {
            return project.State.SelectedAiModelName;
        }

        if (!string.IsNullOrWhiteSpace(project.AiModelName))
        {
            return project.AiModelName;
        }

        return null;
    }
}
