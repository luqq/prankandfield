namespace PrankAndField.Core.Events;

/// <summary>
/// 100 metre freestyle. Alternate the run buttons to stroke; every few seconds a BREATHE prompt comes up
/// and the action button must be pressed while it shows. Missing it leaves the swimmer gasping, breathing
/// with no prompt wastes a stroke. The computer swims the other lane.
/// </summary>
public sealed class SwimmingSimulation : RaceSimulation
{
    private readonly double[] _breathTimer = new double[2];   // seconds until the next prompt
    private readonly double[] _breathWindow = new double[2];  // > 0 while the prompt is up
    private readonly double[] _breathAnim = new double[2];    // > 0 right after a good breath
    private readonly double[] _chokeTimer = new double[2];    // > 0 while gasping
    private readonly int[] _breaths = new int[2];
    private readonly int[] _missed = new int[2];

    public SwimmingSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.Swimming100m, round, athletes, rng, Rules.SwimDistance)
    {
        foreach (var a in Athletes)
        {
            a.Runner.MaxSpeed = Rules.SwimMaxSpeed;
            a.Runner.ImpulseScale = Rules.SwimImpulseScale;
        }
        ResetCourse();
    }

    protected override double CpuAcceleration => Rules.SwimCpuAcceleration;

    /// <summary>True while the BREATHE prompt is up for the player.</summary>
    public bool BreathPrompt(int playerIndex) => _breathWindow[playerIndex] > 0;

    /// <summary>Fraction of the prompt window still left (1 when it appears, 0 when it closes).</summary>
    public double BreathWindowLeft(int playerIndex) => Math.Clamp(_breathWindow[playerIndex] / Rules.SwimBreathWindow, 0, 1);

    /// <summary>True while the swimmer gasps after a missed breath.</summary>
    public bool IsChoking(int playerIndex) => _chokeTimer[playerIndex] > 0;

    public int Breaths(int playerIndex) => _breaths[playerIndex];
    public int MissedBreaths(int playerIndex) => _missed[playerIndex];

    protected override Pose MovingPose(Athlete a) => _breathAnim[a.PlayerIndex] > 0 || _chokeTimer[a.PlayerIndex] > 0 ? Pose.Breathe : Pose.Swim;

    protected override Pose CoastingPose(Athlete a) => a.Runner.SpeedCm <= 0 && !a.Disqualified ? Pose.Celebrate : Pose.Swim;

    protected override void OnGun() => Cue(AudioCue.SfxSplash);

    protected override void ResetCourse()
    {
        for (int p = 0; p < 2; p++)
        {
            _breathTimer[p] = Rules.SwimFirstBreath;
            _breathWindow[p] = 0;
            _breathAnim[p] = 0;
            _chokeTimer[p] = 0;
            _breaths[p] = 0;
            _missed[p] = 0;
        }
    }

    protected override void TickAthleteMotion(Athlete a, ButtonEdges? e, double dt)
    {
        int p = a.PlayerIndex;
        if (_breathAnim[p] > 0) _breathAnim[p] -= dt;
        if (_chokeTimer[p] > 0) _chokeTimer[p] -= dt;
        if (a.IsCpu || a.Finished || e is null) return;

        if (_breathWindow[p] > 0)
        {
            _breathWindow[p] -= dt;
            if (e.ActionPressed)
            {
                _breathWindow[p] = 0;
                _breathTimer[p] = Rng.Range(Rules.SwimBreathMinInterval, Rules.SwimBreathMaxInterval);
                a.Runner.SpeedCm = Math.Min(a.Runner.MaxSpeed, a.Runner.SpeedCm + Rules.SwimBreathBoost);
                _breaths[p]++;
                _breathAnim[p] = 0.45;
                Cue(AudioCue.SfxBreath);
            }
            else if (_breathWindow[p] <= 0)
            {
                // Missed: the swimmer swallows water and loses the rhythm for a moment.
                _breathTimer[p] = Rng.Range(Rules.SwimBreathMinInterval, Rules.SwimBreathMaxInterval);
                a.Runner.SpeedCm *= Rules.SwimChokeFactor;
                a.LockTimer = Rules.SwimChokeLock;
                _chokeTimer[p] = Rules.SwimChokeLock;
                _missed[p]++;
                Cue(AudioCue.SfxGasp);
            }
        }
        else
        {
            _breathTimer[p] -= dt;
            if (_breathTimer[p] <= 0)
            {
                _breathWindow[p] = Rules.SwimBreathWindow;
                Cue(AudioCue.SfxTick);
            }
            else if (e.ActionPressed && a.LockTimer <= 0)
            {
                // Breathing with no prompt costs a bit of speed.
                a.Runner.SpeedCm *= Rules.SwimWastedBreathFactor;
            }
        }
    }
}
