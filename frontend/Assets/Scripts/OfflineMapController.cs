using UnityEngine;
using System;
using shared;
using static shared.Battle;

public class OfflineMapController : AbstractMapController {
    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        return false;
    }

    // Start is called before the first frame update
    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;

        selfPlayerInfo = new CharacterDownsync();
        
        roomCapacity = 1;
        preallocateHolders();
        resetCurrentMatch();
        selfPlayerInfo.JoinIndex = 1;
        var startRdf = mockStartRdf();
        onRoomDownsyncFrame(startRdf, null);
    }

    // Update is called once per frame
    void Update() {
        try {
            doUpdate();
        } catch (Exception ex) {
            Debug.LogError(String.Format("Error during OfflineMap.Update {0}", ex));
            onBattleStopped();
        }
    }

    void OnRenderObject() {
        if (debugDrawingEnabled) {
            return;
        }
        // The magic name "OnRenderObject" is the only callback I found working to draw the debug boundaries.
        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        // Set transformation matrix for drawing to
        // match our transform
        GL.MultMatrix(transform.localToWorldMatrix);

        // The anchoring quad for testing
        /*
        GL.Begin(GL.QUADS);
        GL.Vertex3(1024, -512, 0);
        GL.Vertex3(1040, -512, 0);
        GL.Vertex3(1040, -496, 0);
        GL.Vertex3(1024, -496, 0);
        GL.End();
        */
        // Draw static colliders
        foreach (var collider in staticRectangleColliders) {
            if (null == collider) {
                break;
            }
            if (null == collider.Shape) {
                throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
            }
            if (null == collider.Shape.Points) {
                throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
            }
            GL.Begin(GL.LINES);
            for (int i = 0; i < 4; i++) {
                int j = i + 1;
                if (j >= 4) j -= 4;
                var (_, pi) = collider.Shape.Points.GetByOffset(i);
                var (_, pj) = collider.Shape.Points.GetByOffset(j);
                var (ix, iy) = CollisionSpacePositionToWorldPosition(collider.X + pi.X, collider.Y + pi.Y, spaceOffsetX, spaceOffsetY);
                var (jx, jy) = CollisionSpacePositionToWorldPosition(collider.X + pj.X, collider.Y + pj.Y, spaceOffsetX, spaceOffsetY);
                GL.Vertex3(ix, iy, 0);
                GL.Vertex3(jx, jy, 0);
            }
            GL.End();
        }

        // Draw dynamic colliders
        var (_, rdf) = renderBuffer.GetByFrameId(renderFrameId);
        if (null != rdf) {
            for (int k = 0; k < roomCapacity; k++) {
                var currCharacterDownsync = rdf.PlayersArr[k];
                float boxCx, boxCy, boxCw, boxCh;
                calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
                
                var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
                // World space width and height are just the same as that of collision space.

                GL.Begin(GL.LINES);

                GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                GL.End();
            }
        }
        GL.PopMatrix();
    }
}
