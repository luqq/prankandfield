namespace PrankAndField.Net;

/// <summary>
/// Reliable, ordered channel to the other peer (directly or through the relay server).
/// The session polls it from the game loop, so implementations backed by a network client
/// queue incoming messages and hand them out here.
/// </summary>
public interface IGameTransport
{
    void Send(NetMessage message);

    bool TryReceive(out NetMessage message);
}
