using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    /// <summary>
    /// Translates Unity's frame-based input API into coordinator commands and tick controls.
    /// All gameplay mutations still pass through <see cref="WavePrototypeApp"/> and
    /// <see cref="WaveSimulation"/>.
    /// </summary>
    internal sealed class PrototypeInputController
    {
        private readonly WavePrototypeApp app;
        private readonly PrototypeCameraController camera;

        public PrototypeInputController(WavePrototypeApp app, PrototypeCameraController camera)
        {
            this.app = app;
            this.camera = camera;
        }

        public void PollCommands()
        {
            if (Input.GetKeyDown(KeyCode.P)) app.TogglePause();
            if (Input.GetKeyDown(KeyCode.Period)) app.PauseAndStep();
            if (Input.GetKeyDown(KeyCode.R)) app.ResetSimulation();
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Vector2 cursor = camera.ScreenToWorld(Input.mousePosition);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    app.SpawnLocalBreakerBurst(cursor);
                else
                    app.SpawnSwellFront(cursor);
            }
            if (Input.GetKeyDown(KeyCode.B)) app.SpawnBoat(camera.ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.C)) app.SpawnFloatingObject(
                FloatingObjectKind.Cargo, camera.ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.X)) app.SpawnFloatingObject(
                FloatingObjectKind.Wreckage, camera.ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.T)) app.RelocateTarget();
            if (Input.GetKeyDown(KeyCode.V)) app.ToggleTarget();
            if (Input.GetKeyDown(KeyCode.K)) app.ToggleTargetBearing();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) app.AdjustTargetRadius(-1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) app.AdjustTargetRadius(1f);
            if (Input.GetKeyDown(KeyCode.M)) camera.ToggleMapView();
            if (Input.GetKeyDown(KeyCode.F3)) app.ToggleDebugOverlay();
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1)) app.ToggleHelp();
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }

        public BoatControl ReadPlayerControl()
        {
            float throttle = 0f;
            float steering = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) throttle -= 0.35f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steering += 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steering -= 1f;
            return new BoatControl(throttle, steering);
        }
    }
}
