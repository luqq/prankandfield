using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>
/// Visual-only particles: dust behind sprinting feet, sand spray on landing, dirt where an implement hits
/// the ground, fireworks for a qualified mark. The simulation never sees them, so they cannot affect the
/// lockstep checksum; they only live in the renderer and advance with the game clock.
/// </summary>
public sealed class Fx
{
    /// <summary>Lane key of particles that live in screen space (menus, banners) rather than in a scrolling lane.</summary>
    public const int ScreenSpace = -1;

    private struct Particle
    {
        public int Lane;      // player index of the lane strip (or ScreenSpace)
        public double X;      // metres along the lane, or screen x when Lane == ScreenSpace
        public double Y;      // pixels above the ground (positive up), or screen y when Lane == ScreenSpace
        public double Vx;     // m/s, or px/s in screen space
        public double Vy;     // px/s, positive up (screen space: positive down)
        public double Life;
        public double MaxLife;
        public double Size;
        public double Gravity;
        public Rgba Color;
    }

    private const int MaxParticles = 320;
    private readonly List<Particle> _ps = new(MaxParticles);
    private uint _seed = 0x9E3779B9;
    private double _lastTime = double.NaN;

    public int Count => _ps.Count;

    public void Clear() => _ps.Clear();

    private double Rand()
    {
        _seed = _seed * 1664525u + 1013904223u;
        return (_seed >> 8) / 16777216.0;
    }

    private double Range(double a, double b) => a + (b - a) * Rand();

    /// <summary>Advances every particle to the game clock <paramref name="now"/> (seconds).</summary>
    public void Update(double now)
    {
        double dt = double.IsNaN(_lastTime) ? 0 : Math.Clamp(now - _lastTime, 0, 0.1);
        _lastTime = now;
        if (dt <= 0) return;
        for (int i = _ps.Count - 1; i >= 0; i--)
        {
            var p = _ps[i];
            p.Life -= dt;
            if (p.Life <= 0) { _ps.RemoveAt(i); continue; }
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            if (p.Lane == ScreenSpace) p.Vy += p.Gravity * dt; else p.Vy -= p.Gravity * dt;
            if (p.Lane != ScreenSpace && p.Y < 0) { p.Y = 0; p.Vy = 0; p.Vx *= 0.6; }
            p.Vx *= 1 - 1.5 * dt;
            _ps[i] = p;
        }
    }

    private void Add(Particle p)
    {
        if (_ps.Count >= MaxParticles) _ps.RemoveAt(0);
        _ps.Add(p);
    }

    /// <summary>Small puff of dust kicked up by a foot at <paramref name="worldX"/> metres.</summary>
    public void Dust(int lane, double worldX, int count = 3, Rgba? color = null)
    {
        var c = color ?? Palette.Dust;
        for (int i = 0; i < count; i++)
        {
            double life = Range(0.25, 0.5);
            Add(new Particle
            {
                Lane = lane, X = worldX + Range(-0.15, 0.05), Y = Range(0, 1.5),
                Vx = Range(-1.2, -0.2), Vy = Range(6, 16), Life = life, MaxLife = life,
                Size = Range(0.9, 1.7), Gravity = 12, Color = c.WithAlpha(170),
            });
        }
    }

    /// <summary>Spray of grains thrown up where something lands (sand, dirt, mat padding).</summary>
    public void Splash(int lane, double worldX, int count, Rgba color, double strength = 1)
    {
        for (int i = 0; i < count; i++)
        {
            double life = Range(0.35, 0.8);
            Add(new Particle
            {
                Lane = lane, X = worldX + Range(-0.2, 0.2), Y = Range(0, 1),
                Vx = Range(-2.5, 2.5) * strength, Vy = Range(18, 42) * strength, Life = life, MaxLife = life,
                Size = Range(0.8, 1.6), Gravity = 70, Color = color,
            });
        }
    }

    /// <summary>A firework burst at a screen position, in a colour of the crowd palette.</summary>
    public void Burst(double sx, double sy, Rgba color, int count = 22)
    {
        for (int i = 0; i < count; i++)
        {
            double a = i / (double)count * Math.PI * 2 + Range(-0.1, 0.1);
            double v = Range(22, 46);
            double life = Range(0.6, 1.1);
            Add(new Particle
            {
                Lane = ScreenSpace, X = sx, Y = sy,
                Vx = Math.Cos(a) * v, Vy = Math.Sin(a) * v, Life = life, MaxLife = life,
                Size = Range(0.9, 1.5), Gravity = 18, Color = i % 4 == 0 ? Palette.Spark : color,
            });
        }
    }

    /// <summary>Draws the particles of one lane, converting metres to screen with the lane's camera.</summary>
    public void Draw(DrawList dl, int lane, double cameraX, double groundY)
    {
        foreach (var p in _ps)
        {
            if (p.Lane != lane) continue;
            double sx = Layout.ToScreenX(p.X, cameraX);
            if (sx < -4 || sx > Screen.Width + 4) continue;
            double t = p.Life / p.MaxLife;
            byte alpha = (byte)(p.Color.A * Math.Clamp(t * 1.4, 0, 1));
            double s = p.Size * (0.6 + 0.6 * t);
            dl.FillRect(sx - s / 2, groundY - p.Y - s / 2, s, s, p.Color.WithAlpha(alpha));
        }
    }

    /// <summary>Draws the screen-space particles (fireworks).</summary>
    public void DrawScreen(DrawList dl)
    {
        foreach (var p in _ps)
        {
            if (p.Lane != ScreenSpace) continue;
            double t = p.Life / p.MaxLife;
            byte alpha = (byte)(p.Color.A * Math.Clamp(t * 1.5, 0, 1));
            double s = p.Size * (0.5 + 0.8 * t);
            dl.FillRect(p.X - s / 2, p.Y - s / 2, s, s, p.Color.WithAlpha(alpha));
            // a short tail against the direction of travel
            if (t > 0.5) dl.Line(p.X, p.Y, p.X - p.Vx * 0.04, p.Y - p.Vy * 0.04, s * 0.6, p.Color.WithAlpha((byte)(alpha / 2)));
        }
    }
}

/// <summary>
/// Watches the game between frames and fires the effects that go with what just happened: a landing, a
/// hurdle knocked over, a qualified mark... It keeps only what it saw last frame, nothing the game depends on.
/// </summary>
public sealed class FxDirector
{
    private EventSimulation? _sim;
    private GameState _state;
    private readonly double[] _phase = new double[2];
    private readonly double[] _y = new double[2];
    private readonly Pose[] _pose = new Pose[2];
    private readonly HashSet<Hurdle> _fallen = new();
    private int _subState = -1;
    private BannerKind _banner;
    private double _nextFirework;

    public void Observe(Game game, Fx fx)
    {
        if (game.State != _state)
        {
            var previous = _state;
            _state = game.State;
            fx.Clear();
            if (_state == GameState.EventOutcome && previous == GameState.Event && game.Outcomes.Any(o => o.Qualified)) _nextFirework = game.TotalTime;
        }
        if (_state == GameState.Ceremony || (_state == GameState.EventOutcome && game.Outcomes.Any(o => o.Qualified)))
        {
            // fireworks over the stands every half second or so
            if (game.TotalTime >= _nextFirework)
            {
                _nextFirework = game.TotalTime + 0.55;
                int k = (int)(game.TotalTime * 10) % Palette.Crowd.Length;
                double sx = 30 + (game.TotalTime * 73) % 196;
                fx.Burst(sx, Layout.StandsTop + 6 + (game.TotalTime * 31) % 26, Palette.Crowd[k]);
            }
        }

        var sim = game.Simulation;
        if (!ReferenceEquals(sim, _sim))
        {
            _sim = sim;
            _fallen.Clear();
            _subState = -1;
            _banner = BannerKind.None;
            for (int i = 0; i < 2; i++) { _phase[i] = 0; _y[i] = 0; _pose[i] = Pose.Idle; }
            if (sim is null) return;
            foreach (var a in sim.Athletes) Remember(a);
            return;
        }
        if (sim is null) return;

        // a qualified mark or a record lights the sky
        if (sim.Banner != _banner)
        {
            _banner = sim.Banner;
            if (_banner is BannerKind.Qualified or BannerKind.WorldRecord or BannerKind.Bonus)
            {
                for (int i = 0; i < 3; i++) fx.Burst(70 + i * 58, Layout.FieldTop + 14 + (i % 2) * 10, Palette.Crowd[(i * 5 + (int)(game.TotalTime * 7)) % Palette.Crowd.Length]);
            }
        }

        switch (sim)
        {
            case RaceSimulation race:
                foreach (var a in race.Athletes) Footfalls(a, a.PlayerIndex, fx);
                if (race is HurdlesSimulation hurdles)
                {
                    foreach (var a in race.Athletes)
                    {
                        foreach (var h in hurdles.HurdlesFor(a.PlayerIndex))
                        {
                            if (h.Fallen && _fallen.Add(h)) fx.Splash(a.PlayerIndex, h.X, 6, Palette.LightGrey, 0.7);
                        }
                    }
                }
                break;
            case LongJumpSimulation lj:
                Footfalls(lj.CurrentAthlete, 0, fx);
                if (Changed((int)lj.State))
                {
                    if (lj.State == JumpState.Rising) fx.Dust(0, lj.CurrentAthlete.X, 5);
                    if (lj.State == JumpState.Landed) fx.Splash(0, lj.CurrentAthlete.X, 18, Palette.SandSpray, 1.1);
                }
                break;
            case JavelinSimulation jv:
                Footfalls(jv.CurrentAthlete, 0, fx);
                if (Changed((int)jv.State))
                {
                    if (jv.State == ThrowState.WindUp) fx.Dust(0, jv.CurrentAthlete.X, 4);
                    if (jv.State == ThrowState.Landed) fx.Splash(0, jv.JavelinX, 10, Palette.Dust.Darker(0.8), 0.8);
                }
                break;
            case HammerSimulation hm:
                if (Changed((int)hm.State))
                {
                    if (hm.State == HammerState.Landed) fx.Splash(0, hm.HammerX, 14, Palette.Dust.Darker(0.75), 1.0);
                    if (hm.State == HammerState.Flight) fx.Dust(0, 0.3, 5);
                }
                break;
            case HighJumpSimulation hj:
                Footfalls(hj.CurrentAthlete, 0, fx);
                if (Changed((int)hj.State))
                {
                    if (hj.State == HighJumpState.Rising) fx.Dust(0, hj.CurrentAthlete.X, 5);
                    if (hj.State is HighJumpState.Landed or HighJumpState.Missed) fx.Splash(0, Math.Max(0.4, hj.CurrentAthlete.X), 10, Palette.MatLight.Lighter(0.4), 0.6);
                }
                break;
        }
        foreach (var a in sim.Athletes) Remember(a);
    }

    private bool Changed(int subState)
    {
        if (subState == _subState) return false;
        bool first = _subState < 0;
        _subState = subState;
        return !first;
    }

    private void Remember(Athlete a)
    {
        int i = Math.Clamp(a.PlayerIndex, 0, 1);
        _phase[i] = a.AnimPhase;
        _y[i] = a.Y;
        _pose[i] = a.Pose;
    }

    /// <summary>A sprinting foot strikes the ground twice per stride (phase 0 and 0.5): kick up some dust.</summary>
    private void Footfalls(Athlete a, int lane, Fx fx)
    {
        int i = Math.Clamp(a.PlayerIndex, 0, 1);
        if (a.Pose != Pose.Run || a.Y > 0 || a.Runner.SpeedCm < 500) return;
        double prev = _phase[i], cur = a.AnimPhase;
        int before = (int)Math.Floor(prev * 2), after = (int)Math.Floor(cur * 2);
        if (before != after || cur < prev) fx.Dust(lane, a.X - 0.2, 2);
    }
}
