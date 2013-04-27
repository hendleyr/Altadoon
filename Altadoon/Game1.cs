using System;
using Altadoon.AltadoonEngine.Quad;
using Altadoon.Components.Camera;
using Altadoon.Components.Entities.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Altadoon {
    public class Game1 : Microsoft.Xna.Framework.Game {
        readonly GraphicsDeviceManager graphics;
        RasterizerState wireframeRasterizerState;
        RasterizerState defaultRasterizerState;
        bool isWireframe;
        readonly ContentManager content;
        QuadTree quadTree;
        private const float cellSize = 10.0f; // width of a single cell (between 2 points) in the heightmap
        long frameNum = 0;

        //Matrix ScreenViewMatrix;
        //Matrix ScreenProjectionMatrix;
        VertexBuffer nodesVertBuf;
        IndexBuffer nodesIndexBuf;
        const int maxNodeVerts = 768;
        static readonly VertexPositionColor[] _nodeVerts = new VertexPositionColor[maxNodeVerts];
        const int maxNodeIndices = maxNodeVerts * 2;
        //static short[] _nodeIndices = new short[maxNodeIndices];

        int numRocks = 500;

        Player player;

        // The direction the camera points without rotation.
        //const float lookAhead = 600.0f;
        //Vector3 cameraReference = new Vector3(0, 0, lookAhead);
        //float currentLookatY = 0.0f;
        //// Field of view of the camera in radians (pi/4 is 45 degrees).
        //static float viewAngle = MathHelper.PiOver4;
        ////Distance from the camera of the near and far clipping planes
        private const float nearClip = 0.1f;
        private const float farClip = 9999.9f;
        //// max acceleration that we allow user to fall
        //static float terminalVelocity = (5280.0f * 124.0f) / 60.0f / 60.0f;
        //// Rates in world units per 1/60th second (the default fixed step interval)
        //float rotationSpeed = 1f / 60f;
        //float forwardSpeed = 117.333f / 60f; // ~80mph
        //// acceleration vector
        //Vector3 acceleration = new Vector3(0.0f, 0.0f, 0.0f);

        public Game1() {
            graphics = new GraphicsDeviceManager(this);
            content = new ContentManager(Services);

            // setup for FULLSCREEN mode
            graphics.PreferredBackBufferWidth = 640;
            graphics.PreferredBackBufferHeight = 480;
            graphics.PreferMultiSampling = false;
            graphics.IsFullScreen = true; // un-comment for fullscreen
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize() {
            defaultRasterizerState = graphics.GraphicsDevice.RasterizerState;
            wireframeRasterizerState = new RasterizerState();
            wireframeRasterizerState.FillMode = FillMode.WireFrame;
            Viewport viewport = graphics.GraphicsDevice.Viewport;
            float aspectRatio = (float)viewport.Width / (float)viewport.Height;

            // View and projection matrices for when we want a "heads up display"
            //ScreenViewMatrix = Matrix.Identity;
            //ScreenProjectionMatrix = Matrix.CreateOrthographicOffCenter(0, graphics.GraphicsDevice.Viewport.Width, graphics.GraphicsDevice.Viewport.Height, 0, 0.0f, 1.0f);

            // make quadTree
            quadTree = new QuadTree(this);
            Components.Add(quadTree);

            player = new Player(this) {Position = new Vector3(250, 500, 250)};
            player.PlayerCamera.Perspective(80f, aspectRatio, nearClip, farClip);
            player.PlayerCamera.LookAt(new Vector3(250, 500, 0), player.Position, Vector3.Up);
            quadTree.AddComponent(player);
            
            base.Initialize(); // this will call Initialize on components
        }

        /// <summary>
        /// Load your graphics content.
        /// </summary>
        protected override void LoadContent() {
            // Create the terrain using a heightmap and the QuadTree component
            Texture2D heightMap = content.Load<Texture2D>("Content\\HeightMap");
            quadTree.Create(heightMap, cellSize, 16); 

            // Tell the quadTree what texture to use over the landscape
            quadTree.Texture = content.Load<Texture2D>("Content\\LandscapeTexture");
            nodesVertBuf = new VertexBuffer(graphics.GraphicsDevice, VertexPositionNormalTexture.VertexDeclaration, _nodeVerts.Length, BufferUsage.WriteOnly); //TODO:
            nodesIndexBuf = new IndexBuffer(graphics.GraphicsDevice, IndexElementSize.SixteenBits, maxNodeIndices, BufferUsage.WriteOnly);

            // Setup the QuadTree Effects
            quadTree.Effect.FogEnd = farClip - 2.0f;
            quadTree.Effect.FogStart = ComputeFogStart(nearClip, farClip);
            quadTree.Effect.FogColor = Color.CornflowerBlue.ToVector3();
            quadTree.Effect.FogEnabled = true;

            // load a rock model and place "numRocks" of them in the quadTree terrain
            // (we use a seperate Random instance so the rocks always 
            // get positioned in the same place each time the game starts
            Model rock = content.Load<Model>("Content\\AlienRock");
            SetModelEffectsToMatchQuadTree(rock);
            Random r = new Random(512);
            for (int i = 0; i < numRocks; i++) {
                float x, z;
                x = (float)r.NextDouble() * (510f * 30.0f); // 512x512 map with cellSize = 30
                z = (float)r.NextDouble() * (510f * 30.0f); // 512x512 map with cellSize = 30
                float scale = (float)r.NextDouble() * 0.05f + 0.05f;    // TODO: have to figure out why scale of some models is so off
                float rotV = (float)r.NextDouble() * 6.28f;
                Vector3 rot = new Vector3(0, rotV, scale);
                quadTree.AddModel(rock, new Vector3(x, 0.0f, z), scale, rot);
            }
                
            // TODO: load any other dynamic content
            // TODO: Load any static content
        }

        /// <summary>
        /// Unload your graphics content.
        /// </summary>
        protected override void UnloadContent() {
            content.Unload();
            quadTree = null;
            nodesIndexBuf = null;
            nodesVertBuf = null;
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input and playing audio.
        /// </summary>
        protected override void Update(GameTime gameTime) {
            frameNum++;

            if (isWireframe) {
                graphics.GraphicsDevice.RasterizerState = wireframeRasterizerState;
            }
            else {
                graphics.GraphicsDevice.RasterizerState = defaultRasterizerState;
            }

            // Allows the default game to exit on Xbox 360 and Windows
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed) {
                Exit();
            }

            // Quick Exit key (ESCAPE)
            if (Keyboard.GetState().IsKeyDown(Keys.Escape)) {
                Exit();
            }

            // Messy toggle wireframe
            if (Keyboard.GetState().IsKeyDown(Keys.Enter)) {
                isWireframe = !isWireframe;
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime) {
            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);

            // Tell the QuadTree Component what our view and projection matrices are
            // since QuadTree.Draw() will happen in base.Draw()
            quadTree.View = player.PlayerCamera.ViewMatrix;
            quadTree.Projection = player.PlayerCamera.ViewProjectionMatrix;
            quadTree.ViewProjection = player.PlayerCamera.ViewProjectionMatrix;

            // The Drawing of the Models added to the quadTree is done
            // by the quadTree when they are visible.
            base.Draw(gameTime); // Base Draw will call ComponentsCollection.Draw
        }

        float ComputeFogStart(float nearClip, float farClip) {
            return (farClip - nearClip) * 0.6f + nearClip;
        }

        private void SetModelEffectsToMatchQuadTree(Model model) {
            if (model == null)
                return;

            foreach (ModelMesh m in model.Meshes) {
                foreach (BasicEffect basicEffect in m.Effects) {
                    basicEffect.AmbientLightColor = quadTree.Effect.AmbientLightColor;

                    basicEffect.SpecularColor = quadTree.Effect.SpecularColor;
                    basicEffect.SpecularPower = 5.0f; // we'll set this up ourselves

                    basicEffect.DirectionalLight0.Enabled = quadTree.Effect.DirectionalLight0.Enabled;
                    basicEffect.DirectionalLight0.DiffuseColor = quadTree.Effect.DirectionalLight0.DiffuseColor;
                    basicEffect.DirectionalLight0.Direction = quadTree.Effect.DirectionalLight0.Direction;
                    basicEffect.DirectionalLight0.SpecularColor = quadTree.Effect.DirectionalLight0.SpecularColor;

                    basicEffect.DirectionalLight1.Enabled = quadTree.Effect.DirectionalLight1.Enabled;
                    basicEffect.DirectionalLight1.DiffuseColor = quadTree.Effect.DirectionalLight1.DiffuseColor;
                    basicEffect.DirectionalLight1.Direction = quadTree.Effect.DirectionalLight1.Direction;
                    basicEffect.DirectionalLight1.SpecularColor = quadTree.Effect.DirectionalLight1.SpecularColor;

                    basicEffect.DirectionalLight2.Enabled = false;

                    basicEffect.LightingEnabled = quadTree.Effect.LightingEnabled;

                    basicEffect.FogColor = quadTree.Effect.FogColor;
                    basicEffect.FogStart = quadTree.Effect.FogStart;
                    basicEffect.FogEnd = quadTree.Effect.FogEnd;
                    basicEffect.FogEnabled = quadTree.Effect.FogEnabled;
                }
            }
        }
    }
}