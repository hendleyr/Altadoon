using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Altadoon.AltadoonEngine.Quad {
    public class QuadTreeNode {
        const int NW = 0, NE = 1, SW = 2, SE = 3;
        private static float OutOfRange = -987654321.0f;

        //-------------------------------------------
        //  Instance variables
        //-------------------------------------------
        protected float m_startX;
        protected float m_startY;
        protected float m_endX;
        protected float m_endY;
        protected float m_minHeight;
        protected float m_maxHeight;

        // Pre-calulate the corners or this node (used by TryCull)
        protected Vector3[] m_corners;
        // projected corners will be calculated in TryCull each frame
        protected Vector4[] m_projCorners;

        protected int m_width;
        protected int m_height;
        protected float m_cellSize;
        protected float[,] m_heightData;

        protected bool nodeDrawn;
        protected QuadTreeNode[] children;

        protected int m_numVerts;
        protected VertexBuffer m_vertexBuffer;
        protected int m_numTris;
        protected IndexBuffer m_indexBuffer;

        protected QuadTreeNode m_parent;

        private Collection<QuadTreeModelInfo> _models;
        private Collection<QuadTreeGameComponent> _gameComponents;

        public QuadTreeNode(QuadTreeNode parent) {
            m_parent = parent;

            children = new QuadTreeNode[4];

            // TryCull instance variables
            m_corners = new Vector3[8];
            m_projCorners = new Vector4[8];

            // Models and Components collections
            _models = new Collection<QuadTreeModelInfo>();
            _gameComponents = new Collection<QuadTreeGameComponent>();
        }

        //-------------------------------------------
        // Build the quadTree using the height data
        //-------------------------------------------
        public bool Buildout(GraphicsDevice gd, float cellSize, int sectorsize, int minX, int minY, int width, int height, float[,] heightData, Vector3[,] normals) {
            m_width = width;
            m_height = height;

            if (minX + m_width >= heightData.GetLength(1))
                m_width = heightData.GetLength(1) - minX - 1; // handle end of row case ( sectorsize - 1)
            if (minY + m_height >= heightData.GetLength(0))
                m_height = heightData.GetLength(0) - minY - 1; // handle end of column case (sectorsize - 1)

            m_cellSize = cellSize;

            // set instance variables that define world coords of this sector
            m_startX = cellSize * (float)minX;
            m_startY = cellSize * (float)minY;
            m_endX = cellSize * (float)(minX + m_width);
            m_endY = cellSize * (float)(minY + m_height);

            // now we don't need to do any more if we will delegate to children
            if (m_width > sectorsize || m_height > sectorsize) {
                int halfWidth = m_width / 2;
                if ((m_width & 0x01) == 0x01) {
                    halfWidth++;
                }
                int halfHeight = m_height / 2;
                if ((halfHeight & 0x01) == 0x01) {
                    halfHeight++;
                }

                m_minHeight = 999.0f;
                m_maxHeight = -999.0f;

                if (children[NW] == null) {
                    children[NW] = new QuadTreeNode(this);
                }
                children[NW].Buildout(gd, cellSize, sectorsize, minX, minY, halfWidth, halfHeight, heightData, normals);
                m_minHeight = children[NW].m_minHeight < m_minHeight ? children[NW].m_minHeight : m_minHeight;
                m_maxHeight = children[NW].m_maxHeight > m_maxHeight ? children[NW].m_maxHeight : m_maxHeight;

                if (width > sectorsize) {
                    if (children[NE] == null)
                        children[NE] = new QuadTreeNode(this);
                    children[NE].Buildout(gd, cellSize, sectorsize, minX + halfWidth, minY, halfWidth, halfHeight, heightData, normals);
                    m_minHeight = children[NE].m_minHeight < m_minHeight ? children[NE].m_minHeight : m_minHeight;
                    m_maxHeight = children[NE].m_maxHeight > m_maxHeight ? children[NE].m_maxHeight : m_maxHeight;
                }
                if (height > sectorsize) {
                    if (children[SW] == null)
                        children[SW] = new QuadTreeNode(this);
                    children[SW].Buildout(gd, cellSize, sectorsize, minX, minY + halfHeight, halfWidth, halfHeight, heightData, normals);
                    m_minHeight = children[SW].m_minHeight < m_minHeight ? children[SW].m_minHeight : m_minHeight;
                    m_maxHeight = children[SW].m_maxHeight > m_maxHeight ? children[SW].m_maxHeight : m_maxHeight;
                }

                if (height > sectorsize && width > sectorsize) {
                    if (children[SE] == null)
                        children[SE] = new QuadTreeNode(this);
                    children[SE].Buildout(gd, cellSize, sectorsize, minX + halfWidth, minY + halfHeight, halfWidth, halfHeight, heightData, normals);
                    m_minHeight = children[SE].m_minHeight < m_minHeight ? children[SE].m_minHeight : m_minHeight;
                    m_maxHeight = children[SE].m_maxHeight > m_maxHeight ? children[SE].m_maxHeight : m_maxHeight;
                }

                // find the 4 corners of this node (flat)
                // these are used in TryCull
                ComputeCorners();

                //----------------------------------------
                // we're done here!
                // We will only keep track of children nodes,
                // no data needed for rendering in this node
                //----------------------------------------
                return true;
            }

            //--------------------------------------
            // we are a lowest level child and have 
            // arrived at width and height <= sectorsize
            // save the height data for this sector
            // and create the data for rendering
            //--------------------------------------

            // we actually duplicate verts and heightdata on borders of sectors
            // that is, m_width and m_height are the number of cells
            // so the number of vertices is (m_width+1) * (m_height+1).
            // We save the heightdata in an instance variable so that we 
            // can compute GetHeightAt(x,y) later.

            m_heightData = new float[m_height + 1, m_width + 1];
            if (m_heightData == null)
                throw new Exception("QuadtreeNode.BuildOut : Out of memory for local heightData");

            m_minHeight = 9999.0f;
            m_maxHeight = -9999.0f;

            for (int y = 0; y <= m_height; y++) {
                for (int x = 0; x <= m_width; x++) {
                    m_heightData[y, x] = heightData[minY + y, minX + x];

                    float data = m_heightData[y, x];

                    if (data > m_maxHeight)
                        m_maxHeight = data;
                    if (data < m_minHeight)
                        m_minHeight = data;
                }
            }

            // find the 4 corners of this node (flat)
            // these are used in TryCull
            ComputeCorners();

            float totalHeight = (float)heightData.GetLength(0);
            float totalWidth = (float)heightData.GetLength(1);

            float ustep = 1.0f / totalWidth;// / (float )m_width;
            float ustart = (float)minX * ustep;
            float vstep = 1.0f / totalHeight;// / (float )m_height;
            float vstart = (float)minY * vstep;

            return CreateMeshFromHeightData(gd, minX, minY, ustart, ustep, vstart, vstep, normals);
        }

        //-------------------------------------------
        //  Create the Vertex Buffer and Index Buffer
        //  for this node
        //-------------------------------------------
        protected bool CreateMeshFromHeightData(GraphicsDevice gd, int minX, int minY, float ustart, float ustep, float vstart, float vstep, Vector3[,] normals) {
            // we duplicate vertices on sector borders (therefore, the +1)
            m_numVerts = (m_width + 1) * (m_height + 1);

            // Create the vertices list
            VertexPositionNormalTexture[] vertList = new VertexPositionNormalTexture[m_numVerts];
            if (vertList == null) {
                throw new Exception("QuadtreeNode.CreateMeshFromHeightData : Out of memory for vertList");
            }

            int i = 0;
            for (int y = 0; y <= m_height; y++) {
                for (int x = 0; x <= m_width; x++) {
                    vertList[i].Position = new Vector3(m_startX + (x * m_cellSize), m_heightData[y, x], m_startY + (y * m_cellSize));
                    vertList[i].Normal = normals[minY + y, minX + x];
                    vertList[i].TextureCoordinate = new Vector2(ustart + (x * ustep), vstart + (y * vstep));
                    i++;
                }
            }

            // Initialize the vertex buffer, allocating memory for each vertex
            m_vertexBuffer = new VertexBuffer(gd, VertexPositionNormalTexture.VertexDeclaration, m_numVerts, BufferUsage.None);

            if (m_vertexBuffer == null)
                throw new Exception("QuadtreeNode.CreateMeshFromHeightData : Out of memory for VertexBuffer");

            // Set the vertex buffer data to the array of vertices
            m_vertexBuffer.SetData<VertexPositionNormalTexture>(vertList);

            // Create the indices that index into the vertex buffer
            m_numTris = m_width * m_height * 2; // 2 tri's per square (cell)
            short[] indices = new short[m_numTris * 3]; // 3 indices per tri

            if (indices == null) {
                throw new Exception("QuadtreeNode.CreateMeshFromHeightData : Out of memory for indices");
            }

            int vertsPerRow = m_width + 1;
            i = 0;
            for (int y = 0; y < m_height; y++) {
                int vertStart = y * vertsPerRow; // set to first vertice used in this row of indices

                for (int x = 0; x < m_width; x++) {
                    // cells upper left tri
                    indices[i] = (short)(vertStart + vertsPerRow);
                    i++;
                    indices[i] = (short)vertStart;
                    i++;
                    indices[i] = (short)(vertStart + 1);
                    i++;

                    // cells lower right tri
                    indices[i] = (short)(vertStart + vertsPerRow);
                    i++;
                    indices[i] = (short)(vertStart + 1);
                    i++;
                    indices[i] = (short)(vertStart + vertsPerRow + 1);
                    i++;

                    vertStart++; // set to next column of verts
                }
            }

            // create an index buffer with our indices
            m_indexBuffer = new IndexBuffer(gd, IndexElementSize.SixteenBits, indices.Length, BufferUsage.None);

            if (m_indexBuffer == null) {
                throw new Exception("QuadtreeNode.CreateMeshFromHeightData : Out of memory for IndexBuffer");
            }

            m_indexBuffer.SetData(indices);

            return true;
        }

        void ComputeCorners() {
            m_corners[0].X = m_corners[1].X = m_startX;
            m_corners[2].X = m_corners[3].X = m_endX;
            m_corners[0].Z = m_corners[3].Z = m_startY;
            m_corners[1].Z = m_corners[2].Z = m_endY;
            m_corners[0].Y = m_corners[1].Y = m_corners[2].Y = m_corners[3].Y = m_minHeight;

            m_corners[4].X = m_corners[5].X = m_startX;
            m_corners[6].X = m_corners[7].X = m_endX;
            m_corners[4].Z = m_corners[7].Z = m_startY;
            m_corners[5].Z = m_corners[6].Z = m_endY;
            m_corners[4].Y = m_corners[5].Y = m_corners[6].Y = m_corners[7].Y = m_maxHeight;
        }

        //-------------------------------------------
        //  Attempt to cull this node out
        //  returns -1 = partially in viewing frustum
        //           0 = not in viewing frustum
        //           1 = totally in viewing frustum
        //-------------------------------------------
        int TryCull(Matrix viewproj) {
            int t1, t2, t3, t4;
            t1 = t2 = t3 = t4 = 0;

            int i;
            for (i = 0; i < 8; i++) {
                m_projCorners[i] = Vector4.Transform(m_corners[i], viewproj);
            }

            // Check each of the transformed corners of our bounding box
            // to see if it was transformed completely outside of 
            // the viewing frustrum, partially out or completely within.
            // w now represents the relative viewing frustrums extents
            // so, valid coords within viewing frustrum are
            //      0 <= z <= w && -w <= x <= w && -w <= y <= w
            //   or z >= 0 && z < w && x >= -w && x <= w && y >= -w && y <= w
            for (i = 0; i < 8; i++) {
                if (m_projCorners[i].Z < 0.0f) {
                    t1++;
                }
            }
            if (t1 >= 8) {
                return 0; // completely behind Frustrum?
            }

            for (i = 0; i < 8; i++) {
                if (m_projCorners[i].X > m_projCorners[i].W) {
                    t2++;
                }
            }
            if (t2 >= 8) {
                return 0;
            }

            for (i = 0; i < 8; i++) {
                if (m_projCorners[i].X < -m_projCorners[i].W) {
                    t3++;
                }
            }
            if (t3 >= 8) {
                return 0;
            }

            for (i = 0; i < 8; i++) {
                if (m_projCorners[i].Z > m_projCorners[i].W) {
                    t4++;
                }
            }
            
            if (t4 >= 8) {
                return 0;
            }

            if ((t1 + t2 + t3 + t4) == 0) {
                return 1;
            }

            return -1; // partially
        }

        //-------------------------------------------
        //  Draw this node if at least partially visible
        //-------------------------------------------
        public void Draw(GraphicsDevice gd, Matrix viewproj, bool bCheckCull) {
            nodeDrawn = false; // used by DrawModels;

            int cull = 1; // if cull == 0 then completely out of frustrum, 1 = in, -1 = partially
            bCheckCull = false; // TODO: culling is messed up with quaternion camera...
            if (bCheckCull) {
                cull = TryCull(viewproj);
                if (cull == 0) // completely out?
                    return;
            }

            nodeDrawn = true;

            if (m_vertexBuffer != null) {
                gd.SetVertexBuffer(m_vertexBuffer);
                gd.Indices = m_indexBuffer;

                gd.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0,  // vertex buffer offset to add to each element of the index buffer
                    0,  // minimum vertex index
                    m_numVerts, // number of vertices
                    0,  // first index element to read
                    m_numTris   // number of primitives to draw
                );
            }
            for (int i = 0; i < 4; i++) {
                if (children[i] != null)
                    children[i].Draw(gd, viewproj, cull < 0);
            }
        }

        // Models
        public void DrawModels(Matrix view, Matrix proj) {
            // if we did _not_ draw the terrain node
            // then we don't need to draw any models 
            // that sit on it. (TODO: this is true except
            // for a special case which I'll handle later).
            if (!nodeDrawn) {
                return;
            }

            for (int i = 0; i < 4; i++) {
                if (children[i] != null) {
                    children[i].DrawModels(view, proj);
                }
            }

            if (_models == null) {
                return;
            }

            if (_models.Count < 1) {
                return;
            }

            foreach (QuadTreeModelInfo minfo in _models) {
                // let's copy the varibles out of the QuadTreeModelInfo
                // structure just for code clarity
                Model m = minfo._model;
                Vector3 modelPosition = minfo._position;
                float modelScale = minfo._scale;
                Vector3 modelRotation = minfo._rotation;

                Matrix[] transforms = new Matrix[m.Bones.Count];
                m.CopyAbsoluteBoneTransformsTo(transforms);

                //Draw the model, a model can have multiple meshes, so loop
                foreach (ModelMesh mesh in m.Meshes) {
                    //This is where the mesh orientation is set, as well as our camera and projection
                    foreach (BasicEffect effect in mesh.Effects) {
                        //effect.EnableDefaultLighting();
                        effect.World = transforms[mesh.ParentBone.Index] * Matrix.CreateScale(modelScale)
                            * Matrix.CreateRotationX(modelRotation.X)
                            * Matrix.CreateRotationZ(modelRotation.Z)
                            * Matrix.CreateRotationY(modelRotation.Y)
                            * Matrix.CreateTranslation(modelPosition);
                        effect.View = view;
                        effect.Projection = proj;
                    }
                    //Draw the mesh, will use the effects set above.
                    mesh.Draw();
                }
            }
        }

        internal bool AddModel(QuadTreeModelInfo modelInfo) {
            if (_models == null) {
                return false;
            }

            if (modelInfo._position.X >= m_startX && modelInfo._position.X < m_endX) {
                if (modelInfo._position.Z >= m_startY && modelInfo._position.Z < m_endY) {
                    // it belongs to this instance or a child

                    if (children != null)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (children[i] != null)
                            {
                                if (children[i].AddModel(modelInfo))
                                    return true;
                            }
                        }
                    }
                    // up to us to store it
                    if (modelInfo._position.Y == 0) {
                        modelInfo._position.Y = GetHeightAt(modelInfo._position.X, modelInfo._position.Z);
                    }
                    modelInfo._node = this;
                    _models.Add(modelInfo);
                    return true;
                }
            }
            return false;
        }

        internal bool RemoveModel(QuadTreeModelInfo modelInfo) {
            if (_models.Contains(modelInfo)) {
                _models.Remove(modelInfo);
                return true;
            }
            return false;
        }

        // Components
        public void DrawComponents(GameTime gameTime, Matrix view, Matrix proj) {
            // if we did _not_ draw the terrain node
            // then we don't need to draw any models 
            // that sit on it. (TODO: this is true except
            // for a special case which I'll handle later).
            if (!nodeDrawn) {
                return;
            }

            if (_gameComponents != null) {
                foreach (QuadTreeGameComponent component in _gameComponents)
                {
                    component.Draw(gameTime, view, proj);
                }
            }

            if (children != null) {
                for (int i = 0; i < 4; i++) {
                    if (children[i] != null) {
                        children[i].DrawComponents(gameTime, view, proj);
                    }
                }
            }
        }

        internal bool AddComponent(QuadTreeGameComponent component) {
            if (_gameComponents == null) {
                return false;
            }

            if (!IsInThisNode(component.Position)) {
                return false;
            }

            if (children != null) {
                for (int i = 0; i < 4; i++) {
                    if (children[i] != null) {
                        if (children[i].AddComponent(component))
                            return true;
                    }
                }
            }

            // up to us to store it
            if (component.position.Y == 0) {
                component.position.Y = GetHeightAt(component.Position.X, component.Position.Z);
            }
            component.qNode = this;
            _gameComponents.Add(component);
            return true;
        }

        internal bool RemoveComponent(QuadTreeGameComponent component) {
            if (_gameComponents.Contains(component))
            {
                _gameComponents.Remove(component);
                component.qNode = null;
                return true;
            }
            return false;
        }

        internal bool IsInThisNode(Vector3 position) {
            return IsInThisNode(position.X, position.Z);
        }
        
        internal bool IsInThisNode(float x, float z) {
            if (x >= m_startX && x < m_endX)
            {
                if (z >= m_startY && z < m_endY)
                {
                    return true;
                }
            }
            return false;
        }

        //-------------------------------------------
        //  Get the terrain height at location x, y
        //  returns OutOfRange if x,y is off terrain
        //-------------------------------------------
        public float GetHeightAt(float x, float y) {
            if (x < m_startX || x >= m_endX)
                return OutOfRange;
            if (y < m_startY || y >= m_endY)
                return OutOfRange;

            if (m_heightData == null)
            {
                for (int i = 0; i < 4; i++)
                {
                    float ret;
                    if (children[i] != null)
                    {
                        ret = children[i].GetHeightAt(x, y);
                        if (ret != OutOfRange)
                            return ret;
                    }
                }
                return OutOfRange;
            }

            // up to this instance....

            // find the cell
            float thisX = x - m_startX;
            float thisY = y - m_startY;
            int mapX = (int)(thisX / m_cellSize);
            int mapY = (int)(thisY / m_cellSize);
            if (mapX < 0 || mapX >= m_width)
                throw new Exception("QuadtreeNode:GetHeightAt(x,y) x out of range.");
            if (mapY < 0 || mapY >= m_height)
                throw new Exception("QuadtreeNode:GetHeightAt(x,y) y out of range.");

            // compute distance into cell
            float deltaX = (thisX % m_cellSize) / m_cellSize;
            float deltaY = (thisY % m_cellSize) / m_cellSize;

            // find which tri in the cell to use
            float hx1, hx2;
            float hy1, hy2;
            if (deltaY < deltaX) // in the upper left tri?
            {
                hx1 = m_heightData[mapY, mapX];
                hx2 = m_heightData[mapY, mapX + 1];
                hy1 = m_heightData[mapY, mapX];
                hy2 = m_heightData[mapY + 1, mapX];
            }
            else
            {
                hx1 = m_heightData[mapY + 1, mapX];
                hx2 = m_heightData[mapY + 1, mapX + 1];
                hy1 = m_heightData[mapY, mapX + 1];
                hy2 = m_heightData[mapY + 1, mapX + 1];
            }

            // find height at that spot within the cell
            float height = ((hx1 + ((hx2 - hx1) * deltaX)) + (hy1 + ((hy2 - hy1) * deltaY))) / 2.0f;
            return height;
        }
    }
}
