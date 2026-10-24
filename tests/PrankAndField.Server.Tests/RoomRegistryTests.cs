namespace PrankAndField.Server.Tests;

public class RoomRegistryTests
{
    [Fact]
    public void CodesUseTheUnambiguousAlphabet()
    {
        var rooms = new RoomRegistry();
        for (int i = 0; i < 200; i++)
        {
            var room = rooms.Create($"c{i}");
            Assert.Equal(RoomRegistry.CodeLength, room.Code.Length);
            Assert.All(room.Code, c => Assert.Contains(c, RoomRegistry.CodeAlphabet));
        }
        Assert.Equal(200, rooms.RoomCount);
    }

    [Fact]
    public void TheCreatorIsP1AndTheJoinerP2()
    {
        var rooms = new RoomRegistry();
        var room = rooms.Create("a");
        Assert.Equal(JoinError.None, rooms.TryJoin(room.Code.ToLowerInvariant(), "b", out var joined));
        Assert.Same(room, joined);
        Assert.True(rooms.TryGetSeat("a", out _, out int seatA));
        Assert.True(rooms.TryGetSeat("b", out _, out int seatB));
        Assert.Equal((0, 1), (seatA, seatB));
        Assert.Equal("b", room.Opponent(0));
    }

    [Fact]
    public void JoiningFailsForUnknownFullOrDoubleBookedRooms()
    {
        var rooms = new RoomRegistry();
        var room = rooms.Create("a");
        Assert.Equal(JoinError.NotFound, rooms.TryJoin("ZZZZZ", "b", out _));
        Assert.Equal(JoinError.AlreadyInRoom, rooms.TryJoin(room.Code, "a", out _));
        Assert.Equal(JoinError.None, rooms.TryJoin(room.Code, "b", out _));
        Assert.Equal(JoinError.Full, rooms.TryJoin(room.Code, "c", out _));
    }

    [Fact]
    public void LeavingClosesTheRoomForBoth()
    {
        var rooms = new RoomRegistry();
        var room = rooms.Create("a");
        rooms.TryJoin(room.Code, "b", out _);

        Assert.Equal("b", rooms.Leave("a"));
        Assert.Equal(0, rooms.RoomCount);
        Assert.False(rooms.TryGetSeat("b", out _, out _));
        Assert.Null(rooms.Leave("b"));
    }
}
