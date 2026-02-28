namespace GardenAI;

/// <summary>
/// Reads garden bed configuration from Markdown files in the beds directory.
/// Each file's frontmatter provides structured config; the body describes
/// what's planted and any care notes — both are injected into the AI prompt.
///
/// If an image file with the same base name exists alongside the markdown
/// (e.g. main-bed.jpg), it is loaded and sent to vision-capable models.
/// </summary>
public static class GardenBedLoader
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static List<GardenBed> LoadAll(string bedsDirectory)
    {
        if (!Directory.Exists(bedsDirectory))
            return [];

        return Directory.GetFiles(bedsDirectory, "*.md")
            .Select(Load)
            .OrderBy(b => b.Channels.DefaultIfEmpty(int.MaxValue).Min())
            .ToList();
    }

    private static GardenBed Load(string filePath)
    {
        var raw = File.ReadAllText(filePath);
        var (meta, body) = ParseFrontmatter(raw);

        // channels: accepts "1" or "1, 2" — non-integer values are silently skipped
        var channels = meta.TryGetValue("channels", out var ch)
            ? ch.Split(',')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToList()
            : meta.TryGetValue("channel", out var single) && int.TryParse(single.Trim(), out var singleVal)
                ? [singleVal]
                : new List<int>();

        // optional image alongside the markdown file
        string? imageData      = null;
        string? imageMediaType = null;
        var basePath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            Path.GetFileNameWithoutExtension(filePath));

        foreach (var ext in ImageExtensions)
        {
            var imagePath = basePath + ext;
            if (!File.Exists(imagePath)) continue;

            imageData      = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            imageMediaType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".webp"           => "image/webp",
                _                 => "image/jpeg"
            };
            break;
        }

        return new GardenBed(
            Channels:       channels,
            Name:           meta.GetValueOrDefault("name", Path.GetFileNameWithoutExtension(filePath)),
            Location:       meta.GetValueOrDefault("location", ""),
            AreaSqm:        meta.TryGetValue("area_sqm", out var area) ? double.Parse(area) : null,
            Soil:           meta.GetValueOrDefault("soil", ""),
            Sun:            meta.GetValueOrDefault("sun", ""),
            Notes:          body.Trim(),
            ImageData:      imageData,
            ImageMediaType: imageMediaType
        );
    }

    private static (Dictionary<string, string> meta, string body) ParseFrontmatter(string content)
    {
        var meta = new Dictionary<string, string>();

        if (!content.StartsWith("---"))
            return (meta, content);

        var end = content.IndexOf("---", 3);
        if (end < 0)
            return (meta, content);

        var yaml = content[3..end].Trim();
        var body = content[(end + 3)..].Trim();

        foreach (var line in yaml.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key   = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"').Trim('\'');
            if (!string.IsNullOrEmpty(key))
                meta[key] = value;
        }

        return (meta, body);
    }
}
