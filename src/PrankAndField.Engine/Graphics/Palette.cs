namespace PrankAndField.Engine.Graphics;

/// <summary>The arcade palette, sampled from the original attract screens.</summary>
public static class Palette
{
    public static readonly Rgba Black = Rgba.Hex(0x000000);
    public static readonly Rgba White = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Grey = Rgba.Hex(0x9A9A9A);
    public static readonly Rgba DarkGrey = Rgba.Hex(0x5A5A5A);
    public static readonly Rgba LightGrey = Rgba.Hex(0xC8C8C8);

    // HUD
    public static readonly Rgba HudFrame = Rgba.Hex(0xE03838);
    public static readonly Rgba HudCyan = Rgba.Hex(0x40E8E8);
    public static readonly Rgba HudYellow = Rgba.Hex(0xF8E858);
    public static readonly Rgba HudGreen = Rgba.Hex(0x48E048);
    public static readonly Rgba HudOrange = Rgba.Hex(0xF8A040);
    public static readonly Rgba HudBlue = Rgba.Hex(0x4860F8);
    public static readonly Rgba HudPink = Rgba.Hex(0xF890B0);
    public static readonly Rgba HudRed = Rgba.Hex(0xF84040);

    // stadium
    public static readonly Rgba Track = Rgba.Hex(0xC8684A);
    public static readonly Rgba TrackDark = Rgba.Hex(0xA85238);
    public static readonly Rgba LaneLine = Rgba.Hex(0xF4F4F4);
    public static readonly Rgba Grass = Rgba.Hex(0x3CB043);
    public static readonly Rgba GrassDark = Rgba.Hex(0x2F9437);
    public static readonly Rgba Sand = Rgba.Hex(0xD8C8A0);
    public static readonly Rgba SandEdge = Rgba.Hex(0xB0A078);
    public static readonly Rgba Wall = Rgba.Hex(0x9C9C9C);
    public static readonly Rgba WallDark = Rgba.Hex(0x6E6E6E);
    public static readonly Rgba StandRow = Rgba.Hex(0x8A8A8A);
    public static readonly Rgba StandRowAlt = Rgba.Hex(0x7A7A7A);
    public static readonly Rgba Rail = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba Mat = Rgba.Hex(0x3858E8);
    public static readonly Rgba MatDark = Rgba.Hex(0x2840B0);
    public static readonly Rgba Bar = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Upright = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba Hurdle = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba HurdleBar = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba HurdleStripe = Rgba.Hex(0x3050E0);
    public static readonly Rgba Sky = Rgba.Hex(0x101828);
    public static readonly Rgba Cage = Rgba.Hex(0xB0B0B0);
    public static readonly Rgba Board = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Plasticine = Rgba.Hex(0xE8E870);

    public static readonly Rgba[] Crowd =
    [
        Rgba.Hex(0xF04040), Rgba.Hex(0x40E040), Rgba.Hex(0x4060F0), Rgba.Hex(0xF0E040),
        Rgba.Hex(0xF08040), Rgba.Hex(0x40E0E0), Rgba.Hex(0xF040F0), Rgba.Hex(0xF8F8F8),
        Rgba.Hex(0xF0C090), Rgba.Hex(0x90F090), Rgba.Hex(0x9090F0), Rgba.Hex(0xE0E0E0),
    ];

    // athletes
    public static readonly Rgba Skin = Rgba.Hex(0xF2C4A0);
    /// <summary>Player 2 has a darker skin tone, like the second runner of the arcade original.</summary>
    public static readonly Rgba SkinP2 = Rgba.Hex(0x9C5C34);
    public static readonly Rgba HairBrown = Rgba.Hex(0x4A2A12);
    public static readonly Rgba HairBlack = Rgba.Hex(0x1A1A1A);
    public static readonly Rgba Mustache = Rgba.Hex(0x30200A);
    public static readonly Rgba Singlet = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba ShortsP1 = Rgba.Hex(0x3050F0);
    public static readonly Rgba ShortsP2 = Rgba.Hex(0xF03030);
    public static readonly Rgba Shoe = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba Number = Rgba.Hex(0x303030);

    // implements
    public static readonly Rgba Javelin = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba JavelinTip = Rgba.Hex(0xA0A0A0);
    public static readonly Rgba Hammer = Rgba.Hex(0x404040);
    public static readonly Rgba HammerHot = Rgba.Hex(0xF84040);
    public static readonly Rgba Wire = Rgba.Hex(0xC0C0C0);

    // critters
    public static readonly Rgba Mole = Rgba.Hex(0x6A4A2A);
    public static readonly Rgba MoleNose = Rgba.Hex(0xF0A0A0);
    public static readonly Rgba Bird = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba BirdBeak = Rgba.Hex(0xF8A020);

    // Hyper Sports
    public static readonly Rgba Water = Rgba.Hex(0x2878E0);
    public static readonly Rgba WaterDark = Rgba.Hex(0x1C5CB8);
    public static readonly Rgba WaterLight = Rgba.Hex(0x58A8F8);
    public static readonly Rgba LaneRope = Rgba.Hex(0xF8E858);
    public static readonly Rgba PoolDeck = Rgba.Hex(0xD8D8D0);
    public static readonly Rgba Clay = Rgba.Hex(0xF07028);
    public static readonly Rgba ClayDark = Rgba.Hex(0xB04810);
    public static readonly Rgba ClayGold = Rgba.Hex(0xF8D020);
    public static readonly Rgba House = Rgba.Hex(0x707880);
    public static readonly Rgba Sight = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba SightHot = Rgba.Hex(0xF84040);
    public static readonly Rgba Gun = Rgba.Hex(0x303030);
    public static readonly Rgba Flash = Rgba.Hex(0xF8F080);
    public static readonly Rgba Horse = Rgba.Hex(0xB06030);
    public static readonly Rgba HorseTop = Rgba.Hex(0xD89050);
    public static readonly Rgba HorseLeg = Rgba.Hex(0x808080);
    public static readonly Rgba Springboard = Rgba.Hex(0xE8E8E8);
    public static readonly Rgba TargetWhite = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba TargetBlack = Rgba.Hex(0x202020);
    public static readonly Rgba TargetBlue = Rgba.Hex(0x3060E0);
    public static readonly Rgba TargetRed = Rgba.Hex(0xE83030);
    public static readonly Rgba TargetGold = Rgba.Hex(0xF8D820);
    public static readonly Rgba TargetStand = Rgba.Hex(0x806040);
    public static readonly Rgba Arrow = Rgba.Hex(0xE8D8A0);
    public static readonly Rgba Fletching = Rgba.Hex(0xF84040);
    public static readonly Rgba Bow = Rgba.Hex(0x9C6830);
    public static readonly Rgba Platform = Rgba.Hex(0xA87848);
    public static readonly Rgba PlatformEdge = Rgba.Hex(0x785030);
    public static readonly Rgba Barbell = Rgba.Hex(0xC8C8C8);
    public static readonly Rgba Plate = Rgba.Hex(0xE83030);
    public static readonly Rgba PlateDark = Rgba.Hex(0x3050E0);
    public static readonly Rgba LightOn = Rgba.Hex(0xF8F8F8);
    public static readonly Rgba LightOff = Rgba.Hex(0x404040);

    // flags
    public static readonly Rgba FlagRed = Rgba.Hex(0xE02020);
    public static readonly Rgba FlagBlue = Rgba.Hex(0x2040C0);
    public static readonly Rgba FlagYellow = Rgba.Hex(0xF8D020);
    public static readonly Rgba FlagGold = Rgba.Hex(0xF8D020);
    public static readonly Rgba FlagSilver = Rgba.Hex(0xD0D0D8);
    public static readonly Rgba FlagBronze = Rgba.Hex(0xC08040);
}
