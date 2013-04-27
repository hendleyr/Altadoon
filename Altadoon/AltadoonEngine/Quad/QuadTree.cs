using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Altadoon.AltadoonEngine.Quad
{
    public class QuadTree : DrawableGameComponent {
        private bool _valid;
        private QuadTreeNode _root;
        //private int m_sectorsize;
        private float _totalWidth;
        private float _totalHeight;
        private BasicEffect _basicEffect;
        private VertexDeclaration _vertexDecl;
        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;
        private Matrix _viewProjectionMatrix;
        private Collection<QuadTreeGameComponent> _gameComponents;

        /// <summary>
        /// Constructs a Quadtree object
        /// </summary>
        public QuadTree(Game game) : base(game) {
            _valid = false;
            _root = null;
            _gameComponents = new Collection<QuadTreeGameComponent>();
        }

        #region Properties
        /// <summary>
        /// Returns true if this QuadTree contains valid data for rendering
        /// </summary>
        public bool Valid {
            get { return _valid; }
        }

        /// <summary>
        /// Set the View matrix before calling Draw()
        /// </summary>
        public Matrix View {
            set { _viewMatrix = value; }
        }

        /// <summary>
        /// Set the Projection matrix before calling Draw()
        /// </summary>
        public Matrix Projection {
            set { _projectionMatrix = value; }
        }

        /// <summary>
        /// Set the concatenated ViewProjection matrix before calling Draw()
        /// </summary>
        public Matrix ViewProjection
        {
            set { _viewProjectionMatrix = value; }
        }

        /// <summary>
        /// Gets or Sets the Large Texture to use on the entire terrain once
        /// </summary>
        public Texture2D Texture {
            get { return _basicEffect.Texture; }
            set {
                _basicEffect.Texture = value;
                _basicEffect.TextureEnabled = (_basicEffect.Texture != null);
            }
        }

        /// <summary>
        /// Gets or Sets the ambient light for the terrain
        /// </summary>
        public Vector3 AmbientLight {
            get { return _basicEffect.AmbientLightColor; }
            set { _basicEffect.AmbientLightColor = value; }
        }

        /// <summary>
        /// Gets or Sets the value that determines whether to use fog on the terrain
        /// </summary>
        public bool FogEnabled {
            get { return _basicEffect.FogEnabled; }
            set { _basicEffect.FogEnabled = value; }
        }

        /// <summary>
        /// Gets or Sets the color for the Fog
        /// </summary>
        public Vector3 FogColor {
            get { return _basicEffect.FogColor; }
            set { _basicEffect.FogColor = value; }
        }

        /// <summary>
        /// Gets or Sets the distance from the camera to start the fog.
        /// </summary>
        public float FogStart {
            get { return _basicEffect.FogStart; }
            set { _basicEffect.FogStart = value; }
        }

        /// <summary>
        /// Gets or Sets the distance from the camera for the fog to completely obscure the terrain.
        /// </summary>
        public float FogEnd {
            get { return _basicEffect.FogEnd; }
            set { _basicEffect.FogEnd = value; }
        }

        /// <summary>
        /// Gets the BasicEffect object that this quadTree terrain uses
        /// </summary>
        public BasicEffect Effect {
            get { return _basicEffect; }
        }

        public float Width {
            get { return _totalWidth; }
        }
        public float Height {
            get { return _totalHeight; }
        }
        #endregion

        #region Overrides
        /// <summary>
        /// Initializes this QuadTree and any QuadTreeGameComponents
        /// that were added to it up till now.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize(); // base must initialize first so that this.graphicsDevice is valid

            _vertexDecl = new VertexDeclaration(VertexPositionNormalTexture.VertexDeclaration.GetVertexElements());
            CreateEffect();

            foreach (QuadTreeGameComponent component in _gameComponents) {
                component.Initialize();
            }
        }

        /// <summary>
        /// Calls Update on QuadTreeGameComponents that were added to 
        /// this QuadTree up till now.
        /// </summary>
        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            foreach (QuadTreeGameComponent component in _gameComponents)
                component.Update(gameTime);
        }

        protected override void LoadContent() {
            base.LoadContent();

            foreach (QuadTreeGameComponent component in _gameComponents) {
                component.Load();
            }
        }

        protected override void UnloadContent() {
            base.UnloadContent();

            foreach (QuadTreeGameComponent component in _gameComponents)
                component.Unload();
        }
        #endregion

        /// <summary>
        /// Draws the terrain contained in this QuadTree.
        /// </summary>
        /// <param name="gameTime">Time since last Draw</param>
        public override void Draw(GameTime gameTime) {
            if (_basicEffect == null) {
                CreateEffect();
            }

            if (_valid && _root != null) {
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.View = _viewMatrix;
                _basicEffect.World = Matrix.Identity;
                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes) {
                    pass.Apply();
                    GraphicsDevice.SetVertexBuffer(new VertexBuffer(GraphicsDevice, _vertexDecl, _vertexDecl.GetVertexElements().Length, BufferUsage.None));
                    GraphicsDevice.RasterizerState.CullMode = CullMode.CullCounterClockwiseFace;

                    _root.Draw(GraphicsDevice, _viewProjectionMatrix, true);
                }

                // Draw any models and components in the tree
                _root.DrawModels(_viewMatrix, _projectionMatrix);
                _root.DrawComponents(gameTime, _viewMatrix, _projectionMatrix);
            }
        }

        /// <summary>
        /// Creates the QuadTree nodes and initializes them for rendering.
        /// This method must be called after content is loaded but before Draw().
        /// The texture heightmap is no longer needed by QuadTree upon return.
        /// </summary>
        /// <param name="heightMap">An uncompressed Texture2D HeightMap to use for height data.</param>
        /// <param name="cellSize">Width/Height of a cell in the terrain</param>
        /// <param name="sectorsize">Number of cells in x and z that will be rendered or culled as a unit</param>
        public bool Create(Texture2D heightMap, float cellSize, int sectorSize) {
            Int32[] data = new Int32[heightMap.Width * heightMap.Height];
            heightMap.GetData<Int32>(data);

            float[,] floatHeightData = ConvertARGBtoFloatHeightData(heightMap.Width, heightMap.Height, 2.5f, data);
            data = null; // done with it..

            return Create(cellSize, sectorSize, floatHeightData);
        }

        /// <summary>
        /// Creates the QuadTree nodes and initializes them for rendering.
        /// This method must be called after content is loaded but before Draw().
        /// </summary>
        /// <param name="cellSize">Width/Height of a cell in the terrain</param>
        /// <param name="sectorsize">Number of cells in x and z that will be rendered or culled as a unit</param>
        /// <param name="heightData">The HeightData to use.</param>
        public bool Create(float cellSize, int sectorSize, float[,] heightData) {
            int width = heightData.GetLength(1);
            int height = heightData.GetLength(0);

            _totalWidth = cellSize * width;
            _totalHeight = cellSize * height;
            //m_sectorsize = sectorSize;

            if (height > heightData.GetLength(0))
                _valid = false;
            if (width > heightData.GetLength(1))
                _valid = false;

            Vector3[,] normals = CreateNormals(cellSize, heightData);

            _root = new QuadTreeNode(null);
            _valid = _root.Buildout(GraphicsDevice, cellSize, sectorSize, 0, 0, width, height, heightData, normals);
            if (!_valid)
                _root = null;

            normals = null; // don't need them anymore here

            // if we have QuadTreeGameComponents added to the quadTree already
            // then distribute them to the new nodes
            if (_valid && _root != null) {
                foreach (QuadTreeGameComponent component in _gameComponents) {
                    _root.AddComponent(component);
                }
            }

            return _valid;
        }

        /// <summary>
        /// Returns the interpolated height on the heightmap at location x,z
        /// will return a negative number (<0.0f) if off the terrain.
        /// </summary>
        /// <param name="x">X location within terrain</param>
        /// <param name="z">Z location within terrain</param>
        public float GetHeightAt(float x, float z) {
            return _root.GetHeightAt(x, z);
        }

        /// <summary>
        /// Add a model to the terrain.  
        /// Returns null if not on terrain or QuadTree not created.
        /// </summary>
        /// <param name="model">Model to add</param>
        /// <param name="position">where to put the model</param>
        /// <param name="scale">scaling</param>
        /// <param name="rotation">orientation, rotation about the Y/Up axis</param>
        public QuadTreeModelInfo AddModel(Model model, Vector3 position, float scale, Vector3 rotation) {
            if (_valid && _root != null) {
                QuadTreeModelInfo modelInfo = new QuadTreeModelInfo(model, position, scale, rotation);
                if (_root.AddModel(modelInfo)) //position, scale, rotationY, model);
                    return modelInfo;
            }
            return null;
        }

        /// <summary>
        /// Lets the QuadTree know of the new position of a model
        /// </summary>
        /// <param name="modelInfo">ModelInfo object returned from AddModel</param>
        /// <param name="position">where to put the model</param>
        public void UpdateModelPosition(QuadTreeModelInfo modelInfo, Vector3 position) {
            if (_valid && _root != null) {
                if (modelInfo != null) {
                    if (modelInfo._node != null) {
                        modelInfo._node.RemoveModel(modelInfo);
                    }
                    modelInfo._position = position;
                    _root.AddModel(modelInfo);
                }
            }
        }

        /// <summary>
        /// Removes a model and it's associated data from the terrain
        /// Returns the Model that was contained in the QuadTreeModelInfo,
        /// or null if modelInfo is null or contains a null Model reference.
        /// </summary>
        /// <param name="modelInfo">A QuadtreeModelInfo object returned from AddModel()</param>
        public Model RemoveModel(QuadTreeModelInfo modelInfo) {
            if (modelInfo != null) {
                if (modelInfo._node != null) {
                    modelInfo._node.RemoveModel(modelInfo);
                }
                return modelInfo._model;
            }
            return null;
        }

        public bool AddComponent(QuadTreeGameComponent component) {
            if (!_gameComponents.Contains(component))
                _gameComponents.Add(component);

            if (component.qNode != null && component.quadTree == this)
                component.qNode.RemoveComponent(component);
            component.qNode = null;

            component.quadTree = this;

            if (_valid && _root != null)
                _root.AddComponent(component);

            return true;
        }

        /// <summary>
        /// Removes a QuadTreeGameComponent derived object from the QuadTree
        /// </summary>
        /// <param name="component">A QuadTreeGameComponent object previously added to the QuadTree</param>
        public void RemoveComponent(QuadTreeGameComponent component) {
            if (component != null) {
                if (component.quadTree != this) {
                    return; // wrong quadTree or never added
                }

                if (component.qNode != null) {
                    component.qNode.RemoveComponent(component);
                }
                component.qNode = null;

                if (_gameComponents.Contains(component)) {
                    _gameComponents.Remove(component);
                }
                component.quadTree = null;
            }
        }

        //-----------------------------------------------
        //  Create Normals
        //-----------------------------------------------
        private Vector3[,] CreateNormals(float cellSize, float[,] heightData) {
            int height = heightData.GetLength(0);
            int width = heightData.GetLength(1);

            Vector3[,] normals = new Vector3[height, width];
            if (normals == null) {
                throw new Exception("QuadTree:CreateNormals() Out of memory for Normals");
            }


            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if (x == 0 || y == 0 || x == (width - 1) || y == (height - 1)) {
                        // Border vertices (lazy way to deal with these special cases)
                        normals[y, x] = Vector3.Up;
                    }
                    else {
                        Vector3 pos = new Vector3(cellSize * x, heightData[y, x], cellSize * y);
                        Vector3 pos2;
                        Vector3 pos3;
                        Vector3 norm1;
                        Vector3 norm2;
                        Vector3 norm3;
                        Vector3 norm4;
                        Vector3 norm5;
                        Vector3 norm6;

                        pos2 = new Vector3(cellSize * (x), heightData[y - 1, x], cellSize * (y - 1));
                        pos3 = new Vector3(cellSize * (x - 1), heightData[y, x - 1], cellSize * (y));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm1 = Vector3.Cross(pos2, pos3);

                        pos2 = new Vector3(cellSize * (x - 1), heightData[y, x - 1], cellSize * (y));
                        pos3 = new Vector3(cellSize * (x - 1), heightData[y + 1, x - 1], cellSize * (y + 1));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm2 = Vector3.Cross(pos2, pos3);

                        pos2 = new Vector3(cellSize * (x - 1), heightData[y + 1, x - 1], cellSize * (y + 1));
                        pos3 = new Vector3(cellSize * (x), heightData[y + 1, x], cellSize * (y + 1));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm3 = Vector3.Cross(pos2, pos3);

                        pos2 = new Vector3(cellSize * (x), heightData[y + 1, x], cellSize * (y + 1));
                        pos3 = new Vector3(cellSize * (x + 1), heightData[y, x + 1], cellSize * (y));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm4 = Vector3.Cross(pos2, pos3);

                        pos2 = new Vector3(cellSize * (x + 1), heightData[y, x + 1], cellSize * (y));
                        pos3 = new Vector3(cellSize * (x + 1), heightData[y - 1, x + 1], cellSize * (y - 1));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm5 = Vector3.Cross(pos2, pos3);

                        pos2 = new Vector3(cellSize * (x + 1), heightData[y - 1, x + 1], cellSize * (y - 1));
                        pos3 = new Vector3(cellSize * (x), heightData[y - 1, x], cellSize * (y - 1));
                        pos2 -= pos;
                        pos3 -= pos;
                        pos2.Normalize();
                        pos3.Normalize();
                        norm6 = Vector3.Cross(pos2, pos3);

                        Vector3 norm = (norm1 + norm2 + norm3 + norm4 + norm5 + norm6) / 6.0f;

                        normals[y, x] = Vector3.Normalize(norm);
                    }
                }
            }
            return normals;
        }

        //-----------------------------------------------
        // Convert a textures data to an array of floats
        //-----------------------------------------------
        protected float[,] ConvertARGBtoFloatHeightData(int width, int height, float yScale, Int32[] data) {
            int size = data.Length;
            if (size != width * height) {
                return null;
            }

            float[,] ret = new float[height, width];

            int row = 0;
            int col = 0;
            for (int i = 0; i < size; i++) {
                ret[row, col] = (float)(data[i] & 0x000000FF) * yScale;
                col++;
                col %= width;
                if (col == 0) {
                    row++;
                }
            }
            return ret;
        }

        //-----------------------------------------------
        //  CreateEffect for the terrain
        //-----------------------------------------------
        protected void CreateEffect() {
            _basicEffect = new BasicEffect(GraphicsDevice);
            _basicEffect.Alpha = 1.0f;
            _basicEffect.DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f); //(1.0f, 1.0f, 0.0f);
            _basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
            _basicEffect.SpecularPower = 5.0f;

            _basicEffect.AmbientLightColor = new Vector3(0.4f, 0.4f, 0.5f);

            _basicEffect.DirectionalLight0.Enabled = true;
            _basicEffect.DirectionalLight0.DiffuseColor = Vector3.One;
            _basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(1.5f, -0.15f, -0.5f));
            _basicEffect.DirectionalLight0.SpecularColor = Vector3.One;

            _basicEffect.DirectionalLight1.Enabled = false; // true;
            _basicEffect.DirectionalLight1.DiffuseColor = new Vector3(0.5f, 0.5f, 0.5f);
            _basicEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-1.0f, -1.0f, 1.0f));
            _basicEffect.DirectionalLight1.SpecularColor = new Vector3(0.5f, 0.5f, 0.5f);

            _basicEffect.LightingEnabled = true;

            _basicEffect.FogColor = Color.CornflowerBlue.ToVector3();
            _basicEffect.FogStart = 3000.0f;
            _basicEffect.FogEnd = 4000.0f - 2.0f;
            _basicEffect.FogEnabled = true;
        }
    }
}
