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
        var primaryOverlapResult = new SatResult();
        int primaryOverlapIndex = -1;

        var effPushback = new Vector(0, 0);
        var hardPushbackNorms = new Vector[5];
        for (int i = 0; i < hardPushbackNorms.Length; i++) {
            hardPushbackNorms[i] = new Vector(0, 0);
        }
        var (rectCx, rectCy) = (0f, 0f);
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

        // Add a triangular barrier collider
        _logger.LogInfo("-------------------------------------------------------------------");
        _logger.LogInfo(String.Format("aCollider={0}", aCollider.Shape.ToString(false))); 
        collisionSys.AddSingle(aCollider);
        float triangleEdgeLength = 16f;
        var trianglePoints = new float[6] { 0,0, triangleEdgeLength,0, triangleEdgeLength,triangleEdgeLength };
        var (anchorCx1, anchorCy1) = (rectCx + 0.5f*rectCw - 0.5f*triangleEdgeLength, rectCy-0.61f*rectCh);
        var srcPolygon1 = new ConvexPolygon(anchorCx1, anchorCy1, trianglePoints);
        var bCollider1 = NewConvexPolygonCollider(srcPolygon1, 0, 0, null);
        _logger.LogInfo(String.Format("bCollider1={0}", bCollider1.Shape.ToString(false))); 
        collisionSys.AddSingle(bCollider1);

        int hardPushbackCnt = calcHardPushbacksEx(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, SNAP_INTO_PLATFORM_OVERLAP, effPushback, hardPushbackNorms, collisionHolder, ref overlapResult, ref primaryOverlapResult, out primaryOverlapIndex, _logger);

        _logger.LogInfo(String.Format("T#1 hardPushbackCnt={0}, primaryOverlapIndex={1}, primaryOverlapResult={2}", hardPushbackCnt, primaryOverlapIndex, primaryOverlapResult.ToString()));
        for (int k = 0; k < hardPushbackCnt; k++) {
            if (k == primaryOverlapIndex) continue;
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("T#1 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }

        Assert.True(1 == hardPushbackCnt);

        // Add a square barrier collider
        collisionSys.RemoveSingle(aCollider);
        rectCx += 0.5f*triangleEdgeLength;
        rectCy += 0.5f*triangleEdgeLength;
        aCollider.Shape.UpdateAsRectangle(rectCx, rectCy, rectCw, rectCh);
        collisionSys.AddSingle(aCollider);
        _logger.LogInfo("-------------------------------------------------------------------");
        _logger.LogInfo(String.Format("aCollider={0}", aCollider.Shape.ToString(false))); 
        _logger.LogInfo(String.Format("bCollider1={0}", bCollider1.Shape.ToString(false))); 
        float squareEdgeLength = 16f;
        var squarePoints = new float[8] { 0,0, squareEdgeLength,0, squareEdgeLength,squareEdgeLength, 0,squareEdgeLength};
        var (anchorCx2, anchorCy2) = (anchorCx1 + triangleEdgeLength, anchorCy1);
        var srcPolygon2 = new ConvexPolygon(anchorCx2, anchorCy2, squarePoints);
        var bCollider2 = NewConvexPolygonCollider(srcPolygon2, 0, 0, null);
        _logger.LogInfo(String.Format("bCollider2={0}", bCollider2.Shape.ToString(false))); 
        collisionSys.AddSingle(bCollider2);

        hardPushbackCnt = calcHardPushbacksEx(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, SNAP_INTO_PLATFORM_OVERLAP, effPushback, hardPushbackNorms, collisionHolder, ref overlapResult, ref primaryOverlapResult, out primaryOverlapIndex, _logger);
        _logger.LogInfo(String.Format("T#2 hardPushbackCnt={0}, primaryOverlapIndex={1}, primaryOverlapResult={2}", hardPushbackCnt, primaryOverlapIndex, primaryOverlapResult.ToString()));
        for (int k = 0; k < hardPushbackCnt; k++) {
            if (k == primaryOverlapIndex) continue;
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("T#2 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }
    }
}
