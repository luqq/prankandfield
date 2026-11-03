namespace PrankAndField.Engine.Graphics;

/// <summary>The arcade palette, sampled from the original attract screens, plus the shades used for depth and light.</summary>
public static class Palette
{
    public static readonly Rgba Black = Rgba.Hex(0x000000);
    public static readonly Rgba White = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Grey = Rgba.Hex(0x9A9A9A);
    public static readonly Rgba DarkGrey = Rgba.Hex(0x5A5A5A);
    public static readonly Rgba LightGrey = Rgba.Hex(0xC8C8C8);

    /// <summary>Dark ink around sprites and furniture so they read against any background.</summary>
    public static readonly Rgba Outline = Rgba.Hex(0x16122A);
    public static readonly Rgba Shadow = Rgba.Hex(0x000000, 70);

    // HUD
    public static readonly Rgba HudFrame = Rgba.Hex(0xE03838);
    public static readonly Rgba HudFrameLight = Rgba.Hex(0xFF7A7A);
    public static readonly Rgba HudFrameDark = Rgba.Hex(0x8A1C1C);
    public static readonly Rgba HudPanel = Rgba.Hex(0x0B1022);
    public static readonly Rgba HudPanelDark = Rgba.Hex(0x04060E);
    public static readonly Rgba HudBox = Rgba.Hex(0x0E1A34, 220);
    public static readonly Rgba HudCyan = Rgba.Hex(0x40E8E8);
    public static readonly Rgba HudYellow = Rgba.Hex(0xF8E858);
    public static readonly Rgba HudGreen = Rgba.Hex(0x48E048);
    public static readonly Rgba HudOrange = Rgba.Hex(0xF8A040);
    public static readonly Rgba HudBlue = Rgba.Hex(0x4860F8);
    public static readonly Rgba HudPink = Rgba.Hex(0xF890B0);
    public static readonly Rgba HudRed = Rgba.Hex(0xF84040);

    // sky (title, ceremony)
    public static readonly Rgba SkyTop = Rgba.Hex(0x070B24);
    public static readonly Rgba SkyMid = Rgba.Hex(0x3A2060);
    public static readonly Rgba SkyHorizon = Rgba.Hex(0xF07A3A);
    public static readonly Rgba SkyGlow = Rgba.Hex(0xFFC46A);
    public static readonly Rgba Sun = Rgba.Hex(0xFFE08A);
    public static readonly Rgba Star = Rgba.Hex(0xF8F8FF);

    // stadium
    public static readonly Rgba Track = Rgba.Hex(0xC8684A);
    public static readonly Rgba TrackLight = Rgba.Hex(0xD98062);
    public static readonly Rgba TrackDark = Rgba.Hex(0xA85238);
    public static readonly Rgba LaneLine = Rgba.Hex(0xF4F4F4);
    public static readonly Rgba Grass = Rgba.Hex(0x3CB043);
    public static readonly Rgba GrassStripe = Rgba.Hex(0x46BE4E);
    public static readonly Rgba GrassDark = Rgba.Hex(0x2F9437);
    public static readonly Rgba Sand = Rgba.Hex(0xD8C8A0);
    public static readonly Rgba SandLight = Rgba.Hex(0xE8DCB8);
    public static readonly Rgba SandEdge = Rgba.Hex(0xB0A078);
    public static readonly Rgba Wall = Rgba.Hex(0x9C9C9C);
    public static readonly Rgba WallLight = Rgba.Hex(0xB4B4B8);
    public static readonly Rgba WallDark = Rgba.Hex(0x6E6E6E);
    public static readonly Rgba Roof = Rgba.Hex(0x3A3D4E);
    public static readonly Rgba RoofDark = Rgba.Hex(0x22242E);
    public static readonly Rgba TerraceBack = Rgba.Hex(0x565868);
    public static readonly Rgba StandRow = Rgba.Hex(0x8A8A8A);
    public static readonly Rgba StandRowAlt = Rgba.Hex(0x7A7A7A);
    public static readonly Rgba Rail = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba Floodlight = Rgba.Hex(0xFFF6C8);
    public static readonly Rgba FloodGlow = Rgba.Hex(0xFFE890);
    public static readonly Rgba Mat = Rgba.Hex(0x3858E8);
    public static readonly Rgba MatLight = Rgba.Hex(0x5A78FF);
    public static readonly Rgba MatDark = Rgba.Hex(0x2840B0);
    public static readonly Rgba Bar = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Upright = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba UprightBase = Rgba.Hex(0x505050);
    /// <summary>Pale band painted on the high-jump apron where the take-off is legal.</summary>
    public static readonly Rgba TakeoffZone = Rgba.Hex(0xE0A080);
    public static readonly Rgba Hurdle = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba HurdleBar = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba HurdleStripe = Rgba.Hex(0x3050E0);
    public static readonly Rgba Sky = Rgba.Hex(0x101828);
    public static readonly Rgba Cage = Rgba.Hex(0xB0B0B0);
    public static readonly Rgba Board = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Plasticine = Rgba.Hex(0xE8E870);
    public static readonly Rgba Scoreboard = Rgba.Hex(0x0A0A12);
    public static readonly Rgba ScoreboardText = Rgba.Hex(0xFFB048);

    public static readonly Rgba[] Crowd =
    [
        Rgba.Hex(0xF04040), Rgba.Hex(0x40E040), Rgba.Hex(0x4060F0), Rgba.Hex(0xF0E040),
        Rgba.Hex(0xF08040), Rgba.Hex(0x40E0E0), Rgba.Hex(0xF040F0), Rgba.Hex(0xF8F8F8),
        Rgba.Hex(0xF0C090), Rgba.Hex(0x90F090), Rgba.Hex(0x9090F0), Rgba.Hex(0xE0E0E0),
        Rgba.Hex(0x3030A0), Rgba.Hex(0xA03030), Rgba.Hex(0x30A080), Rgba.Hex(0xE0A0C0),
    ];

    /// <summary>Skin tones of the crowd, so the terraces look like people rather than confetti.</summary>
    public static readonly Rgba[] CrowdSkin =
    [
        Rgba.Hex(0xF2C4A0), Rgba.Hex(0xE0A880), Rgba.Hex(0xB07850), Rgba.Hex(0x7C4A2A), Rgba.Hex(0xF8D8B8),
    ];

    // athletes
    public static readonly Rgba Skin = Rgba.Hex(0xF2C4A0);
    /// <summary>Player 2 has a darker skin tone, like the second runner of the arcade original.</summary>
    public static readonly Rgba SkinP2 = Rgba.Hex(0x9C5C34);
    public static readonly Rgba HairBrown = Rgba.Hex(0x4A2A12);
    public static readonly Rgba HairBlack = Rgba.Hex(0x1A1A1A);
    public static readonly Rgba Mustache = Rgba.Hex(0x30200A);
    public static readonly Rgba Singlet = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba SingletShade = Rgba.Hex(0xD8DCE8);
    public static readonly Rgba ShortsP1 = Rgba.Hex(0x3050F0);
    public static readonly Rgba ShortsP2 = Rgba.Hex(0xF03030);
    public static readonly Rgba Shoe = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Number = Rgba.Hex(0x303030);

    // implements
    public static readonly Rgba Javelin = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba JavelinTip = Rgba.Hex(0xA0A0A0);
    public static readonly Rgba Hammer = Rgba.Hex(0x404040);
    public static readonly Rgba HammerShine = Rgba.Hex(0x8A8A96);
    public static readonly Rgba HammerHot = Rgba.Hex(0xF84040);
    public static readonly Rgba Wire = Rgba.Hex(0xC0C0C0);

    // effects
    public static readonly Rgba Dust = Rgba.Hex(0xE2D2B0);
    public static readonly Rgba SandSpray = Rgba.Hex(0xEADFC0);
    public static readonly Rgba Spark = Rgba.Hex(0xFFF0A0);
    public static readonly Rgba Flash = Rgba.Hex(0xFFFFFF);

    // critters
    public static readonly Rgba Mole = Rgba.Hex(0x6A4A2A);
    public static readonly Rgba MoleNose = Rgba.Hex(0xF0A0A0);
    public static readonly Rgba Bird = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba BirdBeak = Rgba.Hex(0xF8A020);

    // flags
    public static readonly Rgba FlagRed = Rgba.Hex(0xE02020);
    public static readonly Rgba FlagBlue = Rgba.Hex(0x2040C0);
    public static readonly Rgba FlagYellow = Rgba.Hex(0xF8D020);
    public static readonly Rgba FlagGreen = Rgba.Hex(0x20A040);
    public static readonly Rgba FlagGold = Rgba.Hex(0xF8D020);
    public static readonly Rgba FlagSilver = Rgba.Hex(0xD0D0D8);
    public static readonly Rgba FlagBronze = Rgba.Hex(0xC08040);
}
