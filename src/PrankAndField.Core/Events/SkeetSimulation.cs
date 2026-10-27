namespace PrankAndField.Core.Events;

/// <summary>One clay target in the air. Positions are metres from the left edge and above the ground.</summary>
public sealed class ClayTarget
{
    public ClayTarget(int index, int side, bool golden, double x, double y)
    {
        Index = index;
        Side = side;
        Golden = golden;
        X = x;
        Y = y;
    }

    /// <summary>1-based number of the target in the round.</summary>
    public int Index { get; }

    /// <summary>0 = released from the left house (flies right), 1 = from the right house (flies left).</summary>
    public int Side { get; }

    /// <summary>A golden target pays a bonus when hit.</summary>
    public bool Golden { get; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Age { get; set; }
    public bool Hit { get; set; }
    public bool Gone { get; set; }
}

/// <summary>
/// Skeet shooting. Clay targets fly out of the two houses and cross the two fixed sights at the top of
/// their arc: RUN 1 fires the left gun, RUN 2 the right one (JUMP/THROW fires whichever has a target in
/// its sight). Twenty targets per player, doubles in the second half, a golden one every seventh.
/// </summary>
public sealed class SkeetSimulation : TurnEventSimulation
{
    private readonly double[] _reload = new double[2];
    private readonly double[] _flash = new double[2];
    private int _launched;
    private double _nextLaunch;

    public SkeetSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.SkeetShooting, round, athletes, rng)
    {
    }

    protected override double StartX => Rules.SkeetFieldWidth / 2;

    public List<ClayTarget> Targets { get; } = new();
    public int Launched => _launched;
    public int Hits { get; private set; }
    public int Shots { get; private set; }
    public int Streak { get; private set; }

    /// <summary>-1 while the shooter faces the left sight, +1 the right one.</summary>
    public int Facing { get; private set; } = -1;

    public override double RunningScore => Hits;

    public override double SpeedReadout(int playerIndex) => Streak * 150;

    /// <summary>True right after a shot from that gun (0 left, 1 right): draws the muzzle flash.</summary>
    public bool MuzzleFlash(int gun) => _flash[gun] > 0;

    public static double SightX(int gun) => gun == 0 ? Rules.SkeetLeftSight : Rules.SkeetRightSight;

    /// <summary>True while a live target is inside the hit zone of that gun.</summary>
    public bool TargetInSight(int gun) => FindInSight(gun) is not null;

    protected override void BeginTurn(Athlete a)
    {
        Targets.Clear();
        _launched = 0;
        Hits = 0;
        Shots = 0;
        Streak = 0;
        Facing = -1;
        _nextLaunch = Rules.SkeetFirstLaunch;
        _reload[0] = _reload[1] = 0;
        _flash[0] = _flash[1] = 0;
        a.X = StartX;
        a.Pose = Pose.Aim;
    }

    protected override void TickTurn(double dt, Athlete a, ButtonEdges e)
    {
        for (int g = 0; g < 2; g++)
        {
            if (_reload[g] > 0) _reload[g] -= dt;
            if (_flash[g] > 0) _flash[g] -= dt;
        }

        // releases
        if (_launched < Rules.SkeetTargets)
        {
            _nextLaunch -= dt;
            if (_nextLaunch <= 0)
            {
                bool doubles = _launched + 1 >= Rules.SkeetDoublesFrom;
                int side = Rng.Next(2);
                Launch(side);
                if (doubles && _launched < Rules.SkeetTargets) Launch(1 - side);
                double t = _launched / (double)Rules.SkeetTargets;
                _nextLaunch = Rules.SkeetLaunchIntervalStart + (Rules.SkeetLaunchIntervalEnd - Rules.SkeetLaunchIntervalStart) * t;
            }
        }

        // flight
        foreach (var t in Targets)
        {
            if (t.Hit || t.Gone) continue;
            t.Age += dt;
            double dir = t.Side == 0 ? 1 : -1;
            t.X += dir * Rules.SkeetTargetSpeedX * dt;
            t.Y = Rules.SkeetLaunchHeight + Rules.SkeetTargetSpeedY * t.Age - 0.5 * Ballistics.G * t.Age * t.Age;
            if (t.Y < 0 || t.X < -0.5 || t.X > Rules.SkeetFieldWidth + 0.5)
            {
                t.Gone = true;
                Streak = 0;
            }
        }
        Targets.RemoveAll(t => t.Gone || (t.Hit && t.Age > 100));

        // shots
        bool left = e.Run1Pressed, right = e.Run2Pressed;
        if (e.ActionPressed)
        {
            if (TargetInSight(0)) left = true;
            else if (TargetInSight(1)) right = true;
            else
            {
                // nothing in a sight yet: fire the gun of the nearest target in flight (or the one faced)
                var nearest = Targets.Where(t => !t.Hit).OrderBy(t => Math.Abs(t.X - SightX(t.Side))).FirstOrDefault();
                if (nearest is null) { if (Facing < 0) left = true; else right = true; }
                else if (nearest.Side == 0) left = true;
                else right = true;
            }
        }
        if (left) Fire(0, a);
        if (right) Fire(1, a);

        if (_launched >= Rules.SkeetTargets && Targets.All(t => t.Hit)) CompleteTurn(Hits);
    }

    private void Launch(int side)
    {
        _launched++;
        bool golden = _launched % Rules.SkeetGoldenEvery == 0;
        double x = side == 0 ? Rules.SkeetLeftHouse : Rules.SkeetRightHouse;
        Targets.Add(new ClayTarget(_launched, side, golden, x, Rules.SkeetLaunchHeight));
        Cue(AudioCue.SfxSpring);
    }

    private ClayTarget? FindInSight(int gun)
    {
        double sx = SightX(gun);
        ClayTarget? best = null;
        double bestD = double.MaxValue;
        foreach (var t in Targets)
        {
            if (t.Hit || t.Gone) continue;
            double dx = t.X - sx, dy = t.Y - Rules.SkeetSightHeight;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= Rules.SkeetHitRadius && d < bestD) { best = t; bestD = d; }
        }
        return best;
    }

    private void Fire(int gun, Athlete a)
    {
        if (_reload[gun] > 0) return;
        _reload[gun] = Rules.SkeetReload;
        _flash[gun] = 0.1;
        Facing = gun == 0 ? -1 : 1;
        Shots++;
        Cue(AudioCue.SfxShot);
        var target = FindInSight(gun);
        if (target is null)
        {
            Streak = 0;
            return;
        }
        target.Hit = true;
        target.Age = 1000; // removed from the list on the next tick
        Hits++;
        Streak++;
        Critters.Add(new FieldCritter("clay", target.X, target.Y, 0.7));
        Cue(AudioCue.SfxClayBreak);
        if (target.Golden) AwardPlainBonus(a.PlayerIndex, Rules.BonusPoints);
        else if (Streak % Rules.SkeetStreakBonusEvery == 0) AwardPlainBonus(a.PlayerIndex, Rules.SkeetStreakBonus);
    }
}
