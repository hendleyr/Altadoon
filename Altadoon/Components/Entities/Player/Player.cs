using System.Linq;
using Altadoon.AltadoonEngine.Quad;
using Altadoon.Components.Camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Altadoon.Components.Entities.Player
{
    public class Player : QuadTreeGameComponent
    {
        private PlayerCamera _playerCamera;
        private QuadTreeModelInfo _playerModel;
        //private Entity _playerEntity;
        //private BoundingBox _boundingBox;

        #region Public Methods
        public Player(Game game) : base(game)
        {
            _playerModel = new QuadTreeModelInfo(game.Content.Load<Model>("Content\\BabyBeholder"), position, 1.0f, Vector3.Zero);
            _playerCamera = new PlayerCamera();
        }

        public override void Update(GameTime gameTime)
        {
            _playerModel.Position = position;

            if (Keyboard.GetState().IsKeyDown(Keys.D))
            {
                _playerCamera.Rotate(50, 0.0f);
            }
            else if (Keyboard.GetState().IsKeyDown(Keys.A))
            {
                _playerCamera.Rotate(-50, 0.0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.W))
            {
                //position = position + (_playerCamera.ViewDirection*50);
                _playerCamera.OffsetDistance = _playerCamera.OffsetDistance - 50;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.S))
            {
                //position = position - (_playerCamera.ViewDirection*50);
                _playerCamera.OffsetDistance = _playerCamera.OffsetDistance + 50;
            }

            _playerCamera.LookAtTarget(position);
            _playerCamera.Update(gameTime);

            System.Console.Out.Write("\nCamera position: " + _playerCamera.Position + 
                "\nCamera distance: " + _playerCamera.OffsetDistance +
                "\nTarget position: " + _playerCamera.Target + 
                "\nView direction: " + _playerCamera.ViewDirection + 
                "\nModel position: " + _playerModel.Position +
                "\nScene position: " + position + "\n");
        }

        public override void Draw(GameTime gameTime, Matrix view, Matrix projection)
        {
            Model m = _playerModel._model;
            Vector3 modelPosition = _playerModel._position;
            float modelScale = _playerModel._scale;
            Vector3 modelRotation = _playerModel._rotation;

            Matrix[] transforms = new Matrix[m.Bones.Count];
            m.CopyAbsoluteBoneTransformsTo(transforms);

            //Draw the model, a model can have multiple meshes, so loop
            foreach (ModelMesh mesh in m.Meshes)
            {
                //System.Console.Write("\nMesh Name: " + mesh.Name);
                //This is where the mesh orientation is set, as well as our camera and projection
                foreach (BasicEffect effect in m.Meshes[0].Effects)
                {
                    //effect.EnableDefaultLighting();
                    effect.World = transforms[mesh.ParentBone.Index] * Matrix.CreateScale(modelScale)
                        * Matrix.CreateRotationX(modelRotation.X)
                        * Matrix.CreateRotationZ(modelRotation.Z)
                        * Matrix.CreateRotationY(modelRotation.Y)
                        * Matrix.CreateTranslation(modelPosition);
                    effect.View = view;
                    effect.Projection = projection;
                }
                //Draw the mesh, will use the effects set above.
                mesh.Draw();
            }
        }
        #endregion

        #region Properties
        public PlayerCamera PlayerCamera { get { return _playerCamera; } }

        //public Entity PlayerEntity { get { return _playerEntity; } }
        #endregion

    }
}
