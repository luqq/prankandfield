using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>Screen regions (logical 256x224) and world-to-screen scale, modelled on the arcade layout.</summary>
public static class Layout
{
    public const double HudTop = 0;
    public const double HudBottom = 66;
    public const double StandsTop = 66;
    public const double StandsBottom = 88;
    public const double WallTop = 88;
    public const double WallBottom = 102;
    public const double FieldTop = 102;
    public const double FieldBottom = 202;
    public const double BottomTop = 202;

    // Two lane strips in running events, with the speed band between them.
    public const double StripHeight = 45;
    public const double BandHeight = 10;
    public const double StripATop = FieldTop;                       // lane 2 (player 2 / computer)
    public const double StripBTop = FieldTop + StripHeight + BandHeight; // lane 1 (player 1)

    /// <summary>Horizontal pixels per metre: 22 keeps the arcade scroll speed (about 5 px/frame at full sprint).</summary>
    public const double PxPerMeterX = 22;

    /// <summary>Vertical pixels per metre for heights (sprites are squashed like the original).</summary>
    public const double PxPerMeterY = 11;

    /// <summary>Vertical squash applied to implement flight heights (javelin, hammer, birds) so arcs stay on screen.</summary>
    public const double FlightSquash = 0.35;

    /// <summary>Where the followed athlete sits on screen while the camera scrolls.</summary>
    public const double FollowPx = 72;

    public static double CameraFor(double worldX, double minCamera = 0, double maxCamera = double.MaxValue) =>
        Math.Clamp(worldX - FollowPx / PxPerMeterX, minCamera, maxCamera);

    public static double ToScreenX(double worldX, double cameraX) => (worldX - cameraX) * PxPerMeterX;

    public static double StripGround(double stripTop) => stripTop + 36;
}
