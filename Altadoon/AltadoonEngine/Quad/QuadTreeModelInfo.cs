using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Altadoon.AltadoonEngine.Quad
{
    public class QuadTreeModelInfo
    {
        internal Model _model;
        internal Vector3 _position;
        internal float _scale;
        internal Vector3 _rotation;
        internal QuadTreeNode _node;

        internal QuadTreeModelInfo(Model model, Vector3 position, float scale, Vector3 rotation) {
            _model = model;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            _node = null;
        }

        public Vector3 Position {
            get { return _position; }
            set { _position = value; }
        }
        
        public Model Model {
            get { return _model; }
        }
        
        public float Scale {
            get { return _scale; }
        }
        
        public Vector3 Rotation {
            get { return _rotation; }
            set { _rotation = value; }
        }
    }
}
