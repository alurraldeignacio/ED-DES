using System.Text.Json;

namespace UrgentCare.Core.Configuration;

/// <summary>
/// Reads a <see cref="ScenarioConfig"/> from JSON. The file is a producer/consumer boundary,
/// so parsing happens here and nowhere else (Constitution Principle IV).
/// </summary>
public sealed class ScenarioConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Loads a scenario from an explicit file path.</summary>
    public ScenarioConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Scenario file not found: {path}", path);
        }

        return Parse(File.ReadAllText(path), path);
    }

    /// <summary>
    /// Loads a scenario by name from a configuration directory, so callers pass
    /// "default" rather than a full path.
    /// </summary>
    public ScenarioConfig LoadByName(string configDirectory, string scenarioName)
    {
        var path = Path.Combine(configDirectory, $"{scenarioName}-scenario.json");
        return LoadFromFile(path);
    }

    /// <summary>Parses scenario JSON already held in memory.</summary>
    public ScenarioConfig Parse(string json, string sourceName = "<memory>")
    {
        ScenarioConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<ScenarioConfig>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ScenarioConfigException($"Scenario '{sourceName}' is not valid JSON: {ex.Message}", ex);
        }

        return config ?? throw new ScenarioConfigException($"Scenario '{sourceName}' deserialized to null.");
    }
}

/// <summary>Raised when a scenario file cannot be read or fails the configuration contract.</summary>
public sealed class ScenarioConfigException : Exception
{
    public ScenarioConfigException(string message) : base(message)
    {
    }

    public ScenarioConfigException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
