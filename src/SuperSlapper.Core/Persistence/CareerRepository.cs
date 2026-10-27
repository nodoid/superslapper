using System.Text.Json;
using System.Text.Json.Serialization;
using SuperSlapper.Core.Career;

namespace SuperSlapper.Core.Persistence;

/// <summary>
/// Saves and loads a career as JSON. The save carries a schema version so a
/// career created by an older build can still be opened after a game update -
/// this is what "carries into the next game" means in practice.
/// </summary>
public sealed class CareerRepository
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _directory;

    public CareerRepository(string directory) => _directory = directory;

    public string Directory => _directory;

    public string SlotPath(string slot) =>
        Path.Combine(_directory, SanitiseSlot(slot) + ".json");

    /// <summary>Writes the career atomically so a mid-save crash cannot corrupt it.</summary>
    public void Save(CareerState state, string slot = "career")
    {
        System.IO.Directory.CreateDirectory(_directory);
        var path = SlotPath(slot);
        var temp = path + ".tmp";

        state.SchemaVersion = CareerState.CurrentSchemaVersion;
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(temp, path);
    }

    public CareerState? Load(string slot = "career")
    {
        var path = SlotPath(slot);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<CareerState>(json, Options);
        if (state is null)
        {
            return null;
        }

        return Migrate(state);
    }

    public bool Exists(string slot = "career") => File.Exists(SlotPath(slot));

    public void Delete(string slot = "career")
    {
        var path = SlotPath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public IReadOnlyList<string> ListSlots()
    {
        if (!System.IO.Directory.Exists(_directory))
        {
            return Array.Empty<string>();
        }

        return System.IO.Directory
            .EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Upgrades older saves. Kept explicit so a migration is a conscious act.</summary>
    public static CareerState Migrate(CareerState state)
    {
        if (state.SchemaVersion < 1)
        {
            state.SchemaVersion = 1;
        }

        // Future migrations get applied here in order.

        state.SchemaVersion = CareerState.CurrentSchemaVersion;
        return state;
    }

    public static string SanitiseSlot(string slot)
    {
        var cleaned = new string(slot
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "career" : cleaned;
    }

    /// <summary>Round-trips a career in memory; used by tests and the save preview.</summary>
    public static CareerState RoundTrip(CareerState state)
    {
        var json = JsonSerializer.Serialize(state, Options);
        var clone = JsonSerializer.Deserialize<CareerState>(json, Options)
                    ?? throw new InvalidOperationException("Failed to round-trip career state.");
        return Migrate(clone);
    }
}
