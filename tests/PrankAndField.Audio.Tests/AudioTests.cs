using System.Text.Json;
using PrankAndField.Core;
using PrankAndField.Core.Localization;
using PrankAndField.UI;

namespace PrankAndField.Audio.Tests;

public class SongTests
{
    public static IEnumerable<object[]> AllSongs => Songs.All.Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(AllSongs))]
    public void NotesAreWellFormed(string name)
    {
        var song = Songs.All.Single(s => s.Name == name);
        foreach (var voice in new[] { song.Lead, song.Bass, song.Arp })
        {
            foreach (var n in voice)
            {
                Assert.True(n.Beats > 0, $"{name}: zero-length note");
                Assert.True(n.IsRest || n.Midi is >= 24 and <= 108, $"{name}: midi {n.Midi} out of range");
            }
        }
        Assert.All(song.Lead.Where(n => !n.IsRest), n => Assert.False(string.IsNullOrWhiteSpace(n.Syllable), $"{name}: lead note without syllable"));
        Assert.All(song.Lead.Where(n => n.IsRest), n => Assert.Null(n.Syllable));
        Assert.InRange(song.DurationSeconds, 1.0, 60.0);
        Assert.NotEmpty(song.Bass);
        Assert.NotEmpty(song.Arp);
    }

    [Fact]
    public void SongIdsAreUnique()
    {
        Assert.Equal(Songs.All.Count, Songs.All.Select(s => s.Id).Distinct().Count());
        Assert.Same(Songs.Title, Songs.ById(Songs.Title.Id));
        Assert.Null(Songs.ById(999));
    }

    [Fact]
    public void EverySyllableHasAVowelForTheFormantSynth()
    {
        foreach (var song in Songs.All)
            foreach (var n in song.Lead.Where(n => !n.IsRest))
                Assert.Matches("[AEIOUY]", n.Syllable!.ToUpperInvariant());
    }
}

public class CueTests
{
    [Fact]
    public void EveryCueMapsToAtLeastOneAudioEvent()
    {
        foreach (AudioCue cue in Enum.GetValues<AudioCue>())
        {
            // None is silent; VoiceMark is carried separately as a SpeakMark event with its value.
            if (cue is AudioCue.None or AudioCue.VoiceMark) { Assert.Empty(CueMap.Map(cue)); continue; }
            var events = CueMap.Map(cue);
            Assert.True(events.Count > 0, $"{cue} is silent");
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case AudioEventKind.PlaySong: Assert.NotNull(Songs.ById(e.Id)); break;
                    case AudioEventKind.PlaySound: Assert.True(Enum.IsDefined((SoundId)e.Id)); break;
                    case AudioEventKind.Speak: Assert.True(Enum.IsDefined((VoiceId)e.Id)); break;
                }
            }
        }
    }

    [Fact]
    public void VoicePhrasesExistInEveryLanguage()
    {
        foreach (var culture in Localizer.SupportedCultures)
        {
            var loc = new Localizer(culture);
            foreach (VoiceId id in Enum.GetValues<VoiceId>())
            {
                string text = loc[CueMap.VoiceKey(id)];
                Assert.False(text.StartsWith('['), $"{culture} lacks {id}");
            }
            using var doc = JsonDocument.Parse(AudioCatalog.VoicePhrasesJson(loc));
            Assert.Equal(loc["lang.tts"], doc.RootElement.GetProperty("lang").GetString());
            Assert.Equal(Enum.GetValues<VoiceId>().Length, doc.RootElement.GetProperty("phrases").EnumerateObject().Count());
            Assert.Contains("{0}", doc.RootElement.GetProperty("time").GetString());
            Assert.Contains("{1}", doc.RootElement.GetProperty("distance").GetString());
        }
    }

    [Fact]
    public void MarksAndFlightTonesBecomeAudioEvents()
    {
        var game = new Game(seed: 4);
        game.JumpToEvent(EventKind.LongJump);
        var seq = new CueSequencer();
        seq.Drain(game);
        seq.Take();
        var bot = new PrankAndField.Core.Tests.Support.Bot(0, 11);
        bool sawTone = false, sawStop = false, sawMark = false;
        double t = 0;
        while (game.State != GameState.EventOutcome && t < 120)
        {
            game.Tick(bot.Next(game), InputFrame.None);
            seq.Drain(game);
            foreach (var (kind, a, b) in seq.Take())
            {
                if (kind == (double)AudioEventKind.FlightTone && b == 1) { sawTone = true; Assert.InRange(a, 0, 1000); }
                if (kind == (double)AudioEventKind.FlightTone && b == 0) sawStop = true;
                if (kind == (double)AudioEventKind.SpeakMark) { sawMark = true; Assert.Equal(0, b); Assert.InRange(a, 100, 1300); }
            }
            t += Game.TickSeconds;
        }
        Assert.True(sawTone, "flight tone while airborne");
        Assert.True(sawStop, "flight tone stops on landing");
        Assert.True(sawMark, "the mark is announced");
    }

    [Fact]
    public void MarksAreSpokenInTheUnitOfTheirEvent()
    {
        Assert.Equal(MarkSpeech.Time, CueMap.SpeechFor(EventKind.Swimming100m));
        Assert.Equal(MarkSpeech.Hits, CueMap.SpeechFor(EventKind.SkeetShooting));
        Assert.Equal(MarkSpeech.Score, CueMap.SpeechFor(EventKind.LongHorse));
        Assert.Equal(MarkSpeech.Points, CueMap.SpeechFor(EventKind.Archery));
        Assert.Equal(MarkSpeech.Distance, CueMap.SpeechFor(EventKind.TripleJump));
        Assert.Equal(MarkSpeech.Kilograms, CueMap.SpeechFor(EventKind.Weightlifting));

        var game = new Game(seed: 6);
        game.JumpToEvent(EventKind.Weightlifting);
        var seq = new CueSequencer();
        var bot = new PrankAndField.Core.Tests.Support.Bot(0, 11);
        var marks = new List<(double, double)>();
        double t = 0;
        while (game.State != GameState.EventOutcome && t < 200)
        {
            game.Tick(bot.Next(game), InputFrame.None);
            seq.Drain(game);
            foreach (var (kind, a, b) in seq.Take())
                if (kind == (double)AudioEventKind.SpeakMark) marks.Add((a, b));
            t += Game.TickSeconds;
        }
        Assert.NotEmpty(marks);
        Assert.All(marks, m => Assert.Equal((double)MarkSpeech.Kilograms, m.Item2));
        Assert.All(marks, m => Assert.Equal(0, m.Item1 % 100));

        foreach (var culture in Localizer.SupportedCultures)
        {
            using var doc = JsonDocument.Parse(AudioCatalog.VoicePhrasesJson(new Localizer(culture)));
            foreach (var key in new[] { "score", "points", "hits", "kilograms" })
                Assert.Contains("{0}", doc.RootElement.GetProperty(key).GetString());
            Assert.Contains("{1}", doc.RootElement.GetProperty("score").GetString());
        }
    }

    [Fact]
    public void HyperSportsSoundsHaveIds()
    {
        foreach (var cue in new[] { AudioCue.SfxSplash, AudioCue.SfxBreath, AudioCue.SfxGasp, AudioCue.SfxShot, AudioCue.SfxClayBreak, AudioCue.SfxBow, AudioCue.SfxArrowHit, AudioCue.SfxSpring, AudioCue.SfxClank, AudioCue.SfxLights })
        {
            var e = Assert.Single(CueMap.Map(cue));
            Assert.Equal(AudioEventKind.PlaySound, e.Kind);
            Assert.InRange(e.Id, 17, 26);
        }
        Assert.Equal(26, Enum.GetValues<SoundId>().Length);
    }

    [Fact]
    public void CatalogRoundTripsThroughJson()
    {
        string json = AudioCatalog.ToJson();
        var dto = AudioCatalog.FromJson(json);
        Assert.NotNull(dto);
        Assert.Equal(Songs.All.Count, dto!.Songs.Count);
        var title = dto.Songs.Single(s => s.Id == Songs.Title.Id);
        Assert.Equal(Songs.Title.Lead.Length, title.Lead.Count);
        Assert.Equal("PRANK", title.Lead[0].Syllable);
        Assert.True(title.Loop);
    }

    [Fact]
    public void SequencerDrainsGameCuesIntoTriplets()
    {
        var game = new Game();
        var seq = new CueSequencer();
        seq.Drain(game);
        var triplets = seq.Take();
        Assert.Contains(((double)AudioEventKind.PlaySong, (double)Songs.Title.Id, 1.0), triplets);
        Assert.Contains(((double)AudioEventKind.Speak, (double)VoiceId.Title, 0.0), triplets);
        Assert.Empty(seq.Take());
    }
}

public class GameHostTests
{
    [Fact]
    public void FrameReturnsDrawCommandsFollowedByAudio()
    {
        var host = new GameHost();
        host.Initialize(null, "es-ES");
        Assert.Equal("es-ES", host.Language);
        var buf = host.Frame(1.0 / 60, 0, 0);
        Assert.True(buf.Length > 100);
        Assert.Equal(0, buf[^1]);
        Assert.Contains(100.0, buf); // audio section with the title song
        // steady state: no audio section once cues are drained
        var next = host.Frame(1.0 / 60, 0, 0);
        Assert.DoesNotContain(100.0, next);
    }

    [Fact]
    public void RecordsAreRestoredAndLanguageFallsBack()
    {
        var records = new WorldRecords();
        records.Submit(EventKind.HighJump, 2.55, "ZZZ");
        var host = new GameHost();
        host.Initialize(records.ToJson(), "fr-FR");
        Assert.Equal("en", host.Language);
        Assert.Equal(2.55, host.Game.Records.Best(EventKind.HighJump).Mark);
        Assert.Contains("ZZZ", host.RecordsJson);
        host.SetLanguage("ca");
        Assert.Equal("ca-ES-valencia", host.Language);
        Assert.Contains("ca-ES", host.VoicePhrasesJson);
        Assert.Contains("songs", host.AudioCatalogJson);
    }
}
