namespace PrankAndField.Core;

/// <summary>
/// Sound requests emitted by the simulation. The core never plays audio; it just queues cues
/// that the audio layer maps to songs, robotic voice lines and effects.
/// </summary>
public enum AudioCue
{
    None = 0,

    // music
    SongTitle,
    SongEventIntro,
    SongEventStart,
    SongQualified,
    SongWorldRecord,
    SongFailed,
    SongCeremony,
    SongGameOver,
    SongNameEntry,
    StopMusic,

    // voice (announcer)
    VoiceTitle,
    VoiceOnYourMarks,
    VoiceGetSet,
    VoiceGo,
    VoiceFalseStart,
    VoiceFoul,
    VoiceQualified,
    VoiceNotQualified,
    VoiceNewWorldRecord,
    VoiceGameOver,
    VoiceTryNumber1,
    VoiceTryNumber2,
    VoiceTryNumber3,
    VoiceBonus,

    /// <summary>Announce the latest mark (time or distance); the value travels in <see cref="Events.EventSimulation.LastMarkForVoice"/>.</summary>
    VoiceMark,

    // effects
    SfxGun,
    SfxStep,
    SfxJump,
    SfxLand,
    SfxThrow,
    SfxHurdleHit,
    SfxBarFall,
    SfxCrowd,
    SfxBird,
    SfxMole,
    SfxBonus,
    SfxMenuMove,
    SfxMenuSelect,
    SfxWhistle,
    SfxSpin,
    SfxTick,
}
