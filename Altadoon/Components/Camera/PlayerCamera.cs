using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altadoon.Components.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Altadoon.Components.Camera
{
    public class PlayerCamera
    {
        #region Fields
        private const float DEFAULT_FOVX = 80.0f;
        private const float DEFAULT_ZFAR = 1000.0f;
        private const float DEFAULT_ZNEAR = 1.0f;

        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;
        private float _fovX;
        private float _zNear;
        private float _zFar;

        private Vector3 _eye;
        private Vector3 _target;
        private Vector3 _targetYAxis;
        private Vector3 _xAxis;
        private Vector3 _yAxis;
        private Vector3 _zAxis;
        private Vector3 _viewDir;
        private Quaternion _orientation;

        private float _pitch;
        private float _yaw;
        private float _offsetDistance;
        #endregion

        #region Public Methods

        public PlayerCamera()
        {
            _offsetDistance = 0.0f;
            _yaw = 0.0f;
            _pitch = 0.0f;

            _fovX = DEFAULT_FOVX;
            _zNear = DEFAULT_ZNEAR;
            _zFar = DEFAULT_ZFAR;

            _eye = Vector3.Zero;
            _target = Vector3.Zero;
            _targetYAxis = Vector3.Up;

            _xAxis = Vector3.UnitX;
            _yAxis = Vector3.UnitY;
            _zAxis = Vector3.UnitZ;
            _viewDir = Vector3.Forward;

            _viewMatrix = Matrix.Identity;
            _projectionMatrix = Matrix.Identity;
            _orientation = Quaternion.Identity;
        }

        public void LookAtTarget(Vector3 target)
        {
            _target = target;
        }

        /// <summary>
        /// Builds a look at style viewing matrix.
        /// </summary>
        /// <param name="eye">The camera position.</param>
        /// <param name="target">The target position to look at.</param>
        /// <param name="up">The up direction.</param>
        public void LookAt(Vector3 eye, Vector3 target, Vector3 up)
        {
            _eye = eye;
            _target = target;
            
            // camera forward
            _zAxis = eye - target;
            _zAxis.Normalize();
            Vector3.Negate(ref _zAxis, out _viewDir);

            // camera right
            Vector3.Cross(ref up, ref _zAxis, out _xAxis);
            _xAxis.Normalize();

            // camera up
            Vector3.Cross(ref _zAxis, ref _xAxis, out _yAxis);
            _yAxis.Normalize();
            _xAxis.Normalize();

            _viewMatrix.M11 = _xAxis.X;
            _viewMatrix.M21 = _xAxis.Y;
            _viewMatrix.M31 = _xAxis.Z;
            Vector3.Dot(ref _xAxis, ref eye, out _viewMatrix.M41);
            _viewMatrix.M41 = -_viewMatrix.M41;

            _viewMatrix.M12 = _yAxis.X;
            _viewMatrix.M22 = _yAxis.Y;
            _viewMatrix.M32 = _yAxis.Z;
            Vector3.Dot(ref _yAxis, ref eye, out _viewMatrix.M42);
            _viewMatrix.M42 = -_viewMatrix.M42;

            _viewMatrix.M13 = _zAxis.X;
            _viewMatrix.M23 = _zAxis.Y;
            _viewMatrix.M33 = _zAxis.Z;
            Vector3.Dot(ref _zAxis, ref eye, out _viewMatrix.M43);
            _viewMatrix.M43 = -_viewMatrix.M43;

            _viewMatrix.M14 = 0.0f;
            _viewMatrix.M24 = 0.0f;
            _viewMatrix.M34 = 0.0f;
            _viewMatrix.M44 = 1.0f;

            _targetYAxis = up;

            Quaternion.CreateFromRotationMatrix(ref _viewMatrix, out _orientation);

            Vector3 offset = target - eye;
            _offsetDistance = offset.Length();
        }

        /// <summary>
        /// Builds a perspective projection matrix based on a horizontal field
        /// of view.
        /// </summary>
        /// <param name="fovX">Horizontal field of view in degrees.</param>
        /// <param name="aspect">The viewport's aspect ratio.</param>
        /// <param name="zNear">The distance to the near clip plane.</param>
        /// <param name="zFar">The distance to the far clip plane.</param>
        public void Perspective(float fovX, float aspect, float zNear, float zFar)
        {
            _fovX = fovX;
            _zNear = zNear;
            _zFar = zFar;

            float aspectInverse = 1.0f/aspect;
            float e = 1.0f/(float) Math.Tan(MathHelper.ToRadians(_fovX)/2.0f);
            float fovY = 2.0f*(float) Math.Atan(aspectInverse/e);
            float xScale = 1.0f/(float) Math.Tan(0.5f*fovY);
            float yScale = xScale/aspectInverse;

            _projectionMatrix.M11 = xScale;
            _projectionMatrix.M12 = 0.0f;
            _projectionMatrix.M13 = 0.0f;
            _projectionMatrix.M14 = 0.0f;

            _projectionMatrix.M21 = 0.0f;
            _projectionMatrix.M22 = yScale;
            _projectionMatrix.M23 = 0.0f;
            _projectionMatrix.M24 = 0.0f;

            _projectionMatrix.M31 = 0.0f;
            _projectionMatrix.M32 = 0.0f;
            _projectionMatrix.M33 = (_zFar + _zNear) / (_zNear - _zFar);
            _projectionMatrix.M34 = -1.0f;

            _projectionMatrix.M41 = 0.0f;
            _projectionMatrix.M42 = 0.0f;
            _projectionMatrix.M43 = (2.0f * _zFar * _zNear) / (_zNear - _zFar);
            _projectionMatrix.M44 = 0.0f;
        }

        /// <summary>
        /// This method must be called once every frame to update the internal
        /// state of the camera.
        /// </summary>
        /// <param name="gameTime">The elapsed game time.</param>
        public void Update(GameTime gameTime)
        {
            float elapsedTimeSec = (float)gameTime.ElapsedGameTime.TotalSeconds;

            UpdateOrientation(elapsedTimeSec);
            UpdateViewMatrix();
        }

        /// <summary>
        /// Rotates the camera. Positive angles specify counter clockwise
        /// rotations when looking down the axis of rotation towards the
        /// origin.
        /// </summary>
        /// <param name="yawDegrees">Y axis rotation in degrees.</param>
        /// <param name="pitchDegrees">X axis rotation in degrees.</param>
        public void Rotate(float yawDegrees, float pitchDegrees)
        {
            _yaw = -yawDegrees;
            _pitch = -pitchDegrees;
        }

        /// <summary>
        /// Zooms the camera. This method functions differently depending on
        /// the camera's current CameraBehavior. When the camera is orbiting this
        /// method will move the camera closer to or further away from the
        /// orbit target. For the other camera behaviors this method will
        /// change the camera's horizontal field of view.
        /// </summary>
        ///
        /// <param name="zoom">
        /// When orbiting this parameter is how far to move the camera.
        /// For the other behaviors this parameter is the new horizontal
        /// field of view.
        /// </param>
        /// 
        /// <param name="minZoom">
        /// When orbiting this parameter is the min allowed zoom distance to
        /// the orbit target. For the other behaviors this parameter is the
        /// min allowed horizontal field of view.
        /// </param>
        /// 
        /// <param name="maxZoom">
        /// When orbiting this parameter is the max allowed zoom distance to
        /// the orbit target. For the other behaviors this parameter is the max
        /// allowed horizontal field of view.
        /// </param>
        public void Zoom(float zoom, float minZoom, float maxZoom)
        {
            //TODO:
        }

        #endregion

        #region Private Methods
        private void UpdateOrientation(float elapsedTimeSec)
        {
            _yaw *= elapsedTimeSec;
            _pitch *= elapsedTimeSec;

            float heading = MathHelper.ToRadians(_yaw);
            float pitch = MathHelper.ToRadians(_pitch);
            Quaternion rotation = Quaternion.Identity;

            if (heading != 0.0f)
            {
                Quaternion.CreateFromAxisAngle(ref _targetYAxis, heading, out rotation);
                Quaternion.Concatenate(ref rotation, ref _orientation, out _orientation);
            }

            if (pitch != 0.0f)
            {
                Vector3 worldXAxis = Vector3.UnitX;
                Quaternion.CreateFromAxisAngle(ref worldXAxis, pitch, out rotation);
                Quaternion.Concatenate(ref _orientation, ref rotation, out _orientation);
            }
        }

        private void UpdateViewMatrix()
        {
            Matrix.CreateFromQuaternion(ref _orientation, out _viewMatrix);

            _xAxis.X = _viewMatrix.M11;
            _xAxis.Y = _viewMatrix.M21;
            _xAxis.Z = _viewMatrix.M31;

            _yAxis.X = _viewMatrix.M12;
            _yAxis.Y = _viewMatrix.M22;
            _yAxis.Z = _viewMatrix.M32;

            _zAxis.X = _viewMatrix.M13;
            _zAxis.Y = _viewMatrix.M23;
            _zAxis.Z = _viewMatrix.M33;

            _eye = _target + _zAxis * _offsetDistance;

            _viewMatrix.M41 = -Vector3.Dot(_xAxis, _eye);
            _viewMatrix.M42 = -Vector3.Dot(_yAxis, _eye);
            _viewMatrix.M43 = -Vector3.Dot(_zAxis, _eye);

            Vector3.Negate(ref _zAxis, out _viewDir);
        }
        #endregion

        #region Properties
        public float OffsetDistance
        {
            get { return _offsetDistance; }
            set { _offsetDistance = value; }
        }

        public Vector3 Position
        {
            get { return _eye; }
        }

        public Vector3 Target
        {
            get { return _target; }
        }

        public Vector3 TargetYAxis
        {
            get { return _targetYAxis; }
        }

        public Vector3 XAxis
        {
            get { return _xAxis; }
        }

        public Vector3 YAxis
        {
            get { return _yAxis; }
            set { _yAxis = value; }
        }

        public Vector3 ZAxis
        {
            get { return _zAxis; }
        }

        public Vector3 ViewDirection
        {
            get { return _viewDir; }
        }

        public Matrix ViewMatrix
        {
            get { return _viewMatrix; }
        }

        public Matrix ViewProjectionMatrix
        {
            get { return _viewMatrix * _projectionMatrix; }
        }

        public Matrix ProjectionMatrix
        {
            get { return _projectionMatrix; }
        }

        public Quaternion Orientation
        {
            get { return _orientation; }
        }

        #endregion
    }
}
