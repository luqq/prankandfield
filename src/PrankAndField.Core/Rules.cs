namespace PrankAndField.Core;

/// <summary>
/// Qualifying standards and tunables. Round 1 of Track &amp; Field matches the arcade defaults
/// (100m 16.00, long jump 6.00, javelin 60.00, hurdles 14.50, hammer 73.00, high jump 2.30);
/// later rounds ramp up more gently than the Centuri board so the game stays fun on a keyboard.
/// The Hyper Sports rows follow the same spirit: a relaxed first round, then tougher standards.
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
        // Hyper Sports
        [32.00, 29.00, 27.50, 26.50], // 100 m swim (seconds)
        [14, 16, 17, 18],             // skeet shooting (hits out of 20)
        [8.50, 9.00, 9.30, 9.60],     // long horse (score out of 10)
        [30, 38, 44, 50],             // archery (points, six arrows of up to 10)
        [12.50, 13.50, 14.50, 15.50], // triple jump (metres)
        [150, 170, 185, 200],         // weightlifting (kilograms)
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
    // A hurdle is cleared with one press of JUMP. The RUN buttons do nothing while the hurdler is in the air, so the race
    // is won in the three strides between barriers: there every press is worth more than in the dash, the flight is short
    // and a clean jump costs the arcade's net 100 cm/s (300 lost at take-off, 200 back on landing). With the test bot a
    // rhythm of 8 taps/s runs about 13.0 s (the dash takes 12.6 at the same rhythm), 9 taps/s about 11.3 and 10 or more
    // about 10.5, where the pace sits at the arcade maximum between hurdles; 7 taps/s stays just over the 14.50 standard.
    public const double HurdleAirTime = 0.45;          // seconds off the ground
    public const double HurdleClearMin = 0.1;          // fraction of the arc where the barrier may be crossed safely
    public const double HurdleClearMax = 0.9;
    public const double HurdleTakeoffPenalty = 300;    // cm/s lost when the jump begins (arcade figure)
    public const double HurdleLandingBoost = 200;      // cm/s given back on landing (arcade figure), never more than the take-off took
    public const double HurdleImpulseScale = 1.4;      // a RUN press between hurdles adds 140 cm/s instead of the dash's 100
    public const double HurdleMinorHitPenalty = 300;   // cm/s
    public const double HurdleMajorHitSpeed = 200;     // cm/s set on a full collision
    public const double StumbleDuration = 0.6;

    /// <summary>Fastest time the computer runner aims for in a heat, per timed event (seconds).</summary>
    public static double CpuBestTime(EventKind kind) => kind switch
    {
        EventKind.Dash100m => 9.6,
        EventKind.Hurdles110m => 12.6,
        EventKind.Swimming100m => 24.0,
        _ => 10.0,
    };

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
    // The thrower runs up, presses THROW before the line to plant his feet and keeps it held: a dial climbs
    // from 20 to 70 degrees and the javelin leaves his hand when the button is released, or by itself when
    // the dial tops out. The mark is measured from the foul line and nothing in flight changes it.
    public const double JavelinLine = 30.0;
    public const double JavelinArmSpeed = 11.0;        // m/s added by the arm
    public const double JavelinRunFactor = 0.75;
    public const double JavelinRangeFactor = 2.5;      // arcade scale on the ballistic range: 7 taps/s at 45 degrees throws ~73 m, 6 taps/s ~61 m, 9 taps/s ~86 m
    public const double JavelinBirdApex = 20.0;        // metres: higher than this hits the bird
    public const double JavelinAngleStart = 20;        // the dial starts here when the thrower plants: nobody throws flat
    public const double JavelinAngleMax = 70;          // ...and throws by itself when it gets here, so nobody parks at a bad angle
    public const double JavelinAngleRate = 60;         // degrees per second while holding: 45 comes 0.42 s after the plant, 70 at 0.83 s
    public const double JavelinBestAngle = 45;         // longest throw (the range curve is symmetric around it: 20 and 70 throw the same)

    public static double JavelinLaunchSpeed(double runSpeedMps) => runSpeedMps * JavelinRunFactor + JavelinArmSpeed;

    public static double JavelinDistance(double launchSpeedMps, double angleDeg) =>
        Math.Max(1.0, JavelinRangeFactor * Ballistics.Range(launchSpeedMps, angleDeg));

    /// <summary>Seconds the dial needs to climb from <see cref="JavelinAngleStart"/> to <paramref name="angleDeg"/>.</summary>
    public static double JavelinHoldFor(double angleDeg) =>
        (Math.Clamp(angleDeg, JavelinAngleStart, JavelinAngleMax) - JavelinAngleStart) / JavelinAngleRate;

    // --- Hammer -----------------------------------------------------------------------------
    public const int HammerMaxSpins = 10;
    public const double HammerReleaseArc = 45;         // degrees either side of straight ahead: the ball glows red here (full distance)
    public const double HammerFoulArc = 90;            // beyond this (ball behind the thrower) the release is a foul

    /// <summary>
    /// Spin speed in revolutions per second after the given (fractional) number of turns. It grows continuously
    /// rather than stepping at every revolution: a short wind-up over the first half turn, then 0.6 plus 0.15 per
    /// turn (1.5 after six turns, 2.1 at the dizzy limit).
    /// </summary>
    public static double HammerSpinSpeed(double revolutions)
    {
        double r = Math.Max(0, revolutions);
        return 0.6 + 0.15 * r - 0.3 * Math.Max(0, 1 - 2 * r);
    }

    /// <summary>
    /// Distance of a release straight down the sector after the given (fractional) number of turns. There is no
    /// launch angle to set: every turn adds 8.5 m, so five turns reach the round-1 standard and seven the round-4 one.
    /// </summary>
    public static double HammerDistance(double revolutions, double luck) =>
        Math.Max(5.0, (30 + 8.5 * Math.Max(0, revolutions)) * luck);

    /// <summary>Penalty for releasing off-centre: 1 straight ahead, about 0.65 at the edge of the front half.</summary>
    public static double HammerDirectionFactor(double headingOffCentreDeg)
    {
        double h = Math.Min(HammerFoulArc, Math.Abs(headingOffCentreDeg)) / HammerFoulArc;
        return 1 - 0.35 * h * h;
    }

    // --- High jump --------------------------------------------------------------------------
    // The jumper runs up on his own, takes off inside a zone painted on the apron, holds the button
    // to set the angle and clears a bar that rises after every valid jump. The bar plane is x = 0.
    public const double HighJumpApproachStart = -18.0;  // metres before the bar where the run-up starts
    public const double HighJumpZoneFar = -1.9;         // take-off zone painted on the apron: from here...
    public const double HighJumpZoneNear = -0.4;        // ...to here (a late take-off past it still counts, with less lift)
    public const double HighJumpCrashX = -0.15;         // still on the ground here: he runs into the uprights
    public const double HighJumpZoneBest = -1.15;       // the white line in the middle: full lift
    public const double HighJumpZonePenalty = 0.06;     // share of the lift lost at the edges of the zone
    public const double HighJumpEarlyFalloff = 0.5;     // extra share lost per (0.5 m)^2 of take-off before the zone
    public const double HighJumpAngleStart = 30;        // the meter starts here: nobody takes off flat
    public const double HighJumpAngleRate = 110;        // degrees per second while holding, like the long jump
    public const double HighJumpBestAngle = 68;         // centre of the lift curve (with the sine the optimum is ~75)
    public const double HighJumpAngleTolerance = 55;    // degrees from the best angle at which the lift would be gone
    public const double HighJumpHipHeight = 1.1;        // body centre above the ground when leaving it
    public const double HighJumpBodySag = 0.25;         // how far the draped limbs hang under the body centre over the bar
    public const double HighJumpBarStep = 0.04;         // the bar rises this much after every valid jump
    public const double HighJumpMinFlight = 0.55;       // seconds: even a hopeless hop takes this long to watch

    /// <summary>Take-off speed (m/s) a run-up at <paramref name="runSpeedMps"/> converts into: 8 m/s -> 5.8, 10 m/s -> 6.1.</summary>
    public static double HighJumpTakeoffSpeed(double runSpeedMps) => 4.6 + 0.145 * runSpeedMps;

    /// <summary>Share of the take-off speed that becomes lift at this angle: 1 at the best angle, 0.84 at 90 degrees.</summary>
    public static double HighJumpEfficiency(double angleDeg)
    {
        double d = (angleDeg - HighJumpBestAngle) / HighJumpAngleTolerance;
        return Math.Max(0.1, 1 - d * d);
    }

    /// <summary>Lift kept for a take-off at <paramref name="takeoffX"/>: 1 on the line, 0.94 at the edges of the zone, falling fast before it.</summary>
    public static double HighJumpTakeoffFactor(double takeoffX)
    {
        double half = (HighJumpZoneNear - HighJumpZoneFar) / 2;
        double d = (Math.Max(takeoffX, HighJumpZoneFar) - HighJumpZoneBest) / half;
        double f = 1 - HighJumpZonePenalty * d * d;
        if (takeoffX < HighJumpZoneFar)
        {
            double early = (HighJumpZoneFar - takeoffX) / 0.5;
            f -= HighJumpEarlyFalloff * early * early;
        }
        return Math.Clamp(f, 0.05, 1);
    }

    /// <summary>Vertical take-off speed in m/s before the take-off position is accounted for.</summary>
    public static double HighJumpVerticalSpeed(double runSpeedMps, double angleDeg) =>
        HighJumpTakeoffSpeed(runSpeedMps) * HighJumpEfficiency(angleDeg) * Math.Sin(angleDeg * Math.PI / 180.0);

    /// <summary>How far the body centre rises above <see cref="HighJumpHipHeight"/>, in metres.</summary>
    public static double HighJumpRise(double runSpeedMps, double angleDeg, double takeoffX)
    {
        double vy = HighJumpVerticalSpeed(runSpeedMps, angleDeg);
        return vy * vy / (2 * Ballistics.G) * HighJumpTakeoffFactor(takeoffX);
    }

    /// <summary>Highest point of the body centre for this jump.</summary>
    public static double HighJumpPeak(double runSpeedMps, double angleDeg, double takeoffX) =>
        HighJumpHipHeight + HighJumpRise(runSpeedMps, angleDeg, takeoffX);

    /// <summary>The bar stays up when the body centre passes it with <see cref="HighJumpBodySag"/> to spare.</summary>
    public static bool HighJumpClears(double bodyCentreAtBar, double barHeight) => bodyCentreAtBar - HighJumpBodySag >= barHeight - 1e-9;

    // ======================================================================================
    // Hyper Sports
    // ======================================================================================

    // --- 100 m swim -----------------------------------------------------------------------
    // The arcade swim is about twice as fast as real life, so strokes behave like a slow sprint.
    public const double SwimDistance = 100.0;
    public const double SwimMaxSpeed = 440;            // cm/s cap (11 taps/s swim ~24.6 s, a frantic 16 taps/s ~23.5 s)
    public const double SwimImpulseScale = 0.4;        // each stroke adds 40 cm/s instead of the runner's 100
    public const double SwimCpuAcceleration = 300;     // cm/s^2 for the computer swimmer
    public const double SwimBreathMinInterval = 2.4;   // seconds between BREATHE prompts
    public const double SwimBreathMaxInterval = 3.4;
    public const double SwimFirstBreath = 2.6;         // seconds after the gun until the first prompt
    public const double SwimBreathWindow = 0.9;        // seconds the prompt stays up
    public const double SwimBreathBoost = 30;          // cm/s for a well-timed breath
    public const double SwimChokeFactor = 0.45;        // speed kept when a breath is missed
    public const double SwimChokeLock = 0.5;           // seconds without strokes after choking
    public const double SwimWastedBreathFactor = 0.85; // speed kept when breathing with no prompt

    // --- Skeet shooting ---------------------------------------------------------------------
    // The field is drawn with a fixed camera: x in metres from the left edge, y in metres above the ground.
    public const int SkeetTargets = 20;
    public const int SkeetDoublesFrom = 11;            // from this target on, both houses release together
    public const int SkeetGoldenEvery = 7;             // every seventh target is golden (bonus when hit)
    public const double SkeetFieldWidth = 11.6;        // 256 px / 22 px per metre
    public const double SkeetLeftHouse = 0.6;
    public const double SkeetRightHouse = 11.0;
    public const double SkeetLeftSight = 3.7;          // the two fixed sights (gun positions)
    public const double SkeetRightSight = 7.9;
    public const double SkeetSightHeight = 2.6;
    public const double SkeetLaunchHeight = 1.0;
    public const double SkeetTargetSpeedX = 5.0;       // m/s, towards the far side
    public const double SkeetTargetSpeedY = 5.6;       // m/s upwards at launch: the apex is at the sight
    public const double SkeetHitRadius = 0.62;         // metres around the sight where a shot connects
    public const double SkeetFirstLaunch = 1.2;        // seconds before the first target
    public const double SkeetLaunchIntervalStart = 1.7;
    public const double SkeetLaunchIntervalEnd = 1.05;
    public const double SkeetReload = 0.3;             // seconds between shots of the same gun
    public const int SkeetStreakBonusEvery = 5;        // consecutive hits that pay a bonus
    public const int SkeetStreakBonus = 500;

    // --- Long horse (vault) -----------------------------------------------------------------
    public const double VaultBoard = 25.0;             // metres from the start to the springboard
    public const double VaultBoardZone = 1.1;          // metres before the board where a take-off is accepted
    public const double VaultBoardSweetSpot = 0.25;    // best take-off: this far before the board
    public const double VaultHorseStart = 26.2;        // the horse spans 1.6 m past the board
    public const double VaultHorseLength = 1.6;
    public const double VaultHorseHeight = 1.35;
    public const double VaultPreFlight = 0.5;          // seconds from the board to the hands on the horse
    public const double VaultPushEarliest = 0.12;      // seconds on the horse: push window
    public const double VaultPushLatest = 0.5;
    public const double VaultPushBest = 0.3;
    public const double VaultPushAuto = 0.7;           // no press by then: a weak automatic push
    public const double VaultMinFlight = 1.0;          // seconds; a strong push adds up to 0.5
    public const double VaultMinHeight = 1.4;          // metres above the horse; a strong push adds up to 1.2
    public const int VaultMaxTwists = 4;
    public const double VaultStickWindow = 0.22;       // seconds before landing where a press "sticks" it
    public const double VaultBaseScore = 5.0;
    public const double VaultBoardWeight = 1.5;
    public const double VaultPushWeight = 1.5;
    public const double VaultTwistScore = 0.5;
    public const double VaultStickScore = 0.3;
    public const double VaultHalfTwistPenalty = 1.0;

    /// <summary>
    /// Judges' score out of 10: 5.0 base, up to 1.5 for the board, 1.5 for the push, 0.5 per whole twist (up to four) and
    /// 0.3 for sticking the landing; a half twist left over costs 1.0.
    /// </summary>
    public static double VaultScore(double board, double push, double twists, bool stuck)
    {
        double whole = Math.Floor(twists + 1e-9);
        bool half = twists - whole > 1e-9;
        double s = VaultBaseScore + VaultBoardWeight * Math.Clamp(board, 0, 1) + VaultPushWeight * Math.Clamp(push, 0, 1)
            + VaultTwistScore * Math.Min(VaultMaxTwists, whole) + (stuck ? VaultStickScore : 0) - (half ? VaultHalfTwistPenalty : 0);
        return Math.Clamp(s, 0, 10.0);
    }

    // --- Archery ----------------------------------------------------------------------------
    // World units are compressed: the target stands 10 units from the archer (drawn as 30 m).
    public const int ArcheryArrows = 6;
    public const double ArcheryTargetX = 10.0;
    public const double ArcheryTargetCentre = 1.6;     // the target bobs around this height
    public const double ArcheryTargetSwing = 1.0;      // metres above and below the centre
    public const double ArcheryTargetSpeed = 1.3;      // radians per second for the first arrow
    public const double ArcheryTargetSpeedStep = 0.12; // faster for each later arrow
    public const double ArcheryTargetRadius = 1.1;     // outer edge of the face: ten rings of ArcheryRing
    public const double ArcheryRing = 0.11;            // width of each of the ten rings (10 = centre)
    public const double ArcheryBowHeight = 1.3;        // release height
    public const double ArrowSpeed = 14.0;             // units per second
    public const double ArcheryAngleRate = 70;         // degrees per second while holding
    public const double ArcheryMaxAngle = 45;
    public const double ArcheryWindLift = 0.5;         // metres of lift (or drop) at the target per unit of wind
    public const double ArcheryTimeout = 8.0;          // seconds before an arrow is shot by itself
    public const int ArcheryBullseyeStreak = 3;        // three tens in a row pay a bonus

    /// <summary>Points for an arrow that lands <paramref name="offset"/> metres from the centre (0 = miss).</summary>
    public static int ArcheryPoints(double offset)
    {
        double d = Math.Abs(offset);
        if (d > ArcheryTargetRadius) return 0;
        return Math.Max(1, 10 - (int)Math.Floor(d / ArcheryRing + 1e-9));
    }

    // --- Triple jump ------------------------------------------------------------------------
    public const double TripleJumpBoard = 35.0;
    public const double TripleJumpPitStart = 9.0;      // metres past the board where the sand begins
    public const double TripleJumpOvershoot = 0.6;
    public const double TripleJumpContactWindow = 0.35;// seconds on the ground to press for the next phase
    public const double TripleJumpSpeedKept = 0.9;     // speed kept at each landing
    public const double TripleJumpLateSpeedKept = 0.7; // speed kept when the press comes too late
    public const double TripleJumpLateAngle = 10;

    /// <summary>Length of one phase (0 = hop, 1 = step, 2 = jump) for a take-off speed and angle.</summary>
    public static double TripleJumpPhaseDistance(int phase, double speedMps, double angleDeg)
    {
        double r = Ballistics.Range(speedMps, angleDeg);
        return phase switch
        {
            // 11 m/s at 40 degrees: hop 6.1 m, step 4.4 m (speed x0.9), jump 5.2 m (x0.81) -> 15.7 m, about twice a long jump
            0 => Math.Max(0.4, 0.40 * r + 1.2),
            1 => Math.Max(0.4, 0.35 * r + 1.0),
            _ => Math.Max(0.4, 0.50 * r + 1.2),
        };
    }

    // --- Weightlifting ----------------------------------------------------------------------
    public const double WeightStep = 5;                // kilograms added after a good lift
    public const double CleanTimeLimit = 5.0;          // seconds to bring the bar to the chest
    public const double CleanImpulseAlternate = 0.09;  // power per alternating press
    public const double CleanImpulseRepeat = 0.03;
    public const double CleanDecay = 0.25;             // power lost per second
    public const double JerkDelay = 0.15;              // seconds after the clean before the PRESS prompt
    public const double JerkWindow = 0.7;              // seconds to press once prompted
    public const double HoldSeconds = 2.5;             // seconds overhead before the lights
    public const double HoldDrift = 0.6;               // random balance drift per second
    public const double HoldInstability = 1.6;         // the tilt feeds on itself (inverted pendulum), per second
    public const double HoldNudge = 0.3;               // balance correction per RUN press
    public const double HoldLimit = 1.0;               // |balance| beyond this drops the bar

    /// <summary>Power needed to clean a bar: 1 at 150 kg, growing with the weight.</summary>
    public static double CleanPowerNeeded(double kg) => 1.0 + Math.Max(0, kg - 150) / 150.0;
}
