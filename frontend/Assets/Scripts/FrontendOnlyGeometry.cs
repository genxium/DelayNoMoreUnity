using UnityEngine;

public class FrontendOnlyGeometry {
    public static (float, float) TiledLayerPositionToWorldPosition(float tiledLayerX, float tiledLayerY) {
        return (tiledLayerX, - tiledLayerY);
    }

    public static (float, float) WorldPositionToCollisionSpacePosition(float wx, float wy, float tilemapHalfHeight, float collisionSpacePaddingLeft, float collisionSpacePaddingBottom) {
        float cx = wx - collisionSpacePaddingLeft;
        float cy = tilemapHalfHeight + tilemapHalfHeight + wy - collisionSpacePaddingBottom;
        return (cx, cy);
    }

    public static (float, float) TiledLayerPositionToCollisionSpacePosition(float tiledLayerX, float tiledLayerY, float tilemapHalfHeight, float collisionSpacePaddingLeft, float collisionSpacePaddingBottom) {
        var (wx, wy) = TiledLayerPositionToWorldPosition(tiledLayerX, tiledLayerY);
        return WorldPositionToCollisionSpacePosition(wx, wy, tilemapHalfHeight, collisionSpacePaddingLeft, collisionSpacePaddingBottom);
    }

    public static (float, float) CollisionSpacePositionToWorldPosition(float cx, float cy, float tilemapHalfHeight, float collisionSpacePaddingLeft, float collisionSpacePaddingBottom) {
        // [WARNING] This conversion is specifically added for Unity+SuperTiled2Unity
        return (cx + collisionSpacePaddingLeft, -tilemapHalfHeight-tilemapHalfHeight + collisionSpacePaddingBottom + cy);
    }
}
