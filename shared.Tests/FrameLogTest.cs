namespace shared.Tests;
using shared;

public class FrameLogTest {
    [Fact]
    public void TestStringify() {
        const int roomCapacity = 1;
        var startRdf = Battle.NewPreallocatedRoomDownsyncFrame(roomCapacity, 8, 128);
        startRdf.Id = Battle.DOWNSYNC_MSG_ACT_BATTLE_START;
        startRdf.ShouldForceResync = false;
        var (selfPlayerWx, selfPlayerWy) = Battle.CollisionSpacePositionToWorldPosition(0, 0, 1024, 512);

        var selfPlayerCharacterSpeciesId = 0;
        var selfPlayerCharacter = Battle.characters[selfPlayerCharacterSpeciesId];

        var selfPlayerInRdf = startRdf.PlayersArr[0];
        var (selfPlayerVposX, selfPlayerVposY) = Battle.PolygonColliderCtrToVirtualGridPos(0, 0);
        selfPlayerInRdf.Id = 10;
        selfPlayerInRdf.JoinIndex = 1;
        selfPlayerInRdf.VirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.VirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.RevivalVirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.RevivalVirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.Speed = selfPlayerCharacter.Speed;
        selfPlayerInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
        selfPlayerInRdf.FramesToRecover = 0;
        selfPlayerInRdf.DirX = 2;
        selfPlayerInRdf.DirY = 0;
        selfPlayerInRdf.VelX = 0;
        selfPlayerInRdf.VelY = 0;
        selfPlayerInRdf.InAir = true;
        selfPlayerInRdf.OnWall = false;
        selfPlayerInRdf.Hp = 100;
        selfPlayerInRdf.MaxHp = 100;
        selfPlayerInRdf.SpeciesId = 0;

        var startIfd = new InputFrameDownsync {
            InputFrameId = 132,
            ConfirmedList = ((ulong)1 << roomCapacity) - 1
        };
        startIfd.InputList.AddRange(new ulong[roomCapacity] { 12 });

        string s = Battle.stringifyFrameLog(new FrameLog {
            Rdf = startRdf,
            ActuallyUsedIdf = startIfd
        }, true);

        Assert.NotNull(s);
    }
}
