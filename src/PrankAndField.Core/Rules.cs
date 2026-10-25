namespace PrankAndField.Core;

/// <summary>
/// Qualifying standards and tunables. Round 1 matches the arcade defaults
/// (100m 16.00, long jump 6.00, javelin 60.00, hurdles 14.50, hammer 73.00, high jump 2.30);
/// later rounds ramp up more gently than the Centuri board so the game stays fun on a keyboard.
/// </summary>
public static class Rules
{
    public const int MaxRound = 4;
    public const int TriesPerFieldEvent = 3;
    public const double FieldAttemptTimeout = 20;      // seconds before an idle attempt is fouled
    public const int FalseStartsAllowed = 2;
    public const int InitialChances = 1;
    public const long ExtraChanceEvery = 100_000;
    public const int BonusPoints = 1000;
    public const int WorldRecordBonus = 3000;
    public const int TieBonus = 1000;

    private static readonly double[][] Qualifying =
    [
        // round 1, 2, 3, 4+
        [16.00, 14.00, 13.00, 12.50], // 100 m dash (seconds)
        [6.00, 7.00, 7.50, 8.00],     // long jump (metres)
        [60.00, 70.00, 80.00, 85.00], // javelin (metres)
        [14.50, 13.50, 13.00, 12.50], // 110 m hurdles (seconds)
        [73.00, 78.00, 81.00, 85.00], // hammer (metres)
        [2.30, 2.34, 2.38, 2.40],     // high jump (metres)
    ];

    public static double QualifyingMark(EventKind kind, int round)
    {
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        var row = Qualifying[(int)kind];
        return row[Math.Min(round, row.Length) - 1];
    }

    /// <summary>True when <paramref name="mark"/> satisfies the qualifying standard of the event.</summary>
    public static bool Qualifies(EventKind kind, int round, double mark)
    {
        double q = QualifyingMark(kind, round);
        return kind.IsTimed() ? mark <= q + 1e-9 : mark >= q - 1e-9;
    }

    /// <summary>Whether mark a is better than mark b for the given event.</summary>
    public static bool IsBetter(EventKind kind, double a, double b) => kind.IsTimed() ? a < b - 1e-9 : a > b + 1e-9;

    /// <summary>Rounds a mark to the arcade precision: hundredths (seconds or metres).</summary>
    public static double Snap(double mark) => Math.Round(mark, 2, MidpointRounding.AwayFromZero);

    // --- Dash / hurdles -------------------------------------------------------------------
    public const double DashDistance = 100.0;
    public const double HurdlesDistance = 110.0;
    public const double FirstHurdle = 13.72;
    public const double HurdleSpacing = 9.14;
    public const int HurdleCount = 10;
    public const double HurdleAirTime = 0.6;
    public const double HurdleClearMin = 0.1;          // fraction of the arc where the barrier may be crossed safely
    public const double HurdleClearMax = 0.9;
    public const double HurdleTakeoffPenalty = 150;    // cm/s
    public const double HurdleMinorHitPenalty = 300;   // cm/s
    public const double HurdleMajorHitSpeed = 200;     // cm/s set on a full collision
    public const double StumbleDuration = 0.6;

    // --- Long jump -------------------------------------------------------------------------
    public const double LongJumpBoard = 40.0;          // metres from start to the take-off board
    public const double LongJumpFoulTolerance = 0.0;   // foot past the board = foul
    public const double LongJumpPitStart = 1.0;        // metres past the board where the sand begins; landing short of it = foul
    public const double LongJumpOvershoot = 0.6;       // running this far past the board without jumping = foul
    public const double LongJumpBoardBonusZone = 0.3;  // metres before the board that give a speed bonus
    public const double LongJumpBoardBonus = 50;       // cm/s
    public const double MoleZone = 0.03;               // take-off within 3 cm of the board = mole bonus
    public const double AngleRateUp = 110;             // degrees per second while holding

    public static double LongJumpDistance(double speedMps, double angleDeg) =>
        Math.Max(0.5, 0.5 * Ballistics.Range(speedMps, angleDeg) + 2.0); // 9 m/s at 45 degrees ~ 6.1 m, 11.8 m/s ~ 9.0 m

    // --- Javelin ----------------------------------------------------------------------------
    public const double JavelinLine = 30.0;
    public const double JavelinArmSpeed = 11.0;        // m/s added by the arm (a 7.8 m/s run-up at 45 degrees clears 60 m)
    public const double JavelinRunFactor = 0.75;
    public const double JavelinRunHoldBonus = 0.20;    // up to +20% when holding RUN during flight
    public const double JavelinBirdApex = 20.0;        // metres: higher than this hits the bird
    public const double JavelinAngleRate = 150;        // degrees per second while holding (faster than the jump)

    public static double JavelinLaunchSpeed(double runSpeedMps) => runSpeedMps * JavelinRunFactor + JavelinArmSpeed;

    public static double JavelinDistance(double launchSpeedMps, double angleDeg) =>
        Math.Max(1.0, 2.3 * Ballistics.Range(launchSpeedMps, angleDeg));

    // --- Hammer -----------------------------------------------------------------------------
    public const int HammerMaxSpins = 10;
    public const double HammerReleaseArc = 45;         // degrees either side of straight ahead: the ball glows red here (full distance)
    public const double HammerFoulArc = 90;            // beyond this (ball behind the thrower) the release is a foul
    /// <summary>Launch angle gained per degree of hammer rotation while holding: press behind, release in front = 45 degrees.</summary>
    public const double HammerAnglePerDegreeTurned = 0.25;

    public static double HammerSpinSpeed(int spinsCompleted) => 0.6 + 0.15 * spinsCompleted; // revolutions per second (1.5 at six turns)

    /// <summary>Distance for a release straight down the sector.</summary>
    public static double HammerDistance(int spinsCompleted, double angleDeg, double luck) =>
        Math.Max(5.0, (32 + 8.5 * spinsCompleted) * Math.Sin(2 * angleDeg * Math.PI / 180.0) * luck);

    /// <summary>Penalty for releasing off-centre: 1 straight ahead, about 0.65 at the edge of the front half.</summary>
    public static double HammerDirectionFactor(double headingOffCentreDeg)
    {
        double h = Math.Min(HammerFoulArc, Math.Abs(headingOffCentreDeg)) / HammerFoulArc;
        return 1 - 0.35 * h * h;
    }

    // --- High jump --------------------------------------------------------------------------
    public const double HighJumpApproachStart = -14.0;
    public const double HighJumpApproachSpeed = 650;   // cm/s, set by the computer in the arcade
    public const double HighJumpTakeoffMin = -2.5;     // metres before the bar
    public const double HighJumpTakeoffMax = -0.3;
    public const double HighJumpAngleStart = 80;
    public const double HighJumpAngleMin = 20;
    public const double HighJumpAngleRateDown = 60;
    public const double HighJumpAirTime = 1.0;
    public const int HighJumpMaxMashes = 14;
    public const double HighJumpMashHeight = 0.015;
    public const double HighJumpBarStep = 0.04;

    public static double HighJumpPeak(double angleDeg, int mashes) =>
        1.95 + 0.5 * Math.Sin(angleDeg * Math.PI / 180.0) + HighJumpMashHeight * Math.Min(mashes, HighJumpMaxMashes);

    public static double HighJumpTravel(double angleDeg) => 1.2 + 1.6 * Math.Cos(angleDeg * Math.PI / 180.0);
}
