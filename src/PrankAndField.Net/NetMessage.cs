namespace PrankAndField.Net;

/// <summary>Everything two peers of a lockstep match send each other.</summary>
public abstract record NetMessage(int Seat);

/// <summary>
/// Input masks of <see cref="NetMessage.Seat"/> for consecutive ticks starting at <paramref name="FirstTick"/>.
/// A session sends one tick per message; the array lets a transport batch them.
/// </summary>
public sealed record InputMessage(int Seat, int FirstTick, int[] Masks) : NetMessage(Seat);

/// <summary><see cref="PrankAndField.Core.Game.Checksum"/> of the sender after simulating <paramref name="Tick"/> ticks.</summary>
public sealed record ChecksumMessage(int Seat, int Tick, ulong Checksum) : NetMessage(Seat);
