namespace PrankAndField.Core.Events;

public sealed class Hurdle
{
    public Hurdle(double x)
    {
        X = x;
    }

    public double X { get; }
    public bool Fallen { get; set; }
    public double FallAnim { get; set; }
}

/// <summary>
/// 110 metre hurdles: a dash with ten barriers, jumped with the action button. A jump locks the RUN buttons for
/// <see cref="Rules.HurdleAirTime"/> and costs speed the way the arcade did (300 cm/s at take-off, 200 back on landing),
/// so the heat is won in the three strides between hurdles, where every press adds <see cref="Rules.HurdleImpulseScale"/>
/// times the dash impulse. Jumping a little early only clips the hurdle; running into one is a stumble.
/// </summary>
public sealed class HurdlesSimulation : RaceSimulation
{
    private const double NoLanding = -100;

    private readonly double[] _airTime = new double[2];
    private readonly double[] _takeoffSpeed = new double[2];
    private readonly double[] _landingX = [NoLanding, NoLanding];
    private readonly int[] _nextHurdle = new int[2];
    private readonly Hurdle[][] _hurdles = new Hurdle[2][];

    public HurdlesSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.Hurdles110m, round, athletes, rng, Rules.HurdlesDistance)
    {
        foreach (var a in Athletes) a.Runner.ImpulseScale = Rules.HurdleImpulseScale;
        for (int lane = 0; lane < 2; lane++) _hurdles[lane] = MakeHurdles();
    }

    /// <summary>Apex of a hurdle jump in metres (a hurdle is 1.07 m; the athlete must clearly sail over it).</summary>
    public const double JumpHeight = 1.45;

    public IReadOnlyList<Hurdle> HurdlesFor(int playerIndex) => _hurdles[playerIndex];

    public bool IsAirborne(int playerIndex) => _airTime[playerIndex] > 0;

    public int FallenCount(int playerIndex) => _hurdles[playerIndex].Count(h => h.Fallen);

    /// <summary>Speed (cm/s) a runner keeps in the air after taking off at <paramref name="speedCm"/>.</summary>
    public static double AirborneSpeed(double speedCm) => Math.Max(0, speedCm - Rules.HurdleTakeoffPenalty);

    public override double? FlightHeight
    {
        get
        {
            var human = Athletes.FirstOrDefault(a => !a.IsCpu) ?? Athletes[0];
            return _airTime[human.PlayerIndex] > 0 ? Math.Clamp(human.Y / JumpHeight, 0, 1) : null;
        }
    }

    private static Hurdle[] MakeHurdles()
    {
        var list = new Hurdle[Rules.HurdleCount];
        for (int i = 0; i < Rules.HurdleCount; i++) list[i] = new Hurdle(Rules.FirstHurdle + i * Rules.HurdleSpacing);
        return list;
    }

    protected override void ResetCourse()
    {
        for (int lane = 0; lane < 2; lane++)
        {
            _hurdles[lane] = MakeHurdles();
            _airTime[lane] = 0;
            _takeoffSpeed[lane] = 0;
            _landingX[lane] = NoLanding;
            _nextHurdle[lane] = 0;
        }
    }

    protected override void TickAthleteMotion(Athlete a, ButtonEdges? e, double dt)
    {
        int p = a.PlayerIndex;
        var hurdles = _hurdles[p];
        foreach (var h in hurdles)
        {
            if (h.Fallen && h.FallAnim < 1) h.FallAnim = Math.Min(1, h.FallAnim + dt * 4);
        }

        bool wantsJump;
        if (e is not null)
        {
            wantsJump = e.ActionPressed;
        }
        else
        {
            // Computer: take off so the hurdle is crossed around the middle of the arc at the speed it keeps in the air.
            wantsJump = false;
            if (_nextHurdle[p] < hurdles.Length)
            {
                double v = AirborneSpeed(a.Runner.SpeedCm) / 100.0;
                double gap = hurdles[_nextHurdle[p]].X - a.X;
                if (gap > 0 && gap <= v * Rules.HurdleAirTime * 0.5) wantsJump = true;
            }
        }

        if (wantsJump && _airTime[p] <= 0 && a.LockTimer <= 0 && !a.Finished)
        {
            _airTime[p] = Rules.HurdleAirTime;
            _takeoffSpeed[p] = a.Runner.SpeedCm;
            a.Runner.SpeedCm = AirborneSpeed(a.Runner.SpeedCm);
            a.Runner.Locked = true;
            a.Pose = Pose.Hurdle;
            Cue(AudioCue.SfxJump);
        }

        if (_airTime[p] > 0)
        {
            _airTime[p] -= dt;
            double progress = 1 - _airTime[p] / Rules.HurdleAirTime;
            a.Y = 4 * JumpHeight * progress * (1 - progress);
            if (_airTime[p] <= 0) Land(a);
        }

        // Crossing the next hurdle?
        if (_nextHurdle[p] < hurdles.Length)
        {
            var h = hurdles[_nextHurdle[p]];
            double step = a.Runner.SpeedMetersPerSecond * dt;
            if (a.X + step >= h.X)
            {
                _nextHurdle[p]++;
                if (_airTime[p] > 0)
                {
                    double progress = 1 - _airTime[p] / Rules.HurdleAirTime;
                    if (progress < Rules.HurdleClearMin || progress > Rules.HurdleClearMax) Clip(a, h);
                }
                else if (h.X - _landingX[p] <= Rules.HurdleGraceDistance)
                {
                    // Jumped a touch early and came down just short of it: the trailing leg knocks it over.
                    Clip(a, h);
                }
                else
                {
                    h.Fallen = true;
                    a.Runner.SpeedCm = Rules.HurdleMajorHitSpeed;
                    a.LockTimer = Rules.StumbleDuration;
                    a.Pose = Pose.Stumble;
                    Cue(AudioCue.SfxHurdleHit);
                }
            }
        }
    }

    private void Clip(Athlete a, Hurdle h)
    {
        h.Fallen = true;
        a.Runner.SpeedCm = Math.Max(0, a.Runner.SpeedCm - Rules.HurdleMinorHitPenalty);
        Cue(AudioCue.SfxHurdleHit);
    }

    /// <summary>
    /// Back on the track. The landing push gives <see cref="Rules.HurdleLandingBoost"/> back, but never lifts the runner
    /// above the take-off speed minus the net cost of a hurdle: a hop from a standstill is worth nothing, and the computer,
    /// which keeps building speed in the air, only gets what it still lacks.
    /// </summary>
    private void Land(Athlete a)
    {
        int p = a.PlayerIndex;
        double ceiling = Math.Max(0, _takeoffSpeed[p] - (Rules.HurdleTakeoffPenalty - Rules.HurdleLandingBoost));
        double boost = Math.Clamp(ceiling - a.Runner.SpeedCm, 0, Rules.HurdleLandingBoost);
        a.Runner.SpeedCm = Math.Min(a.Runner.MaxSpeed, a.Runner.SpeedCm + boost);
        a.Runner.Locked = false;
        a.Y = 0;
        _airTime[p] = 0;
        _takeoffSpeed[p] = 0;
        _landingX[p] = a.X;
        a.Pose = Pose.Run;
    }

    protected override void OnRaceFinished()
    {
        // The results phase only lets the runners coast: whoever crossed the line in the air comes down now.
        foreach (var a in Athletes)
        {
            if (_airTime[a.PlayerIndex] > 0) Land(a);
        }
        base.OnRaceFinished();
    }

    public override void AddChecksum(ref StateHash h)
    {
        base.AddChecksum(ref h);
        for (int p = 0; p < 2; p++)
        {
            h.Add(_airTime[p]);
            h.Add(_takeoffSpeed[p]);
            h.Add(_landingX[p]);
            h.Add(_nextHurdle[p]);
            foreach (var hurdle in _hurdles[p]) h.Add(hurdle.Fallen);
        }
    }
}
