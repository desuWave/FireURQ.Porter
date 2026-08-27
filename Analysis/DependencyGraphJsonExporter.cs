using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireURQ.Porter.Analysis;

public static class DependencyGraphJsonExporter
{
    public static void Export<T>(
        T graph,
        string outputPath)
    {
        if (graph == null)
            throw new ArgumentNullException(
                nameof(graph));

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException(
                "Output path cannot be empty.",
                nameof(outputPath));

        var directory =
            Path.GetDirectoryName(
                Path.GetFullPath(outputPath));

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var options =
            new JsonSerializerOptions
            {
                WriteIndented = true,

                DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull
            };

        var json =
            JsonSerializer.Serialize(
                graph,
                options);

        File.WriteAllText(
            outputPath,
            json);
    }
}