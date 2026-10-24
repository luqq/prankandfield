using PrankAndField.Core;

namespace PrankAndField.Net;

/// <summary>Identifies an online match: both peers must agree on all of it before the first tick.</summary>
/// <param name="Seed">Seed of the shared <see cref="Game"/>, chosen by the server.</param>
/// <param name="LocalSeat">0 plays as P1, 1 as P2.</param>
/// <param name="InputDelay">Ticks between sampling a local input and simulating it (hides the round trip).</param>
/// <param name="ChecksumInterval">Every this many ticks the peers exchange <see cref="Game.Checksum"/>.</param>
public sealed record MatchSettings(uint Seed, int LocalSeat, int InputDelay = 4, int ChecksumInterval = 60);

/// <summary>
/// Deterministic lockstep over a <see cref="Game"/>: each peer samples its own input, schedules it
/// <see cref="MatchSettings.InputDelay"/> ticks ahead, sends it and only simulates a tick once it holds
/// the inputs of both seats for it. Periodic checksums detect a desync.
/// </summary>
public sealed class LockstepSession
{
    private readonly IGameTransport _transport;
    private readonly Dictionary<int, int>[] _inputs = [new(), new()];
    private readonly Dictionary<int, ulong> _localChecksums = new();
    private readonly Dictionary<int, ulong> _remoteChecksums = new();
    private double _accumulator;

    public LockstepSession(MatchSettings settings, IGameTransport transport, Game? game = null)
    {
        if (settings.LocalSeat is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(settings), "Seat must be 0 or 1.");
        if (settings.InputDelay < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Input delay must be at least one tick.");
        Settings = settings;
        _transport = transport;
        // A fresh record board: local records would give one peer the world record bonus and not the other.
        Game = game ?? new Game(records: new WorldRecords(), seed: settings.Seed, online: true);
        if (!Game.IsOnline) throw new ArgumentException("The game must be created in online mode.", nameof(game));
        Game.StartOnlineCredit();

        // Nobody can have pressed anything during the first InputDelay ticks.
        for (int t = 0; t < settings.InputDelay; t++)
        {
            _inputs[0][t] = 0;
            _inputs[1][t] = 0;
        }
        NextLocalTick = settings.InputDelay;
    }

    public MatchSettings Settings { get; }
    public Game Game { get; }

    public int LocalSeat => Settings.LocalSeat;
    public int RemoteSeat => 1 - Settings.LocalSeat;

    /// <summary>Number of ticks simulated so far (the next tick to simulate).</summary>
    public int CurrentTick { get; private set; }

    /// <summary>Tick the next sampled local input will be applied to.</summary>
    public int NextLocalTick { get; private set; }

    /// <summary>True while the last attempted tick was missing the remote input.</summary>
    public bool IsWaitingForRemote { get; private set; }

    /// <summary>Consecutive attempts that could not simulate because the remote input was missing.</summary>
    public int StalledSteps { get; private set; }

    /// <summary>First checksum tick at which the peers disagreed, or null while they agree.</summary>
    public int? DesyncTick { get; private set; }

    public bool IsDesynced => DesyncTick.HasValue;

    /// <summary>Raised once, with the tick, when the peers' checksums first differ.</summary>
    public event Action<int>? DesyncDetected;

    /// <summary>
    /// Wall-clock driver for the host loop: runs <see cref="TryStep"/> once per elapsed 1/60 s with the
    /// current local mask. Returns the number of ticks simulated.
    /// </summary>
    public int Advance(double seconds, int localMask)
    {
        _accumulator += Math.Min(seconds, 0.25);
        int simulated = 0;
        while (_accumulator >= Game.TickSeconds)
        {
            _accumulator -= Game.TickSeconds;
            if (TryStep(localMask)) simulated++;
        }
        return simulated;
    }

    /// <summary>
    /// One fixed step: receives pending messages, samples <paramref name="localMask"/> (unless the local
    /// side is already <see cref="MatchSettings.InputDelay"/> ticks ahead) and simulates the current tick
    /// if both inputs are known. Returns whether a tick was simulated.
    /// </summary>
    public bool TryStep(int localMask)
    {
        Receive();
        if (IsDesynced) return false;

        if (NextLocalTick <= CurrentTick + Settings.InputDelay)
        {
            _inputs[LocalSeat][NextLocalTick] = localMask;
            _transport.Send(new InputMessage(LocalSeat, NextLocalTick, [localMask]));
            NextLocalTick++;
        }

        if (!_inputs[0].TryGetValue(CurrentTick, out int p1) || !_inputs[1].TryGetValue(CurrentTick, out int p2))
        {
            IsWaitingForRemote = true;
            StalledSteps++;
            return false;
        }
        IsWaitingForRemote = false;
        StalledSteps = 0;

        _inputs[0].Remove(CurrentTick);
        _inputs[1].Remove(CurrentTick);
        Game.Tick(InputFrame.FromMask(p1), InputFrame.FromMask(p2));
        CurrentTick++;

        if (CurrentTick % Settings.ChecksumInterval == 0)
        {
            ulong checksum = Game.Checksum();
            _localChecksums[CurrentTick] = checksum;
            _transport.Send(new ChecksumMessage(LocalSeat, CurrentTick, checksum));
            Compare(CurrentTick);
        }
        return true;
    }

    private void Receive()
    {
        while (_transport.TryReceive(out var message))
        {
            if (message.Seat != RemoteSeat) continue;
            switch (message)
            {
                case InputMessage input:
                    for (int i = 0; i < input.Masks.Length; i++)
                    {
                        int tick = input.FirstTick + i;
                        if (tick >= CurrentTick) _inputs[RemoteSeat][tick] = input.Masks[i];
                    }
                    break;
                case ChecksumMessage check:
                    _remoteChecksums[check.Tick] = check.Checksum;
                    Compare(check.Tick);
                    break;
            }
        }
    }

    private void Compare(int tick)
    {
        if (!_localChecksums.TryGetValue(tick, out ulong local) || !_remoteChecksums.TryGetValue(tick, out ulong remote)) return;
        _localChecksums.Remove(tick);
        _remoteChecksums.Remove(tick);
        if (local == remote || IsDesynced) return;
        DesyncTick = tick;
        DesyncDetected?.Invoke(tick);
    }
}
