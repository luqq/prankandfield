namespace PrankAndField.Core.Events;

public enum RacePhase
{
    Intro,
    OnYourMarks,
    GetSet,
    Running,
    Results,
    Done,
}

/// <summary>Shared logic for the two timed heats: start sequence, false starts, timing and the computer runner.</summary>
public abstract class RaceSimulation : EventSimulation
{
    private double _phaseTimer;
    private double _setDelay;
    private readonly double[] _cpuTarget = new double[2];
    private int _restartCount;

    protected RaceSimulation(EventKind kind, int round, IReadOnlyList<Athlete> athletes, Rng rng, double distance)
        : base(kind, round, athletes, rng)
    {
        Distance = distance;
        foreach (var a in Athletes)
        {
            a.ResetForEvent(0);
            a.Pose = Pose.Crouch;
            if (a.IsCpu)
            {
                // The computer aims a little under the qualifying mark, never faster than a 9.5 s / 12.5 s pace.
                double q = QualifyingMark;
                double target = Math.Max(kind == EventKind.Dash100m ? 9.6 : 12.6, q * Rng.Range(0.74, 0.88));
                _cpuTarget[a.PlayerIndex] = target;
            }
        }
        Phase = RacePhase.Intro;
        _phaseTimer = 2.0;
    }

    public double Distance { get; }
    public RacePhase Phase { get; private set; }

    /// <summary>Race clock in seconds since the gun (0 before the start).</summary>
    public double RaceTime { get; private set; }

    public double TimerFor(int playerIndex)
    {
        var a = AthleteFor(playerIndex);
        if (a is null) return 0;
        return a.Finished ? a.FinishTime : RaceTime;
    }

    public override double? MarkFor(int playerIndex)
    {
        var a = AthleteFor(playerIndex);
        if (a is null || !a.Finished || a.Disqualified) return null;
        return Rules.Snap(a.FinishTime);
    }

    /// <summary>Cpu target speed in cm/s, derived from the target time.</summary>
    protected double CpuTargetSpeed(Athlete a) => Distance / _cpuTarget[a.PlayerIndex] * 100.0 * 1.04;

    protected override void TickCore(double dt, ButtonEdges p1, ButtonEdges p2)
    {
        switch (Phase)
        {
            case RacePhase.Intro:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0) EnterMarks();
                break;

            case RacePhase.OnYourMarks:
            case RacePhase.GetSet:
                _phaseTimer -= dt;
                if (CheckFalseStart(p1, p2)) break;
                if (_phaseTimer <= 0)
                {
                    if (Phase == RacePhase.OnYourMarks)
                    {
                        Phase = RacePhase.GetSet;
                        foreach (var a in Athletes) a.Pose = Pose.Set;
                        Cue(AudioCue.VoiceGetSet);
                        _setDelay = Rng.Range(0.8, 1.9);
                        _phaseTimer = _setDelay;
                    }
                    else
                    {
                        Phase = RacePhase.Running;
                        RaceTime = 0;
                        foreach (var a in Athletes) a.Pose = Pose.Run;
                        Cue(AudioCue.SfxGun);
                        Cue(AudioCue.VoiceGo);
                    }
                }
                break;

            case RacePhase.Running:
                RaceTime += dt;
                foreach (var a in Athletes)
                {
                    if (a.Finished && a.Runner.SpeedCm <= 0) continue;
                    if (a.IsCpu) TickCpu(a, dt);
                    else TickHuman(a, InputFor(a, p1, p2), dt);
                }
                if (Athletes.All(a => a.Finished))
                {
                    Phase = RacePhase.Results;
                    _phaseTimer = 3.0;
                    OnRaceFinished();
                }
                break;

            case RacePhase.Results:
                foreach (var a in Athletes) Coast(a, dt);
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                {
                    Phase = RacePhase.Done;
                    IsFinished = true;
                }
                break;
        }
    }

    private void EnterMarks()
    {
        Phase = RacePhase.OnYourMarks;
        _phaseTimer = 1.4;
        foreach (var a in Athletes)
        {
            if (a.Disqualified) continue; // a thrown-out athlete stays out for the restarted heat
            a.ResetForEvent(0);
            a.Pose = Pose.Crouch;
        }
        ResetCourse();
        Cue(AudioCue.VoiceOnYourMarks);
    }

    private bool CheckFalseStart(ButtonEdges p1, ButtonEdges p2)
    {
        foreach (var a in Athletes)
        {
            if (a.IsCpu || a.Disqualified) continue;
            var e = InputFor(a, p1, p2);
            if (!e.AnyRunPressed) continue;
            a.FalseStarts++;
            Cue(AudioCue.SfxWhistle);
            Cue(AudioCue.VoiceFalseStart);
            ShowBanner(BannerKind.FalseStart, a.FalseStarts, a.PlayerIndex, 1.5);
            if (a.FalseStarts > Rules.FalseStartsAllowed)
            {
                // Third flying start: the heat is lost for this athlete.
                a.Disqualified = true;
                a.Finished = true;
                a.FinishTime = 99.99;
            }
            _restartCount++;
            if (Athletes.All(x => x.Finished))
            {
                Phase = RacePhase.Results;
                _phaseTimer = 2.0;
                OnRaceFinished();
                return true;
            }
            Phase = RacePhase.Intro;
            _phaseTimer = 1.6;
            return true;
        }
        return false;
    }

    private void TickHuman(Athlete a, ButtonEdges e, double dt)
    {
        if (a.Disqualified) return;
        double before = a.X;
        if (!a.Finished && a.LockTimer <= 0) a.Runner.ApplyEdges(e);
        if (a.LockTimer > 0) a.LockTimer -= dt;
        TickAthleteMotion(a, e, dt);
        a.Runner.Integrate(dt);
        AfterMotion(a, before, dt);
    }

    private void TickCpu(Athlete a, double dt)
    {
        double before = a.X;
        if (!a.Finished && a.LockTimer <= 0)
        {
            double target = CpuTargetSpeed(a) * (1 + 0.04 * Math.Sin(Elapsed * 2.1));
            double accel = 900 * dt;
            if (a.Runner.SpeedCm < target) a.Runner.SpeedCm = Math.Min(target, a.Runner.SpeedCm + accel + a.Runner.SpeedCm * Runner.DragPerSecond * dt);
        }
        if (a.LockTimer > 0) a.LockTimer -= dt;
        TickAthleteMotion(a, null, dt);
        a.Runner.Integrate(dt);
        AfterMotion(a, before, dt);
    }

    private void AfterMotion(Athlete a, double before, double dt)
    {
        a.AdvanceAnim(a.X - before);
        if (!a.Finished && a.X >= Distance)
        {
            double v = a.Runner.SpeedMetersPerSecond;
            double overshoot = a.X - Distance;
            a.Finished = true;
            a.FinishTime = Rules.Snap(Math.Max(0.01, RaceTime - (v > 0 ? overshoot / v : 0)));
            if (!a.IsCpu) AnnounceMark(a.FinishTime);
            OnAthleteFinished(a);
        }
        if (!a.Finished && RaceTime > QualifyingMark + 12)
        {
            // Nobody waits forever for a stalled runner.
            a.Finished = true;
            a.Disqualified = true;
            a.FinishTime = 99.99;
        }
        if (a.Y <= 0 && a.LockTimer <= 0)
            a.Pose = a.Runner.SpeedCm < 100 ? (a.Runner.SpeedCm <= 0 ? Pose.Idle : Pose.Walk) : Pose.Run;
    }

    private static void Coast(Athlete a, double dt)
    {
        double before = a.X;
        a.Runner.Integrate(dt);
        a.AdvanceAnim(a.X - before);
        if (a.Runner.SpeedCm <= 0 && !a.Disqualified) a.Pose = Pose.Celebrate;
        else if (a.Runner.SpeedCm < 100) a.Pose = Pose.Walk;
    }

    /// <summary>Per-event motion hook (hurdle jumping). <paramref name="e"/> is null for the computer.</summary>
    protected virtual void TickAthleteMotion(Athlete a, ButtonEdges? e, double dt) { }

    protected virtual void ResetCourse() { }

    protected virtual void OnAthleteFinished(Athlete a) { }

    protected virtual void OnRaceFinished()
    {
        var finishers = Athletes.Where(a => !a.Disqualified).ToList();
        if (finishers.Count == 2 && Math.Abs(finishers[0].FinishTime - finishers[1].FinishTime) < 0.005)
        {
            // Dead heat: the arcade paid 1000 points for identical times.
            foreach (var a in finishers.Where(x => !x.IsCpu)) BonusPoints[a.PlayerIndex] += Rules.TieBonus;
            ShowBanner(BannerKind.Bonus, Rules.TieBonus, 0, 2.5);
            Cue(AudioCue.SfxBonus);
        }
        Cue(AudioCue.SfxCrowd);
    }
}

/// <summary>The 100 metre dash.</summary>
public sealed class DashSimulation : RaceSimulation
{
    public DashSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.Dash100m, round, athletes, rng, Rules.DashDistance)
    {
    }
}
