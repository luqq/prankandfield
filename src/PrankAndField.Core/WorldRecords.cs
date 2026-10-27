using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrankAndField.Core;

public readonly record struct RecordEntry(double Mark, string Initials);

/// <summary>
/// The WORLD RECORD board: top three marks per event (both games) with three-letter initials.
/// Track &amp; Field defaults are the real 1983 world records; the arcade shipped with placeholder initials EEE/FFF/GGG.
/// </summary>
public sealed class WorldRecords
{
    public const int Slots = 3;

    private readonly RecordEntry[][] _table;

    public WorldRecords()
    {
        _table = Defaults();
    }

    public IReadOnlyList<RecordEntry> For(EventKind kind) => _table[(int)kind];

    public RecordEntry Best(EventKind kind) => _table[(int)kind][0];

    /// <summary>Rank (1..3) the mark would obtain, or 0 if it does not enter the board.</summary>
    public int RankOf(EventKind kind, double mark)
    {
        var row = _table[(int)kind];
        for (int i = 0; i < row.Length; i++)
        {
            if (Rules.IsBetter(kind, mark, row[i].Mark)) return i + 1;
        }
        return 0;
    }

    public bool IsWorldRecord(EventKind kind, double mark) => RankOf(kind, mark) == 1;

    /// <summary>Inserts the mark if it ranks; returns the rank or 0.</summary>
    public int Submit(EventKind kind, double mark, string initials)
    {
        int rank = RankOf(kind, mark);
        if (rank == 0) return 0;
        var row = _table[(int)kind];
        for (int i = row.Length - 1; i >= rank; i--) row[i] = row[i - 1];
        row[rank - 1] = new RecordEntry(Rules.Snap(mark), NormalizeInitials(initials));
        return rank;
    }

    public void ResetToDefaults()
    {
        var d = Defaults();
        for (int i = 0; i < _table.Length; i++) _table[i] = d[i];
    }

    public static string NormalizeInitials(string initials)
    {
        if (string.IsNullOrWhiteSpace(initials)) return "AAA";
        string s = initials.Trim().ToUpperInvariant();
        if (s.Length > 3) s = s[..3];
        return s.PadRight(3, 'A');
    }

    private static RecordEntry[][] Defaults() =>
    [
        // Track & Field: the real 1983 world records with the arcade's placeholder initials
        [new(9.95, "EEE"), new(10.00, "FFF"), new(10.12, "GGG")],   // 100 m (Hines 1968)
        [new(8.90, "EEE"), new(8.77, "FFF"), new(8.43, "GGG")],     // long jump (Beamon 1968)
        [new(99.72, "EEE"), new(96.72, "FFF"), new(94.58, "GGG")],  // javelin (Petranoff 1983)
        [new(12.93, "EEE"), new(13.00, "FFF"), new(13.11, "GGG")],  // 110 m hurdles (Nehemiah 1981)
        [new(84.14, "EEE"), new(83.98, "FFF"), new(81.80, "GGG")],  // hammer (Litvinov 1983)
        [new(2.37, "EEE"), new(2.36, "FFF"), new(2.35, "GGG")],     // high jump (Zhu 1983)
        // Hyper Sports: arcade-paced marks (the swim runs about twice as fast as real life)
        [new(24.00, "EEE"), new(24.60, "FFF"), new(25.20, "GGG")],  // 100 m swim (the computer swims about 24.1)
        [new(20, "EEE"), new(19, "FFF"), new(18, "GGG")],           // skeet (hits out of 20)
        [new(9.90, "EEE"), new(9.80, "FFF"), new(9.70, "GGG")],     // long horse (score)
        [new(58, "EEE"), new(56, "FFF"), new(55, "GGG")],           // archery (points, six arrows)
        [new(17.89, "EEE"), new(17.57, "FFF"), new(17.44, "GGG")],  // triple jump (de Oliveira 1975)
        [new(205, "EEE"), new(200, "FFF"), new(195, "GGG")],     // weightlifting (kg; round 4 starts at 200, +5 per good lift)
    ];

    // ---- persistence ---------------------------------------------------------------------

    private sealed class Dto
    {
        [JsonPropertyName("v")] public int Version { get; set; } = 1;
        [JsonPropertyName("records")] public List<List<EntryDto>> Records { get; set; } = new();
    }

    private sealed class EntryDto
    {
        [JsonPropertyName("m")] public double Mark { get; set; }
        [JsonPropertyName("i")] public string Initials { get; set; } = "AAA";
    }

    public string ToJson()
    {
        var dto = new Dto();
        foreach (var row in _table)
            dto.Records.Add(row.Select(e => new EntryDto { Mark = e.Mark, Initials = e.Initials }).ToList());
        return JsonSerializer.Serialize(dto, JsonOptions.Default);
    }

    /// <summary>
    /// Loads a snapshot produced by <see cref="ToJson"/>. Invalid input leaves the defaults. A board saved before
    /// Hyper Sports existed only has the six Track &amp; Field rows: those are restored and the new rows keep their defaults.
    /// </summary>
    public bool TryLoadJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions.Default);
            if (dto is null || dto.Records.Count == 0 || dto.Records.Count > _table.Length) return false;
            var fresh = Defaults();
            for (int k = 0; k < dto.Records.Count; k++)
            {
                var kind = (EventKind)k;
                var src = dto.Records[k];
                if (src.Count != Slots) return false;
                for (int i = 0; i < Slots; i++)
                {
                    if (double.IsNaN(src[i].Mark) || src[i].Mark <= 0) return false;
                    fresh[k][i] = new RecordEntry(src[i].Mark, NormalizeInitials(src[i].Initials));
                }
                for (int i = 1; i < Slots; i++)
                {
                    if (Rules.IsBetter(kind, fresh[k][i].Mark, fresh[k][i - 1].Mark)) return false; // must be sorted
                }
            }
            for (int k = 0; k < _table.Length; k++) _table[k] = fresh[k];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web) { WriteIndented = false };
}
