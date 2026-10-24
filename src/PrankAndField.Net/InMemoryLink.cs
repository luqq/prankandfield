namespace PrankAndField.Net;

/// <summary>
/// Two connected in-process transports with a configurable one-way latency, counted in
/// <see cref="Pump"/> calls. Used to play both sides of a match locally and in tests.
/// </summary>
public sealed class InMemoryLink
{
    private readonly Endpoint _a;
    private readonly Endpoint _b;

    public InMemoryLink(int latencyFrames = 0)
    {
        LatencyFrames = latencyFrames;
        _a = new Endpoint(this);
        _b = new Endpoint(this);
        _a.Peer = _b;
        _b.Peer = _a;
    }

    /// <summary>One-way delay in frames before a message reaches the other end.</summary>
    public int LatencyFrames { get; set; }

    /// <summary>When true, sent messages are held (not lost) until it is cleared again.</summary>
    public bool Paused { get; set; }

    public IGameTransport A => _a;
    public IGameTransport B => _b;

    private long Frame { get; set; }

    /// <summary>Advances the link by one frame, delivering every message whose latency has elapsed.</summary>
    public void Pump()
    {
        Frame++;
        if (Paused) return;
        _a.Deliver(Frame);
        _b.Deliver(Frame);
    }

    private sealed class Endpoint(InMemoryLink link) : IGameTransport
    {
        private readonly Queue<(long DueFrame, NetMessage Message)> _inFlight = new();
        private readonly Queue<NetMessage> _inbox = new();

        public Endpoint Peer { get; set; } = null!;

        public void Send(NetMessage message) => Peer._inFlight.Enqueue((link.Frame + link.LatencyFrames, message));

        public bool TryReceive(out NetMessage message) => _inbox.TryDequeue(out message!);

        public void Deliver(long frame)
        {
            while (_inFlight.TryPeek(out var next) && next.DueFrame <= frame)
                _inbox.Enqueue(_inFlight.Dequeue().Message);
        }
    }
}
