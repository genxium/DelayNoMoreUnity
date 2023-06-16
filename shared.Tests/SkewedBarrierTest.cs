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

        var effPushback = new Vector(0, 0);
        var hardPushbackNorms = new Vector[5];
        for (int i = 0; i < hardPushbackNorms.Length; i++) {
            hardPushbackNorms[i] = new Vector(0, 0);
        }
        var (rectCx, rectCy) = (36f, 28f);
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

        // Add a slope barrier collider
        _logger.LogInfo("-------------------------------------------------------------------");
        collisionSys.AddSingle(aCollider);
        _logger.LogInfo(String.Format("aCollider={0}", aCollider.Shape.ToString(false) + "; touchingCells: " + aCollider.TouchingCellsStr())); 
        var points1 = new float[8] { 0,0, 0,-16f, 28f,-16f, 28f,16f};
        var (anchorCx1, anchorCy1) = (36f, 20f);
        var bCollider1 = NewConvexPolygonCollider(new ConvexPolygon(anchorCx1, anchorCy1, points1), 0, 0, null);
        collisionSys.AddSingle(bCollider1);
        _logger.LogInfo(String.Format("bCollider1={0}", bCollider1.Shape.ToString(false) + "; touchingCells: " + bCollider1.TouchingCellsStr())); 

        int primaryOverlapIndex = -1;
        int hardPushbackCnt = calcHardPushbacksNorms(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, hardPushbackNorms, collisionHolder, ref overlapResult, ref primaryOverlapResult, out primaryOverlapIndex, _logger);

        _logger.LogInfo(String.Format("T#1 hardPushbackCnt={0}, primaryOverlapResult={1}", hardPushbackCnt, primaryOverlapResult.ToString()));
        for (int k = 0; k < hardPushbackCnt; k++) {
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("T#1 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }
        Assert.True(1 == hardPushbackCnt);

        // Add a square barrier collider
        _logger.LogInfo("-------------------------------------------------------------------");
		primaryOverlapResult.reset();
		collisionSys.RemoveSingle(bCollider1);
        _logger.LogInfo(String.Format("aCollider={0}", aCollider.Shape.ToString(false) + "; touchingCells: " + aCollider.TouchingCellsStr())); 
        float squareEdgeLength = 16f;
        var points2 = new float[8] { 0,0, squareEdgeLength,0, squareEdgeLength,squareEdgeLength, 0,squareEdgeLength};
        var (anchorCx2, anchorCy2) = (20f, 4f);
        var bCollider2 = NewConvexPolygonCollider(new ConvexPolygon(anchorCx2, anchorCy2, points2), 0, 0, null);
        collisionSys.AddSingle(bCollider2);
        _logger.LogInfo(String.Format("bCollider2={0}", bCollider2.Shape.ToString(false) + "; touchingCells: " + bCollider2.TouchingCellsStr())); 

        hardPushbackCnt = calcHardPushbacksNorms(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, hardPushbackNorms, collisionHolder, ref overlapResult, ref primaryOverlapResult, out primaryOverlapIndex, _logger);
        _logger.LogInfo(String.Format("T#2 hardPushbackCnt={0}, primaryOverlapResult={1}", hardPushbackCnt, primaryOverlapResult.ToString()));
        for (int k = 0; k < hardPushbackCnt; k++) {
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("T#2 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }
        Assert.True(1 == hardPushbackCnt);

        // Add both colliders
        _logger.LogInfo("-------------------------------------------------------------------");
		collisionSys.AddSingle(bCollider1);

        hardPushbackCnt = calcHardPushbacksNorms(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aCollider.Shape, hardPushbackNorms, collisionHolder, ref overlapResult, ref primaryOverlapResult, out primaryOverlapIndex, _logger);
        _logger.LogInfo(String.Format("T#3 hardPushbackCnt={0}, primaryOverlapResult={1}", hardPushbackCnt, primaryOverlapResult.ToString()));
        for (int k = 0; k < hardPushbackCnt; k++) {
            var hardPushbackNorm = hardPushbackNorms[k];
            _logger.LogInfo(String.Format("T#3 hardPushbackNorms[{0}]={{ {1}, {2} }}", k, hardPushbackNorm.X, hardPushbackNorm.Y));
        }
        Assert.True(2 == hardPushbackCnt);
    }
}
