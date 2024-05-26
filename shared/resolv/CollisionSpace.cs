using System;

namespace shared {
    public class CollisionSpace {
        CollisionCell[,] Cells;
        int CellWidth, CellHeight; // Width and Height of each Cell in "world-space" / pixels / whatever

        public CollisionSpace(int spaceWidth, int spaceHeight, int cellWidth, int cellHeight) {
            CellWidth = cellWidth;
            CellHeight = cellHeight;

            int cellCntW = spaceWidth / cellWidth;
            int cellCntH = spaceHeight / cellHeight;

            Cells = new CollisionCell[cellCntH+1, cellCntW+1];
            for (int y = 0; y <= cellCntH; y++) {
                for (int x = 0; x <= cellCntW; x++) {
                    Cells[y, x] = new CollisionCell(x, y);
                }
            }
        }

        // WorldToSpace converts from a world position (x, y) to a position in the Space (a grid-based position).
        public (int, int) WorldToSpace(float x, float y) {
            int fx = (int)(Math.Floor(x / CellWidth));
            int fy = (int)(Math.Floor(y / CellHeight));
            return (fx, fy);
        }

        // SpaceToWorld converts from a position in the Space (on a grid) to a world-based position, given the size of the Space when first created.
        public (float, float) SpaceToWorld(int x, int y) {
            float fx = (float)(x * CellWidth);
            float fy = (float)(y * CellHeight);
            return (fx, fy);
        }

        public CollisionCell? GetCell(int x, int y) {
            if (0 <= y && y < Cells.GetLength(0) && 0 <= x && x < Cells.GetLength(1)) {
                return Cells[y, x];
            }
            return null;
        }

        public void AddSingle(Collider collider) {
            /*
               [WARNING] 

               1. For a static collider, this "AddSingle" would only be called once, thus no cleanup for static collider is needed.
               2. For a dynamic collider, this "AddSingle" would be called multiple times, but at the end of each "Step", we'd call "Space.RemoveSingle" to clean up for the dynamic collider.
             */
            collider.Space = this;
            var (cx, cy, ex, ey) = collider.BoundsToSpace(0, 0);
            for (int y = cy; y <= ey; y++) {
                for (int x = cx; x <= ex; x++) {
                    var c = GetCell(x, y);
                    if (null != c) {
                        if (collider.TouchingCells.Cnt >= collider.TouchingCells.N) {
                            throw new ArgumentException(String.Format("collider.TouchingCells is already full! Cnt={0}, N={1}: trying to insert cell X={2}, Y={3}", collider.TouchingCells.Cnt, collider.TouchingCells.N, x, y));
                        }
                        c.register(collider);
                        collider.TouchingCells.Put(c);
                    }
                }

            }

            if (null != collider.Shape) {
                collider.Shape.SetPosition(collider.X, collider.Y);
            }
        }

        public void RemoveSingle(Collider collider) {
            while (0 < collider.TouchingCells.Cnt) {
                var (_, cell) = collider.TouchingCells.Pop();
                if (null != cell) {
                    cell.unregister(collider);
                }
            }

            collider.Space = null;
        }
    
        public void RemoveAll() {
            foreach (var cell in Cells) {
                cell.unregisterAll();
            }
        }
    }
}
