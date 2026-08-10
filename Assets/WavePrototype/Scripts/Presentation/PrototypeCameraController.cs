using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    /// <summary>
    /// Owns camera creation, follow interpolation, map framing, zoom, and viewport clamping.
    /// It observes presentation snapshots and never writes to simulation state.
    /// </summary>
    internal sealed class PrototypeCameraController
    {
        private readonly SimulationConfigSnapshot config;
        private float followZoom = 18f;
        private Vector3 cameraVelocity;
        private Vector2 smoothedLookAhead;
        private Vector2 lookAheadVelocity;

        public Camera Camera { get; }
        public bool MapView { get; private set; }

        public PrototypeCameraController(Transform parent, SimulationConfigSnapshot config,
            Vector2 initialPosition)
        {
            this.config = config;
            GameObject cameraObject = new GameObject("Simulation Camera");
            cameraObject.transform.SetParent(parent, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = followZoom;
            Camera.transform.position = new Vector3(initialPosition.x, initialPosition.y, -10f);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.012f, 0.045f, 0.072f);
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = 50f;
        }

        public void ApplyScroll(float scroll)
        {
            if (MapView || Mathf.Abs(scroll) <= 0.01f) return;
            followZoom = Mathf.Clamp(followZoom * (1f - scroll * 0.08f), 10.5f, 27f);
        }

        public void ToggleMapView() => MapView = !MapView;

        public void SetMapView(bool enabled) => MapView = enabled;

        public void Reset(Vector2 position)
        {
            MapView = false;
            smoothedLookAhead = Vector2.zero;
            lookAheadVelocity = Vector2.zero;
            cameraVelocity = Vector3.zero;
            Camera.transform.position = new Vector3(position.x, position.y, -10f);
        }

        public void UpdateFollow(BoatData player, float deltaTime)
        {
            Vector3 target;
            float targetZoom;
            if (MapView)
            {
                target = new Vector3(0f, 0f, -10f);
                targetZoom = GetMapViewSize();
            }
            else
            {
                Vector2 desiredLookAhead = Vector2.ClampMagnitude(player.Velocity * 0.82f, 9f);
                smoothedLookAhead = Vector2.SmoothDamp(smoothedLookAhead, desiredLookAhead,
                    ref lookAheadVelocity, 0.22f, 32f, deltaTime);
                target = new Vector3(player.Position.x + smoothedLookAhead.x,
                    player.Position.y + smoothedLookAhead.y, -10f);
                targetZoom = followZoom;
            }

            // Clamp against the larger zoom during transitions so a still-wide viewport
            // cannot briefly leave the playable sea.
            target = ConstrainTarget(target, Mathf.Max(targetZoom, Camera.orthographicSize));
            Camera.transform.position = Vector3.SmoothDamp(Camera.transform.position, target,
                ref cameraVelocity, MapView ? 0.16f : 0.28f, 1000f, deltaTime);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, targetZoom,
                1f - Mathf.Exp(-7f * deltaTime));
        }

        public Vector2 ScreenToWorld(Vector3 screen)
        {
            Vector3 world = Camera.ScreenToWorldPoint(screen);
            return new Vector2(world.x, world.y);
        }

        public float GetMapViewSize()
        {
            Vector2 half = config.WorldHalfExtents;
            float aspect = Mathf.Max(0.1f, Camera.aspect);
            return Mathf.Max(half.y + 4f, (half.x + 4f) / aspect);
        }

        private Vector3 ConstrainTarget(Vector3 target, float orthographicSize)
        {
            Vector2 half = config.WorldHalfExtents;
            float viewHalfWidth = orthographicSize * Mathf.Max(0.1f, Camera.aspect);
            float maximumX = Mathf.Max(0f, half.x - viewHalfWidth);
            float maximumY = Mathf.Max(0f, half.y - orthographicSize);
            target.x = Mathf.Clamp(target.x, -maximumX, maximumX);
            target.y = Mathf.Clamp(target.y, -maximumY, maximumY);
            return target;
        }
    }
}
