namespace PrankAndField.Core;

/// <summary>
/// The button-mashing locomotion model shared by every event with a run-up.
/// Speed is in cm/s like the arcade SPEED readout. Each RUN press adds an impulse;
/// alternating RUN 1 / RUN 2 gives the full impulse, repeating the same button a smaller one,
/// and speed decays exponentially so a steady tapping rhythm settles at a steady speed.
/// </summary>
public sealed class Runner
{
    public const double MaxSpeedCm = 1442;      // human max in the arcade (TAS-measured)
    public const double DragPerSecond = 0.9;    // dv/dt = -drag * v
    public const double ImpulseAlternate = 100; // cm/s per alternating press (9 taps/s settles near 1100 cm/s)
    public const double ImpulseRepeat = 28;     // cm/s per repeated press of the same button

    private int _lastButton;

    public double SpeedCm { get; set; }
    public double PositionM { get; set; }
    public double MaxSpeed { get; set; } = MaxSpeedCm;

    /// <summary>Multiplies the impulse of every press (1 for a runner; a swimmer's stroke is worth less).</summary>
    public double ImpulseScale { get; set; } = 1.0;

    /// <summary>When locked, presses are ignored and speed stays constant (used while airborne).</summary>
    public bool Locked { get; set; }

    /// <summary>Presses counted since <see cref="Reset"/> (used for animation and tests).</summary>
    public int PressCount { get; private set; }

    public double SpeedMetersPerSecond => SpeedCm / 100.0;

    public void Reset(double positionM = 0)
    {
        SpeedCm = 0;
        PositionM = positionM;
        _lastButton = 0;
        PressCount = 0;
        Locked = false;
    }

    public void Press(int button)
    {
        if (Locked) return;
        PressCount++;
        double impulse = (_lastButton == 0 || _lastButton != button ? ImpulseAlternate : ImpulseRepeat) * ImpulseScale;
        _lastButton = button;
        SpeedCm = Math.Min(MaxSpeed, SpeedCm + impulse);
    }

    public void ApplyEdges(ButtonEdges e)
    {
        if (e.Run1Pressed) Press(1);
        if (e.Run2Pressed) Press(2);
    }

    /// <summary>Advances the runner by dt seconds (decay + motion).</summary>
    public void Integrate(double dt)
    {
        if (!Locked)
        {
            SpeedCm -= SpeedCm * DragPerSecond * dt;
            if (SpeedCm < 0.5) SpeedCm = 0;
        }
        PositionM += SpeedCm / 100.0 * dt;
    }

    /// <summary>Steady-state speed for an alternating tapping frequency, handy for tuning and tests.</summary>
    public static double SteadySpeedFor(double pressesPerSecond, double impulseScale = 1.0, double maxSpeed = MaxSpeedCm) =>
        Math.Min(maxSpeed, ImpulseAlternate * impulseScale * pressesPerSecond / DragPerSecond);
}

/// <summary>Simple ballistic helper (metres, seconds).</summary>
public static class Ballistics
{
    public const double G = 9.81;

    public static double Range(double speedMps, double angleDeg, double g = G)
    {
        double a = angleDeg * Math.PI / 180.0;
        return speedMps * speedMps * Math.Sin(2 * a) / g;
    }

    public static double PeakHeight(double speedMps, double angleDeg, double g = G)
    {
        double a = angleDeg * Math.PI / 180.0;
        double vy = speedMps * Math.Sin(a);
        return vy * vy / (2 * g);
    }

    public static double FlightTime(double speedMps, double angleDeg, double g = G)
    {
        double a = angleDeg * Math.PI / 180.0;
        return 2 * speedMps * Math.Sin(a) / g;
    }

    /// <summary>Position along the parabola at time t (origin at launch, x forward, y up).</summary>
    public static (double X, double Y) At(double speedMps, double angleDeg, double t, double g = G)
    {
        double a = angleDeg * Math.PI / 180.0;
        return (speedMps * Math.Cos(a) * t, speedMps * Math.Sin(a) * t - 0.5 * g * t * t);
    }
}
