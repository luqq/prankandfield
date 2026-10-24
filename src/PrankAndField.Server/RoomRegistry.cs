using System.Security.Cryptography;

namespace PrankAndField.Server;

/// <summary>A private two-seat room. Seat 0 (P1) is whoever created it.</summary>
public sealed class Room(string code, uint seed)
{
    public string Code { get; } = code;
    public uint Seed { get; } = seed;
    public string?[] Seats { get; } = new string?[2];
    public bool IsFull => Seats[0] is not null && Seats[1] is not null;

    public string? Opponent(int seat) => Seats[1 - seat];
}

public enum JoinError
{
    None,
    NotFound,
    Full,
    AlreadyInRoom,
}

/// <summary>In-memory rooms of a single relay instance, keyed by code and by connection.</summary>
public sealed class RoomRegistry
{
    // No 0/O, 1/I/L: codes are read aloud and typed on phones.
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 5;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Room Room, int Seat)> _byConnection = new();

    public int RoomCount
    {
        get { lock (_sync) return _rooms.Count; }
    }

    public Room Create(string connectionId)
    {
        lock (_sync)
        {
            if (_byConnection.ContainsKey(connectionId)) throw new InvalidOperationException("The connection is already in a room.");
            string code;
            do code = NewCode(); while (_rooms.ContainsKey(code));
            var room = new Room(code, (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue));
            room.Seats[0] = connectionId;
            _rooms[code] = room;
            _byConnection[connectionId] = (room, 0);
            return room;
        }
    }

    public JoinError TryJoin(string code, string connectionId, out Room? room)
    {
        lock (_sync)
        {
            room = null;
            if (_byConnection.ContainsKey(connectionId)) return JoinError.AlreadyInRoom;
            if (!_rooms.TryGetValue(code.Trim(), out var found)) return JoinError.NotFound;
            if (found.IsFull) return JoinError.Full;
            found.Seats[1] = connectionId;
            _byConnection[connectionId] = (found, 1);
            room = found;
            return JoinError.None;
        }
    }

    public bool TryGetSeat(string connectionId, out Room room, out int seat)
    {
        lock (_sync)
        {
            if (_byConnection.TryGetValue(connectionId, out var entry))
            {
                (room, seat) = entry;
                return true;
            }
            room = null!;
            seat = -1;
            return false;
        }
    }

    /// <summary>Removes the connection; the room closes with it. Returns the opponent left behind, if any.</summary>
    public string? Leave(string connectionId)
    {
        lock (_sync)
        {
            if (!_byConnection.Remove(connectionId, out var entry)) return null;
            var (room, seat) = entry;
            string? opponent = room.Opponent(seat);
            if (opponent is not null) _byConnection.Remove(opponent);
            _rooms.Remove(room.Code);
            return opponent;
        }
    }

    private static string NewCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (int i = 0; i < code.Length; i++) code[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(code);
    }
}
