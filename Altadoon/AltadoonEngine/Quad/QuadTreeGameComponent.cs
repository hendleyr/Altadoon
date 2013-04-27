using Microsoft.Xna.Framework;

namespace Altadoon.AltadoonEngine.Quad
{
    public class QuadTreeGameComponent : DrawableGameComponent {
        internal QuadTree quadTree;
        internal QuadTreeNode qNode;
        internal Vector3 position;

        public QuadTreeGameComponent(Game game) : base(game) {
            quadTree = null;
            qNode = null;
        }

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);
        }

        public virtual void Draw(GameTime gameTime, Matrix view, Matrix projection) {
            Draw(gameTime);
        }

        public Vector3 Position {
            get { return position; }
            set {
                position = value;
                if (qNode != null)
                {
                    if (qNode.IsInThisNode(Position)) {
                        return;
                    }
                    qNode.RemoveComponent(this);
                }

                if (quadTree != null) {
                    quadTree.AddComponent(this);
                }
            }
        }

        public QuadTree QuadTree {
            get { return quadTree; }
        }

        internal void Load() {
            LoadContent();
        }

        internal void Unload() {
            UnloadContent();
        }
    }
}
