namespace shared.Tests;
using shared;
using Xunit.Abstractions;
using static shared.Battle;

public class SkewedBarrierTest {
    ILoggerBridge _logger;
    public SkewedBarrierTest(ITestOutputHelper output) {
        this._logger = new LoggerBridgeImpl(output);
    }

    [Fact]
    public void TestHardPushbackCalc() {
        int mapWidth = 128, mapHeight = 128;
        int tileWidth = 16, tileHeight = 16;
        int spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        int spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        var collisionSys = new CollisionSpace(spaceOffsetX * 2, spaceOffsetY * 2, 64, 64);
        var collisionHolder = new Collision();
        var overlapResult = new SatResult();

        var effPushback = new Vector(0, 0);
        var hardPushbackNorms = new Vector[5];
        for (int i = 0; i < hardPushbackNorms.Length; i++) {
            hardPushbackNorms[i] = new Vector(0, 0);
        }
        var (rectCx, rectCy) = (0, 0);
        var (rectVx, rectVy) = Battle.PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);

        var currCharacterDownsync = new CharacterDownsync();
        currCharacterDownsync.Id = 10;
        currCharacterDownsync.JoinIndex = 1;
        currCharacterDownsync.VirtualGridX = rectVx;
        currCharacterDownsync.VirtualGridY = rectVy;
        currCharacterDownsync.RevivalVirtualGridX = rectVx;
        currCharacterDownsync.RevivalVirtualGridY = rectVx;
        currCharacterDownsync.Speed = 10;
        currCharacterDownsync.CharacterState = CharacterState.InAirIdle1NoJump;
        currCharacterDownsync.FramesToRecover = 0;
        currCharacterDownsync.DirX = 2;
        currCharacterDownsync.DirY = 0;
        currCharacterDownsync.VelX = 0;
        currCharacterDownsync.VelY = 0;
        currCharacterDownsync.InAir = true;
        currCharacterDownsync.OnWall = false;
        currCharacterDownsync.Hp = 100;
        currCharacterDownsync.MaxHp = 100;
        currCharacterDownsync.SpeciesId = 0;

        var thatCharacterInNextFrame = new CharacterDownsync();

        var (rectCw, rectCh) = VirtualGridToPolygonColliderCtr(characters[currCharacterDownsync.SpeciesId].ShrinkedSizeX, characters[currCharacterDownsync.SpeciesId].ShrinkedSizeY);

        var aCollider = NewRectCollider(rectCx, rectCy, rectCw, rectCh, 0, 0, 0, 0, 0, 0, currCharacterDownsync);

        _logger.LogInfo(String.Format("aCollider={0}", aCollider.Shape.ToString(false))); 
        collisionSys.AddSingle(aCollider);

        // Add a triangular barrier collider
        float triangleEdgeLength = 16f;
        var points = new float[6] { 0,0, triangleEdgeLength,0, triangleEdgeLength,triangleEdgeLength };
        var (anchorCx, anchorCy) = (rectCx + 0.5f*rectCw - 0.5f*triangleEdgeLength, rectCy-0.61f*rectCh);
        var srcPolygon = new ConvexPolygon(anchorCx, anchorCy, points);
        var bCollider1 = NewConvexPolygonCollider(srcPolygon, 0, 0, null);
        _logger.LogInfo(String.Format("bCollider1={0}", bCollider1.Shape.ToString(false))); 
        collisionSys.AddSingle(bCollider1);

        int hardPushbackCnt = calcHardPushbacksNorms(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, SNAP_INTO_PLATFORM_OVERLAP, effPushback, hardPushbackNorms, collisionHolder, ref overlapResult, _logger);

        _logger.LogInfo(String.Format("#1 hardPushbackCnt={0}", hardPushbackCnt));

        for (int k = 0; k < hardPushbackCnt; k++) {
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("#1 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }

        Assert.True(1 == hardPushbackCnt);
    }
}
