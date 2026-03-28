using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.IO;
using System.Linq;

namespace DatasetStudio.Tests;

[TestFixture]
public class TagPostProcessorTests
{
    [Test]
    public void LoadLabels_MissingFile_Throws()
    {
        TagPostProcessor processor = new TagPostProcessor();

        Assert.Throws<FileNotFoundException>(() => processor.LoadLabels("C:\\missing\\selected_tags.csv"));
    }

    [Test]
    public void LoadLabels_ParsesCategoriesAndExportNames()
    {
        string csvPath = CreateTempCsv(
            "tag_id,name,category,count",
            "0,safe,9,100",
            "1,blue_eyes,0,500",
            "2,alice_margatroid,4,250",
            "3,>_<,0,50");
        TagPostProcessor processor = new TagPostProcessor();

        IReadOnlyList<TaggerLabelDefinition> labels = processor.LoadLabels(csvPath);

        Assert.That(labels, Has.Count.EqualTo(4));
        Assert.That(labels[0].Category, Is.EqualTo(ImageTagCategory.Rating));
        Assert.That(labels[1].ExportName, Is.EqualTo("blue eyes"));
        Assert.That(labels[2].Category, Is.EqualTo(ImageTagCategory.Character));
        Assert.That(labels[3].ExportName, Is.EqualTo(">_<"));
    }

    [Test]
    public void CreateResult_MismatchedScoreCount_Throws()
    {
        TagPostProcessor processor = new TagPostProcessor();
        TaggerModelConfig modelConfig = new TaggerModelConfig
        {
            ModelId = "wd-swinv2",
        };
        IReadOnlyList<TaggerLabelDefinition> labels = new[]
        {
            new TaggerLabelDefinition { Index = 0, SourceName = "safe", ExportName = "safe", Category = ImageTagCategory.Rating },
            new TaggerLabelDefinition { Index = 1, SourceName = "1girl", ExportName = "1girl", Category = ImageTagCategory.General },
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            processor.CreateResult(modelConfig, labels, new float[] { 0.9f }));

        Assert.That(exception.Message, Does.Contain("output width"));
    }

    [Test]
    public void CreateResult_AppliesThresholdsAndProducesStructuredTags()
    {
        TagPostProcessor processor = new TagPostProcessor();
        TaggerModelConfig modelConfig = new TaggerModelConfig
        {
            ModelId = "wd-swinv2",
            GeneralThreshold = 0.35f,
            CharacterThreshold = 0.85f,
        };
        IReadOnlyList<TaggerLabelDefinition> labels = new[]
        {
            new TaggerLabelDefinition { Index = 0, SourceName = "safe", ExportName = "safe", Category = ImageTagCategory.Rating },
            new TaggerLabelDefinition { Index = 1, SourceName = "questionable", ExportName = "questionable", Category = ImageTagCategory.Rating },
            new TaggerLabelDefinition { Index = 2, SourceName = "1girl", ExportName = "1girl", Category = ImageTagCategory.General },
            new TaggerLabelDefinition { Index = 3, SourceName = "blue_eyes", ExportName = "blue eyes", Category = ImageTagCategory.General },
            new TaggerLabelDefinition { Index = 4, SourceName = "alice_margatroid", ExportName = "alice margatroid", Category = ImageTagCategory.Character },
            new TaggerLabelDefinition { Index = 5, SourceName = ">_<", ExportName = ">_<", Category = ImageTagCategory.General },
        };
        float[] scores = new[] { 0.80f, 0.20f, 0.90f, 0.40f, 0.88f, 0.50f };

        ImageTaggingResult result = processor.CreateResult(modelConfig, labels, scores);

        Assert.That(result.ModelId, Is.EqualTo("wd-swinv2"));
        Assert.That(result.SelectedRating, Is.EqualTo("safe"));
        Assert.That(result.RatingTags.Select(tag => tag.ExportName), Is.EqualTo(new[] { "safe", "questionable" }));
        Assert.That(result.GeneralTags.Select(tag => tag.ExportName), Is.EqualTo(new[] { "1girl", ">_<", "blue eyes" }));
        Assert.That(result.CharacterTags.Select(tag => tag.ExportName), Is.EqualTo(new[] { "alice margatroid" }));
        Assert.That(result.AcceptedTrainingTags, Is.EqualTo(new[] { "1girl", "alice margatroid", ">_<", "blue eyes" }));
    }

    private static string CreateTempCsv(params string[] lines)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            "DatasetStudioTests",
            Guid.NewGuid().ToString("N"),
            "selected_tags.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllLines(filePath, lines);
        return filePath;
    }
}
