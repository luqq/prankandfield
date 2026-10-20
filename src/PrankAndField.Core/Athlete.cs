namespace PrankAndField.Core;

/// <summary>Visual pose of an athlete; the engine maps poses to vector limb layouts.</summary>
public enum Pose
{
    Idle,
    Crouch,      // on the blocks
    Set,         // hips up
    Run,
    Walk,
    Jump,        // take-off stretch
    Fly,         // long jump sail
    Land,
    Hurdle,
    Stumble,
    Fall,
    Throw,       // javelin wind-up
    Release,
    Spin,        // hammer
    Flop,        // high jump bar clearance
    Celebrate,
    Dizzy,
}

/// <summary>Runtime state of one athlete on the field (world units: metres, seconds).</summary>
public sealed class Athlete
{
    public Athlete(int playerIndex, bool isCpu, string name)
    {
        PlayerIndex = playerIndex;
        IsCpu = isCpu;
        Name = name;
    }

    /// <summary>0 for player 1, 1 for player 2 / computer.</summary>
    public int PlayerIndex { get; }
    public bool IsCpu { get; }
    public string Name { get; set; }

    public Runner Runner { get; } = new();

    /// <summary>Horizontal position along the lane / runway, in metres.</summary>
    public double X
    {
        get => Runner.PositionM;
        set => Runner.PositionM = value;
    }

    /// <summary>Height above the ground in metres.</summary>
    public double Y { get; set; }

    public Pose Pose { get; set; } = Pose.Idle;

    /// <summary>Running cycle phase in [0,1), advanced with distance covered.</summary>
    public double AnimPhase { get; set; }

    /// <summary>Rotation for flops and spins (degrees).</summary>
    public double Rotation { get; set; }

    /// <summary>True while an animation lock prevents input (stumble, dizziness...).</summary>
    public double LockTimer { get; set; }

    public bool Finished { get; set; }
    public double FinishTime { get; set; }
    public int FalseStarts { get; set; }
    public bool Disqualified { get; set; }

    public void ResetForEvent(double startX)
    {
        Runner.Reset(startX);
        Y = 0;
        Pose = Pose.Idle;
        AnimPhase = 0;
        Rotation = 0;
        LockTimer = 0;
        Finished = false;
        FinishTime = 0;
        Disqualified = false;
    }

    /// <summary>Advances the run-cycle phase in proportion to distance covered (one stride ~ 2.2 m).</summary>
    public void AdvanceAnim(double distance)
    {
        AnimPhase = (AnimPhase + distance / 2.2) % 1.0;
        if (AnimPhase < 0) AnimPhase += 1.0;
    }
}
