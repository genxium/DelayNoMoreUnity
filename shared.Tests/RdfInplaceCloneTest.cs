namespace shared.Tests;
using shared;

public class RdfInplaceCloneTest {
    [Fact]
    public void TestRdfInplaceClone() {
        const int roomCapacity = 1;
        var startRdf = Battle.NewPreallocatedRoomDownsyncFrame(roomCapacity, 8, 128, 64, 64);
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

        var clonedRdf = Battle.NewPreallocatedRoomDownsyncFrame(roomCapacity, 8, 128, 64, 64);
        Battle.AssignToRdfDeep(startRdf, clonedRdf, roomCapacity);
        Assert.True(Battle.EqualRdfs(startRdf, clonedRdf, roomCapacity));

        var startIfd = new InputFrameDownsync {
            InputFrameId = Battle.ConvertToDelayedInputFrameId(startRdf.Id),
            ConfirmedList = (1ul << roomCapacity) - 1
        };
        startIfd.InputList.AddRange(new ulong[roomCapacity] { 12 });
    }
}
