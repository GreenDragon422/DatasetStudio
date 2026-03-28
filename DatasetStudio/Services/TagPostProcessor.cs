using DatasetStudio.Models;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DatasetStudio.Services;

public sealed class TagPostProcessor
{
    private static readonly HashSet<string> Kaomojis = new HashSet<string>(StringComparer.Ordinal)
    {
        "0_0",
        "(o)_(o)",
        "+_+",
        "+_-",
        "._.",
        "<o>_<o>",
        "<|>_<|>",
        "=_=",
        ">_<",
        "3_3",
        "6_9",
        ">_o",
        "@_@",
        "^_^",
        "o_o",
        "u_u",
        "x_x",
        "|_|",
        "||_||",
    };

    public IReadOnlyList<TaggerLabelDefinition> LoadLabels(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("The tag CSV file was not found.", csvPath);
        }

        List<TaggerLabelDefinition> labels = new List<TaggerLabelDefinition>();
        using TextFieldParser parser = new TextFieldParser(csvPath);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        bool isFirstRow = true;
        while (!parser.EndOfData)
        {
            string[]? parts = parser.ReadFields();
            if (parts is null || parts.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (isFirstRow)
            {
                isFirstRow = false;
                continue;
            }

            if (parts.Length < 4)
            {
                continue;
            }

            string sourceName = parts[1].Trim();
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int categoryValue))
            {
                categoryValue = -1;
            }

            long sampleCount = 0;
            long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleCount);

            TaggerLabelDefinition label = new TaggerLabelDefinition
            {
                Index = labels.Count,
                SourceName = sourceName,
                ExportName = NormalizeExportName(sourceName),
                Category = MapCategory(categoryValue),
                SampleCount = sampleCount,
            };
            labels.Add(label);
        }

        return labels;
    }

    public ImageTaggingResult CreateResult(
        TaggerModelConfig modelConfig,
        IReadOnlyList<TaggerLabelDefinition> labels,
        ReadOnlySpan<float> scores)
    {
        if (labels.Count != scores.Length)
        {
            throw new InvalidOperationException("The model output width does not match the loaded tag CSV.");
        }

        List<ImageTagScore> ratingTags = new List<ImageTagScore>();
        List<ImageTagScore> generalTags = new List<ImageTagScore>();
        List<ImageTagScore> characterTags = new List<ImageTagScore>();

        for (int tagIndex = 0; tagIndex < labels.Count; tagIndex += 1)
        {
            TaggerLabelDefinition label = labels[tagIndex];
            float score = scores[tagIndex];
            ImageTagScore tagScore = new ImageTagScore
            {
                SourceName = label.SourceName,
                ExportName = label.ExportName,
                Category = label.Category,
                Score = score,
            };

            if (label.Category == ImageTagCategory.Rating)
            {
                ratingTags.Add(tagScore);
                continue;
            }

            if (label.Category == ImageTagCategory.General && score >= modelConfig.GeneralThreshold)
            {
                generalTags.Add(tagScore);
                continue;
            }

            if (label.Category == ImageTagCategory.Character && score >= modelConfig.CharacterThreshold)
            {
                characterTags.Add(tagScore);
            }
        }

        List<ImageTagScore> orderedRatings = ratingTags
            .OrderByDescending(tag => tag.Score)
            .ToList();
        List<ImageTagScore> orderedGeneralTags = generalTags
            .OrderByDescending(tag => tag.Score)
            .ToList();
        List<ImageTagScore> orderedCharacterTags = characterTags
            .OrderByDescending(tag => tag.Score)
            .ToList();
        List<ImageTagScore> acceptedTags = orderedGeneralTags
            .Concat(orderedCharacterTags)
            .OrderByDescending(tag => tag.Score)
            .ToList();

        return new ImageTaggingResult
        {
            ModelId = modelConfig.ModelId,
            RatingTags = orderedRatings,
            GeneralTags = orderedGeneralTags,
            CharacterTags = orderedCharacterTags,
            SelectedRating = orderedRatings.FirstOrDefault()?.ExportName ?? string.Empty,
            AcceptedTrainingTags = acceptedTags
                .Select(tag => tag.ExportName)
                .ToList(),
        };
    }

    private static ImageTagCategory MapCategory(int categoryValue)
    {
        return categoryValue switch
        {
            0 => ImageTagCategory.General,
            4 => ImageTagCategory.Character,
            9 => ImageTagCategory.Rating,
            _ => ImageTagCategory.Metadata,
        };
    }

    private static string NormalizeExportName(string sourceName)
    {
        if (Kaomojis.Contains(sourceName))
        {
            return sourceName;
        }

        return sourceName.Replace('_', ' ');
    }
}
