namespace PrankAndField.Audio;

/// <summary>One note of a voice. Midi 0 is a rest. Syllable is sung by the robotic voice on lead notes.</summary>
public readonly record struct Note(int Midi, double Beats, string? Syllable = null)
{
    public bool IsRest => Midi == 0;
    public static Note Rest(double beats) => new(0, beats);
}

/// <summary>A short chiptune with a sung lead line, a square-wave bass and an arpeggio voice.</summary>
public sealed class Song
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required double Bpm { get; init; }
    public bool Loop { get; init; }
    public required Note[] Lead { get; init; }
    public Note[] Bass { get; init; } = [];
    public Note[] Arp { get; init; } = [];

    public double BeatsOf(Note[] voice) => voice.Sum(n => n.Beats);
    public double DurationSeconds => BeatsOf(Lead) * 60.0 / Bpm;
}

/// <summary>Original jingles in the spirit of the 1983 arcade, written for this tribute.</summary>
public static class Songs
{
    // MIDI helpers
    private const int C4 = 60, D4 = 62, E4 = 64, F4 = 65, G4 = 67, A4 = 69, B4 = 71;
    private const int C5 = 72, D5 = 74, E5 = 76, F5 = 77, G5 = 79, A5 = 81, B5 = 83, C6 = 84;
    private const int C3 = 48, D3 = 50, E3 = 52, F3 = 53, G3 = 55, A3 = 57, B3 = 59;
    private const int C2 = 36, G2 = 43, F2 = 41, A2 = 45, D2 = 38;

    private static Note N(int midi, double beats, string? syl = null) => new(midi, beats, syl);
    private static Note R(double beats) => Note.Rest(beats);

    public static readonly Song Title = new()
    {
        Id = 1,
        Name = "Prank & Field March",
        Bpm = 140,
        Loop = true,
        Lead =
        [
            N(G4, 1, "PRANK"), N(G4, 0.5, "AND"), N(B4, 1.5, "FIELD"), R(1),
            N(C5, 0.5, "RUN"), N(B4, 0.5, "AND"), N(A4, 0.5, "JUMP"), N(G4, 0.5, "AND"), N(A4, 1, "THROW"), R(1),
            N(G4, 1, "PRANK"), N(G4, 0.5, "AND"), N(B4, 1.5, "FIELD"), R(1),
            N(D5, 0.5, "GO"), N(C5, 0.5, "FOR"), N(B4, 0.5, "THE"), N(A4, 0.5, "WORLD"), N(G4, 1.5, "RE"), N(E4, 1.5, "CORD"),
            N(D5, 1, "FAST"), N(B4, 0.5, "ER"), N(G4, 1.5, "NOW"), R(1),
            N(E5, 0.5, "HI"), N(D5, 0.5, "GHER"), N(C5, 0.5, "LON"), N(B4, 0.5, "GER"), N(A4, 1, "STRON"), N(G4, 1, "GER"),
            N(G4, 0.5, "ONE"), N(A4, 0.5, "TWO"), N(B4, 0.5, "ONE"), N(C5, 0.5, "TWO"), N(D5, 2, "GO"), R(2),
        ],
        Bass =
        [
            N(G2, 1), N(G2, 1), N(D2, 1), N(D2, 1), N(C3, 1), N(C3, 1), N(D2, 1), N(D2, 1),
            N(G2, 1), N(G2, 1), N(D2, 1), N(D2, 1), N(C3, 1), N(C3, 1), N(D2, 1), N(D2, 1),
            N(G2, 1), N(G2, 1), N(D2, 1), N(D2, 1), N(C3, 1), N(C3, 1), N(D2, 1), N(D2, 1),
            N(G2, 1), N(A2, 1), N(G2, 1), N(D2, 1), N(G2, 2), N(G2, 2),
        ],
        Arp =
        [
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5), N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5),
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5), N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5),
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(E5, 0.5), N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(E5, 0.5),
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(G5, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(D4, 0.5),
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(G5, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(D4, 0.5),
        ],
    };

    public static readonly Song EventIntro = new()
    {
        Id = 2,
        Name = "Here We Go",
        Bpm = 150,
        Lead = [N(C5, 0.5, "HERE"), N(E5, 0.5, "WE"), N(G5, 1.5, "GO"), R(0.5), N(E5, 0.5, "READ"), N(G5, 0.5, "Y"), N(C6, 2, "SET")],
        Bass = [N(C3, 1), N(G2, 1), N(C3, 1), N(G2, 1), N(C3, 2)],
        Arp = [N(C5, 0.25), N(E5, 0.25), N(G5, 0.25), N(C6, 0.25), N(G5, 0.25), N(E5, 0.25), N(C5, 0.25), N(E5, 0.25), N(G5, 0.5), N(C6, 0.5), N(E5, 0.5), N(G5, 0.5), N(C6, 2)],
    };

    public static readonly Song Qualified = new()
    {
        Id = 3,
        Name = "Qualified",
        Bpm = 160,
        Lead = [N(E5, 0.5, "QUA"), N(E5, 0.5, "LI"), N(G5, 1, "FIED"), N(C6, 1.5, "YEAH"), R(0.5)],
        Bass = [N(C3, 1), N(G3, 1), N(C3, 2)],
        Arp = [N(C5, 0.5), N(G5, 0.5), N(E5, 0.5), N(C6, 0.5), N(G5, 1), N(C6, 1)],
    };

    public static readonly Song WorldRecord = new()
    {
        Id = 4,
        Name = "World Record Fanfare",
        Bpm = 150,
        Lead =
        [
            N(G4, 0.5, "NEW"), N(C5, 0.5, "WORLD"), N(E5, 0.5, "RE"), N(G5, 1.5, "CORD"), R(0.5),
            N(A5, 0.5, "YOU"), N(G5, 0.5, "ARE"), N(E5, 0.5, "THE"), N(C6, 2.5, "BEST"),
        ],
        Bass = [N(C3, 1), N(C3, 1), N(F3, 1), N(G3, 1), N(C3, 2), N(G2, 1), N(C3, 1)],
        Arp = [N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(C6, 0.5), N(F5, 0.5), N(A5, 0.5), N(G5, 0.5), N(B5, 0.5), N(C6, 0.5), N(G5, 0.5), N(E5, 0.5), N(C6, 2.5)],
    };

    public static readonly Song Failed = new()
    {
        Id = 5,
        Name = "Not Qualified",
        Bpm = 100,
        Lead = [N(E4, 1, "OH"), N(D4, 1, "NO"), N(C4, 2, "OOH")],
        Bass = [N(A2, 1), N(G2, 1), N(F2, 2)],
        Arp = [N(A4, 0.5), N(C5, 0.5), N(G4, 0.5), N(B4, 0.5), N(F4, 1), N(A4, 1)],
    };

    public static readonly Song Ceremony = new()
    {
        Id = 6,
        Name = "Champion Anthem",
        Bpm = 96,
        Loop = true,
        Lead =
        [
            N(C5, 1, "CHAM"), N(E5, 1, "PI"), N(G5, 2, "ON"),
            N(F5, 1, "YOU"), N(E5, 1, "ARE"), N(D5, 2, "GOLD"),
            N(C5, 1, "STAND"), N(D5, 1, "UP"), N(E5, 1, "AND"), N(F5, 1, "SING"),
            N(G5, 3, "CHAM"), N(E5, 1, "PION"), N(C5, 4, "OOH"),
        ],
        Bass = [N(C3, 2), N(G3, 2), N(F3, 2), N(G3, 2), N(C3, 2), N(F3, 2), N(G3, 2), N(G3, 2), N(C3, 4)],
        Arp =
        [
            N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(E5, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(E5, 0.5), N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5),
            N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5),
            N(C5, 0.5), N(E5, 0.5), N(G5, 0.5), N(C6, 0.5), N(G5, 0.5), N(E5, 0.5), N(C5, 0.5), N(G4, 0.5),
        ],
    };

    public static readonly Song GameOver = new()
    {
        Id = 7,
        Name = "Game Over",
        Bpm = 90,
        Lead = [N(G4, 1, "GAME"), N(F4, 1, "O"), N(E4, 1, "VER"), R(0.5), N(D4, 0.5, "SEE"), N(C4, 0.5, "YOU"), N(B3, 2.5, "SOON")],
        Bass = [N(C3, 1), N(D2, 1), N(C3, 1), N(G2, 1), N(G2, 1), N(G2, 2)],
        Arp = [N(E4, 0.5), N(G4, 0.5), N(D4, 0.5), N(F4, 0.5), N(C4, 0.5), N(E4, 0.5), N(B3, 0.5), N(D4, 0.5), N(G3, 1), N(B3, 2)],
    };

    public static readonly Song NameEntry = new()
    {
        Id = 8,
        Name = "Sign Here",
        Bpm = 120,
        Loop = true,
        Lead = [N(A4, 0.5, "WHAT"), N(C5, 0.5, "IS"), N(E5, 1, "YOUR"), N(D5, 2, "NAME"), R(1), N(C5, 0.5, "TELL"), N(B4, 0.5, "ME"), N(A4, 2, "PLEASE"), R(1)],
        Bass = [N(A2, 1), N(A2, 1), N(F2, 1), N(F2, 1), N(G2, 1), N(G2, 1), N(A2, 1), N(A2, 1)],
        Arp = [N(A4, 0.5), N(C5, 0.5), N(E5, 0.5), N(C5, 0.5), N(F4, 0.5), N(A4, 0.5), N(C5, 0.5), N(A4, 0.5), N(G4, 0.5), N(B4, 0.5), N(D5, 0.5), N(B4, 0.5), N(A4, 0.5), N(C5, 0.5), N(E5, 0.5), N(C5, 0.5)],
    };

    /// <summary>Short brass-style fanfare played the moment an event begins (athletes walk onto the field).</summary>
    public static readonly Song EventStart = new()
    {
        Id = 9,
        Name = "Event Fanfare",
        Bpm = 132,
        Lead = [N(G4, 0.5, "TA"), N(C5, 0.5, "DA"), N(E5, 0.5, "DA"), N(G5, 1.0, "DAA"), N(E5, 0.5, "TA"), N(G5, 1.5, "DAAA")],
        Bass = [N(C3, 1), N(C3, 1), N(G2, 1), N(C3, 1.5)],
        Arp = [N(C5, 0.25), N(E5, 0.25), N(G5, 0.25), N(C6, 0.25), N(E5, 0.25), N(G5, 0.25), N(C6, 0.25), N(E5, 0.25), N(G5, 0.5), N(C6, 0.5), N(E5, 0.5), N(G5, 1.0)],
    };

    public static readonly IReadOnlyList<Song> All = [Title, EventIntro, Qualified, WorldRecord, Failed, Ceremony, GameOver, NameEntry, EventStart];

    public static Song? ById(int id) => All.FirstOrDefault(s => s.Id == id);
}
