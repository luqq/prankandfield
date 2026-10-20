using System.Text.Json;
using System.Text.Json.Serialization;
using PrankAndField.Core;
using PrankAndField.Core.Localization;

namespace PrankAndField.Audio;

/// <summary>Procedural effects synthesized by the audio backend.</summary>
public enum SoundId
{
    Gun = 1,
    Step,
    Jump,
    Land,
    Throw,
    HurdleHit,
    BarFall,
    Crowd,
    Bird,
    Mole,
    Bonus,
    MenuMove,
    MenuSelect,
    Whistle,
    Spin,
    Tick,
}

public enum AudioEventKind
{
    PlaySong = 1,
    PlaySound = 2,
    Speak = 3,
    StopMusic = 4,
    /// <summary>Speak a mark: Id = hundredths of a second / metre, Arg = 1 for a time, 0 for a distance.</summary>
    SpeakMark = 5,
    /// <summary>Flight tone: Id = height in thousandths (0..1000), Arg = 1 while airborne, 0 to stop.</summary>
    FlightTone = 6,
}

/// <summary>A backend-neutral audio request. Encoded as (kind, a, b) triplets in the frame buffer.</summary>
public readonly record struct AudioEvent(AudioEventKind Kind, int Id, int Arg = 0)
{
    public (double, double, double) ToTriplet() => ((double)Kind, Id, Arg);
}

/// <summary>Announcer phrases, spoken by the robotic voice in the current language.</summary>
public enum VoiceId
{
    Title = 1,
    OnYourMarks,
    GetSet,
    Go,
    FalseStart,
    Foul,
    Qualified,
    NotQualified,
    NewWorldRecord,
    GameOver,
    Try1,
    Try2,
    Try3,
    Bonus,
}

/// <summary>Maps simulation cues to songs, sounds and voice lines.</summary>
public static class CueMap
{
    public static IReadOnlyList<AudioEvent> Map(AudioCue cue) => cue switch
    {
        AudioCue.SongTitle => [new(AudioEventKind.PlaySong, Songs.Title.Id, 1)],
        AudioCue.SongEventIntro => [new(AudioEventKind.PlaySong, Songs.EventIntro.Id)],
        AudioCue.SongEventStart => [new(AudioEventKind.PlaySong, Songs.EventStart.Id)],
        AudioCue.SongQualified => [new(AudioEventKind.PlaySong, Songs.Qualified.Id)],
        AudioCue.SongWorldRecord => [new(AudioEventKind.PlaySong, Songs.WorldRecord.Id)],
        AudioCue.SongFailed => [new(AudioEventKind.PlaySong, Songs.Failed.Id)],
        AudioCue.SongCeremony => [new(AudioEventKind.PlaySong, Songs.Ceremony.Id, 1)],
        AudioCue.SongGameOver => [new(AudioEventKind.PlaySong, Songs.GameOver.Id)],
        AudioCue.SongNameEntry => [new(AudioEventKind.PlaySong, Songs.NameEntry.Id, 1)],
        AudioCue.StopMusic => [new(AudioEventKind.StopMusic, 0)],

        AudioCue.VoiceTitle => [new(AudioEventKind.Speak, (int)VoiceId.Title)],
        AudioCue.VoiceOnYourMarks => [new(AudioEventKind.Speak, (int)VoiceId.OnYourMarks)],
        AudioCue.VoiceGetSet => [new(AudioEventKind.Speak, (int)VoiceId.GetSet)],
        AudioCue.VoiceGo => [new(AudioEventKind.Speak, (int)VoiceId.Go)],
        AudioCue.VoiceFalseStart => [new(AudioEventKind.Speak, (int)VoiceId.FalseStart)],
        AudioCue.VoiceFoul => [new(AudioEventKind.Speak, (int)VoiceId.Foul)],
        AudioCue.VoiceQualified => [new(AudioEventKind.Speak, (int)VoiceId.Qualified)],
        AudioCue.VoiceNotQualified => [new(AudioEventKind.Speak, (int)VoiceId.NotQualified)],
        AudioCue.VoiceNewWorldRecord => [new(AudioEventKind.Speak, (int)VoiceId.NewWorldRecord)],
        AudioCue.VoiceGameOver => [new(AudioEventKind.Speak, (int)VoiceId.GameOver)],
        AudioCue.VoiceTryNumber1 => [new(AudioEventKind.Speak, (int)VoiceId.Try1)],
        AudioCue.VoiceTryNumber2 => [new(AudioEventKind.Speak, (int)VoiceId.Try2)],
        AudioCue.VoiceTryNumber3 => [new(AudioEventKind.Speak, (int)VoiceId.Try3)],
        AudioCue.VoiceBonus => [new(AudioEventKind.Speak, (int)VoiceId.Bonus)],

        AudioCue.SfxGun => [new(AudioEventKind.PlaySound, (int)SoundId.Gun)],
        AudioCue.SfxStep => [new(AudioEventKind.PlaySound, (int)SoundId.Step)],
        AudioCue.SfxJump => [new(AudioEventKind.PlaySound, (int)SoundId.Jump)],
        AudioCue.SfxLand => [new(AudioEventKind.PlaySound, (int)SoundId.Land)],
        AudioCue.SfxThrow => [new(AudioEventKind.PlaySound, (int)SoundId.Throw)],
        AudioCue.SfxHurdleHit => [new(AudioEventKind.PlaySound, (int)SoundId.HurdleHit)],
        AudioCue.SfxBarFall => [new(AudioEventKind.PlaySound, (int)SoundId.BarFall)],
        AudioCue.SfxCrowd => [new(AudioEventKind.PlaySound, (int)SoundId.Crowd)],
        AudioCue.SfxBird => [new(AudioEventKind.PlaySound, (int)SoundId.Bird)],
        AudioCue.SfxMole => [new(AudioEventKind.PlaySound, (int)SoundId.Mole)],
        AudioCue.SfxBonus => [new(AudioEventKind.PlaySound, (int)SoundId.Bonus)],
        AudioCue.SfxMenuMove => [new(AudioEventKind.PlaySound, (int)SoundId.MenuMove)],
        AudioCue.SfxMenuSelect => [new(AudioEventKind.PlaySound, (int)SoundId.MenuSelect)],
        AudioCue.SfxWhistle => [new(AudioEventKind.PlaySound, (int)SoundId.Whistle)],
        AudioCue.SfxSpin => [new(AudioEventKind.PlaySound, (int)SoundId.Spin)],
        AudioCue.SfxTick => [new(AudioEventKind.PlaySound, (int)SoundId.Tick)],
        _ => [],
    };

    public static string VoiceKey(VoiceId id) => id switch
    {
        VoiceId.Title => "voice.title",
        VoiceId.OnYourMarks => "voice.on_your_marks",
        VoiceId.GetSet => "voice.get_set",
        VoiceId.Go => "voice.go",
        VoiceId.FalseStart => "voice.false_start",
        VoiceId.Foul => "voice.foul",
        VoiceId.Qualified => "voice.qualified",
        VoiceId.NotQualified => "voice.not_qualified",
        VoiceId.NewWorldRecord => "voice.new_world_record",
        VoiceId.GameOver => "voice.game_over",
        VoiceId.Try1 => "voice.try1",
        VoiceId.Try2 => "voice.try2",
        VoiceId.Try3 => "voice.try3",
        VoiceId.Bonus => "voice.bonus",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}

/// <summary>Serialises songs and localized voice phrases for the JavaScript synthesizer.</summary>
public static class AudioCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed class NoteDto
    {
        [JsonPropertyName("m")] public int Midi { get; set; }
        [JsonPropertyName("b")] public double Beats { get; set; }
        [JsonPropertyName("s")] public string? Syllable { get; set; }
    }

    public sealed class SongDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("bpm")] public double Bpm { get; set; }
        [JsonPropertyName("loop")] public bool Loop { get; set; }
        [JsonPropertyName("lead")] public List<NoteDto> Lead { get; set; } = new();
        [JsonPropertyName("bass")] public List<NoteDto> Bass { get; set; } = new();
        [JsonPropertyName("arp")] public List<NoteDto> Arp { get; set; } = new();
    }

    public sealed class CatalogDto
    {
        [JsonPropertyName("songs")] public List<SongDto> Songs { get; set; } = new();
    }

    public static CatalogDto Build()
    {
        var dto = new CatalogDto();
        foreach (var s in Songs.All)
        {
            dto.Songs.Add(new SongDto
            {
                Id = s.Id,
                Name = s.Name,
                Bpm = s.Bpm,
                Loop = s.Loop,
                Lead = s.Lead.Select(ToDto).ToList(),
                Bass = s.Bass.Select(ToDto).ToList(),
                Arp = s.Arp.Select(ToDto).ToList(),
            });
        }
        return dto;
    }

    private static NoteDto ToDto(Note n) => new() { Midi = n.Midi, Beats = n.Beats, Syllable = n.Syllable };

    public static string ToJson() => JsonSerializer.Serialize(Build(), Options);

    public static CatalogDto? FromJson(string json) => JsonSerializer.Deserialize<CatalogDto>(json, Options);

    /// <summary>
    /// Voice phrases for the current language:
    /// { "lang": "es-ES", "phrases": { "1": "..." }, "time": "{0} point {1} seconds", "distance": "{0} metres {1}" }.
    /// </summary>
    public static string VoicePhrasesJson(Localizer loc)
    {
        var phrases = new Dictionary<string, string>();
        foreach (VoiceId id in Enum.GetValues<VoiceId>()) phrases[((int)id).ToString()] = loc[CueMap.VoiceKey(id)];
        return JsonSerializer.Serialize(new
        {
            lang = loc["lang.tts"],
            phrases,
            time = loc["voice.time"],
            distance = loc["voice.distance"],
        }, Options);
    }
}

/// <summary>Drains cues from a game and converts them to audio events for the frame buffer.</summary>
public sealed class CueSequencer
{
    private readonly List<(double, double, double)> _pending = new();
    private bool _tonePlaying;

    public void Enqueue(AudioCue cue)
    {
        foreach (var e in CueMap.Map(cue)) _pending.Add(e.ToTriplet());
    }

    public void Drain(Game game)
    {
        while (game.TryDequeueCue(out var cue)) Enqueue(cue);
        while (game.TryDequeueMark(out var mark))
        {
            _pending.Add(new AudioEvent(AudioEventKind.SpeakMark, (int)Math.Round(mark.Mark * 100), mark.Kind.IsTimed() ? 1 : 0).ToTriplet());
        }
        // Continuous flight tone: one event per frame while something is airborne, one stop event afterwards.
        double? height = game.State == GameState.Event ? game.Simulation?.FlightHeight : null;
        if (height is double h)
        {
            _pending.Add(new AudioEvent(AudioEventKind.FlightTone, (int)Math.Round(Math.Clamp(h, 0, 1) * 1000), 1).ToTriplet());
            _tonePlaying = true;
        }
        else if (_tonePlaying)
        {
            _pending.Add(new AudioEvent(AudioEventKind.FlightTone, 0, 0).ToTriplet());
            _tonePlaying = false;
        }
    }

    public int PendingCount => _pending.Count;

    public (double, double, double)[] Take()
    {
        var arr = _pending.ToArray();
        _pending.Clear();
        return arr;
    }
}
