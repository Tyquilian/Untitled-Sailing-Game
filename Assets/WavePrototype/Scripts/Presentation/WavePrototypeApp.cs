using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class WavePrototypeApp : MonoBehaviour
    {
        private const int InitialSeed = 1847;
        private WaveSimulation simulation;
        private PrototypeCameraController cameraController;
        private PrototypeInputController inputController;
        private readonly PrototypeDiagnostics diagnostics = new PrototypeDiagnostics();
        private readonly PrototypeSnapshotBuffer snapshots = new PrototypeSnapshotBuffer();
        private PrototypeOceanRenderer oceanRenderer;
        private float accumulator;
        private bool paused;
        private bool debugOverlay;
        private bool showHelp = true;
        private bool showTargetBearing = true;
        private bool automatedTestDrive;
        private int selectedSeed = InitialSeed;
        private GUIStyle titleStyle, labelStyle, smallStyle, valueStyle, buttonStyle, boxStyle;

        public WaveSimulation Simulation => simulation;
        private Camera worldCamera => cameraController.Camera;
        private bool mapView => cameraController.MapView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<WavePrototypeApp>() != null) return;
            GameObject root = new GameObject("Wave Prototype");
            DontDestroyOnLoad(root);
            root.AddComponent<WavePrototypeApp>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            simulation = new WaveSimulation(selectedSeed);
            cameraController = new PrototypeCameraController(transform, simulation.Config,
                simulation.Boats[0].Position);
            inputController = new PrototypeInputController(this, cameraController);
            snapshots.Initialize(simulation);
            oceanRenderer = new PrototypeOceanRenderer(transform, simulation, snapshots);
            oceanRenderer.RebuildStatic();
            oceanRenderer.BuildDynamic(RenderInterpolationAlpha, debugOverlay, showTargetBearing);
            PushLog("Arcade sailing sandbox initialized");
            PushLog($"{simulation.Waves.Count} fronts / {simulation.ActiveWaveSegmentCount} active segments");
        }

        private void Start()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            if (Array.Exists(arguments, value => string.Equals(value, "-smoketest", StringComparison.OrdinalIgnoreCase)))
            {
                for (int i = 0; i < 120; i++)
                {
                    simulation.SetPlayerControl(1f, Mathf.Sin(i * 0.08f) * 0.45f);
                    simulation.Step();
                }
                Debug.Log($"[WAVE-SMOKE] PASS batch=14 ticks={simulation.Tick} waves={simulation.Waves.Count} segments={simulation.ActiveWaveSegmentCount}/{simulation.TotalWaveSegmentCount} systems={simulation.SwellSystems.Count} sources={simulation.ActiveWaveSourceCount}/{simulation.WaveSources.Count} objects={simulation.FloatingObjects.Count} salvage={simulation.CollectedSalvageCount}/{simulation.CollectedSalvageValue:0} rocks={simulation.Environment.Rocks.Count} visits={simulation.Target.VisitCount} profile={simulation.Boats[0].Profile} hash={simulation.CalculateStateHash():X16}");
                Application.Quit(0);
            }
            else if (Array.Exists(arguments, value => string.Equals(value, "-capturepreview", StringComparison.OrdinalIgnoreCase)))
            {
                StartCoroutine(CapturePreview());
            }
            else if (Array.Exists(arguments, value => string.Equals(value, "-frametest", StringComparison.OrdinalIgnoreCase)))
            {
                StartCoroutine(RunFrameTest());
            }
        }

        private IEnumerator CapturePreview()
        {
            automatedTestDrive = true;
            for (int i = 0; i < 90; i++) yield return null;
            string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            CaptureCamera(Path.Combine(buildDirectory, "Batch14-preview.png"));
            simulation.SetBoatProfile(simulation.PlayerBoatId, VesselProfileId.HeavyCutter);
            InitializeSnapshots();
            yield return null;
            CaptureCamera(Path.Combine(buildDirectory, "Batch14-heavy-preview.png"));
            Vector3 previousPosition = worldCamera.transform.position;
            float previousSize = worldCamera.orthographicSize;
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
            worldCamera.orthographicSize = cameraController.GetMapViewSize();
            CaptureCamera(Path.Combine(buildDirectory, "Batch14-map-preview.png"));
            worldCamera.transform.position = previousPosition;
            worldCamera.orthographicSize = previousSize;
            Debug.Log("[WAVE-PREVIEW] Captured Batch 14 skiff, heavy, and map previews in " + buildDirectory);
            Application.Quit(0);
        }

        private IEnumerator RunFrameTest()
        {
            const int warmupFrames = 180;
            const int measuredFrames = 600;
            var samples = new float[measuredFrames];
            automatedTestDrive = true;
            for (int i = 0; i < warmupFrames; i++) yield return null;

            int gen0Before = GC.CollectionCount(0);
            long heapBefore = GC.GetTotalMemory(false);
            float total = 0f;
            BoatData renderedPlayer = GetInterpolatedPlayer(RenderInterpolationAlpha);
            Vector2 previousRenderedPosition = renderedPlayer.Position;
            float maximumRenderedStep = 0f;
            int repeatedMovingFrames = 0;
            for (int i = 0; i < measuredFrames; i++)
            {
                yield return null;
                float milliseconds = Time.unscaledDeltaTime * 1000f;
                samples[i] = milliseconds;
                total += milliseconds;
                renderedPlayer = GetInterpolatedPlayer(RenderInterpolationAlpha);
                float renderedStep = Vector2.Distance(previousRenderedPosition, renderedPlayer.Position);
                maximumRenderedStep = Mathf.Max(maximumRenderedStep, renderedStep);
                if (renderedPlayer.Velocity.magnitude > 3f && renderedStep < 0.0001f)
                    repeatedMovingFrames++;
                previousRenderedPosition = renderedPlayer.Position;
            }

            long heapAfter = GC.GetTotalMemory(false);
            int gen0Collections = GC.CollectionCount(0) - gen0Before;
            Array.Sort(samples);
            float average = total / measuredFrames;
            float p99 = samples[Mathf.Clamp(Mathf.CeilToInt(measuredFrames * 0.99f) - 1, 0, measuredFrames - 1)];
            float maximum = samples[measuredFrames - 1];
            Debug.Log($"[WAVE-FRAME] batch=14 frames={measuredFrames} avgMs={average:0.00} p99Ms={p99:0.00} maxMs={maximum:0.00} gen0={gen0Collections} heapDelta={heapAfter - heapBefore} movingRepeats={repeatedMovingFrames} maxBoatStep={maximumRenderedStep:0.000} finalSpeed={renderedPlayer.Velocity.magnitude:0.00} staticVerts={oceanRenderer.StaticVertexCount} dynamicVerts={oceanRenderer.DynamicVertexCount}");
            Application.Quit(0);
        }

        private void CaptureCamera(string path)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = worldCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            worldCamera.targetTexture = target;
            worldCamera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(path, capture.EncodeToPNG());
            worldCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Destroy(capture);
            target.Release();
            Destroy(target);
        }

        private void Update()
        {
            inputController.PollCommands();
            FeedPlayerControl();
            if (!paused)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                int guard = 0;
                while (accumulator >= simulation.Config.FixedDeltaTime && guard++ < 6)
                {
                    accumulator -= simulation.Config.FixedDeltaTime;
                    StepOnce();
                }
            }
            cameraController.ApplyScroll(Input.mouseScrollDelta.y);
            oceanRenderer.BuildDynamic(RenderInterpolationAlpha, debugOverlay, showTargetBearing);
            diagnostics.RecordFrame(Time.unscaledDeltaTime * 1000f);
        }

        private void LateUpdate()
        {
            cameraController.UpdateFollow(GetInterpolatedPlayer(RenderInterpolationAlpha),
                Time.unscaledDeltaTime);
        }

        internal void TogglePause()
        {
            paused = !paused;
            accumulator = 0f;
            // Collapse the interval so pausing or resuming never displays an older tick.
            InitializeSnapshots();
        }

        internal void PauseAndStep()
        {
            paused = true;
            StepOnce();
        }

        internal void ToggleDebugOverlay() => debugOverlay = !debugOverlay;

        internal void ToggleHelp() => showHelp = !showHelp;

        private void FeedPlayerControl()
        {
            if (automatedTestDrive)
            {
                float automatedSteering = Mathf.Sin((float)simulation.Tick * 0.045f) * 0.16f;
                simulation.SetPlayerControl(1f, automatedSteering);
                return;
            }

            BoatControl control = inputController.ReadPlayerControl();
            simulation.SetPlayerControl(control.Throttle, control.Steering);
        }

        private Vector2 ScreenToWorld(Vector3 screen)
        {
            return cameraController.ScreenToWorld(screen);
        }

        private void StepOnce()
        {
            SwapSnapshotBuffers();
            simulation.Step();
            CaptureCurrentSnapshots();
            for (int i = 0; i < simulation.Events.Count; i++)
            {
                SimulationEvent e = simulation.Events[i];
                if (e.BoatId != simulation.PlayerBoatId) continue;
                if (e.Type == SimulationEventType.TargetVisited)
                {
                    PushLog($"TARGET VISITED  #{Mathf.RoundToInt(e.Magnitude)}");
                    continue;
                }
                if (e.Type == SimulationEventType.FloatingObjectCollected)
                {
                    PushLog($"SALVAGE RECOVERED  +{e.Magnitude:0}");
                    continue;
                }
                if (e.Type == SimulationEventType.BoatHitWreckage && e.Magnitude > 0.8f)
                {
                    PushLog($"WRECKAGE IMPACT  {e.Magnitude:0.0}");
                    continue;
                }
                if (e.Type == SimulationEventType.BoatHitRock)
                    PushLog($"ROCK IMPACT  −{e.Magnitude:0.0} hull");
                else if (e.Type == SimulationEventType.BoatGrounded)
                    PushLog($"GROUNDED  −{e.Magnitude:0.0} hull");
                else if (e.Type == SimulationEventType.BoatDamaged && e.Magnitude > 0.03f)
                    PushLog($"Breaking water  −{e.Magnitude:0.00} hull");
            }
        }

        internal void ResetSimulation()
        {
            simulation.Reset(selectedSeed);
            accumulator = 0f;
            diagnostics.ClearLog();
            InvalidateCachedHash();
            InitializeSnapshots();
            oceanRenderer.RebuildStatic();
            Vector2 start = simulation.Boats[0].Position;
            cameraController.Reset(start);
            PushLog("Reset to deterministic seed " + selectedSeed);
        }

        internal void SpawnSwellFront(Vector2 position)
        {
            if (!simulation.SpawnSwellFront(position, 2.65f))
            {
                PushLog("No active swell system is available");
                return;
            }
            RefreshSnapshotsAfterExternalMutation();
            PushLog("Full segmented swell front spawned");
        }

        internal void SpawnLocalBreakerBurst(Vector2 position)
        {
            Vector2 direction = simulation.Config.WindDirection.normalized;
            Vector2 crest = new Vector2(-direction.y, direction.x);
            for (int i = -3; i <= 3; i++)
                simulation.SpawnWave(position + crest * i * 2.4f, direction, 2.65f - Mathf.Abs(i) * 0.12f);
            RefreshSnapshotsAfterExternalMutation();
            PushLog("Local seven-packet breaker burst spawned");
        }

        internal void SpawnBoat(Vector2 position, VesselProfileId profileId)
        {
            simulation.AddBoat(position, 0f, profileId);
            RefreshSnapshotsAfterExternalMutation();
            PushLog(VesselProfiles.GetLabel(profileId) + " test boat spawned");
        }

        internal void TogglePlayerVesselProfile()
        {
            BoatData player = simulation.Boats[0];
            VesselProfileId next = player.Profile == VesselProfileId.ArcadeSkiff
                ? VesselProfileId.HeavyCutter : VesselProfileId.ArcadeSkiff;
            if (!simulation.SetBoatProfile(player.Id, next)) return;
            RefreshSnapshotsAfterExternalMutation();
            cameraController.Reset(simulation.Boats[0].Position);
            PushLog("DEBUG HULL: " + VesselProfiles.GetLabel(next));
        }

        internal void SpawnFloatingObject(FloatingObjectKind kind, Vector2 position)
        {
            int id = simulation.SpawnFloatingObject(kind, position);
            if (id == 0)
            {
                PushLog("Floating object needs clear water");
                return;
            }
            InvalidateCachedHash();
            PushLog(kind == FloatingObjectKind.Cargo
                ? "Floating cargo spawned" : "Drifting wreckage spawned");
        }

        internal void RelocateTarget()
        {
            if (simulation.RelocateTarget())
            {
                InvalidateCachedHash();
                PushLog("Target relocated");
            }
            else PushLog("No safe target water found");
        }

        internal void ToggleTarget()
        {
            simulation.SetTargetEnabled(!simulation.Target.Enabled);
            InvalidateCachedHash();
            PushLog(simulation.Target.Enabled ? "Target enabled" : "Target hidden");
        }

        internal void ToggleTargetBearing()
        {
            showTargetBearing = !showTargetBearing;
            PushLog(showTargetBearing ? "Target bearing arrow enabled" : "Target bearing arrow hidden");
        }

        internal void AdjustTargetRadius(float delta)
        {
            simulation.SetTargetVisitRadius(simulation.Target.VisitRadius + delta);
            InvalidateCachedHash();
            PushLog($"Target radius {simulation.Target.VisitRadius:0} units");
        }

        private void ResetTargetCounter()
        {
            simulation.ResetTargetVisitCount();
            InvalidateCachedHash();
            PushLog("Target visit counter reset");
        }

        private void PushLog(string message)
        {
            diagnostics.PushLog(simulation?.SimulatedTime ?? 0f, message);
        }

        private float RenderInterpolationAlpha => paused
            ? 1f
            : Mathf.Clamp01(accumulator / Mathf.Max(0.0001f, simulation.Config.FixedDeltaTime));

        private void InitializeSnapshots()
        {
            snapshots.Initialize(simulation);
        }

        private void SwapSnapshotBuffers()
        {
            snapshots.BeginStep();
        }

        private void CaptureCurrentSnapshots()
        {
            snapshots.EndStep(simulation);
        }

        private void RefreshSnapshotsAfterExternalMutation()
        {
            snapshots.RefreshAfterExternalMutation(simulation);
            InvalidateCachedHash();
        }

        private BoatData GetInterpolatedPlayer(float alpha)
        {
            return snapshots.GetPlayer(simulation, alpha);
        }

        private void InvalidateCachedHash()
        {
            diagnostics.InvalidateHash();
        }

        private ulong GetCachedStateHash()
        {
            return diagnostics.GetStateHash(simulation, Time.unscaledTime);
        }

        private void InitStyles()
        {
            if (titleStyle != null) return;
            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
            boxStyle.normal.background = MakeTexture(new Color(0.012f, 0.03f, 0.045f, 0.92f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.68f, 0.94f, 1f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.86f, 0.94f, 0.95f) } };
            smallStyle = new GUIStyle(labelStyle) { fontSize = 12, normal = { textColor = new Color(0.62f, 0.76f, 0.79f) } };
            valueStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.27f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 30f };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        private void OnGUI()
        {
            InitStyles();
            BoatData player = simulation.Boats[0];
            VesselProfileDefinition vessel = simulation.Config.GetVesselProfile(player.Profile);
            float vesselCruiseSpeed = simulation.Config.BoatCruiseSpeed * vessel.CruiseSpeedScale;
            float vesselSurfSpeed = simulation.Config.BoatSurfSpeedCap * vessel.SurfSpeedScale;
            float depth = simulation.Environment.SampleDepth(player.Position);
            Vector2 waveForce = simulation.SampleAmbientWaveField(player.Position);
            float windEfficiency = simulation.GetWindEfficiency(player.Heading);
            float viewHalfHeight = worldCamera.orthographicSize;
            float viewHalfWidth = viewHalfHeight * worldCamera.aspect;
            float viewRadius = Mathf.Sqrt(viewHalfWidth * viewHalfWidth + viewHalfHeight * viewHalfHeight);
            WaveDensitySample density = simulation.SampleWaveDensity(player.Position, viewRadius);
            TargetMarkerData target = simulation.Target;
            float targetDistance = Vector2.Distance(player.Position, target.Position);
            int breaking = 0, foam = 0;
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveSegmentCollection segments = simulation.Waves[i].Segments;
                for (int segment = 0; segment < segments.Length; segment++)
                {
                    if (!segments[segment].Active) continue;
                    if (segments[segment].State == WaveState.Breaking) breaking++;
                    if (segments[segment].FoamEnergy >= simulation.Config.MinimumFoamEnergy) foam++;
                }
            }

            GUILayout.BeginArea(new Rect(16, 16, 380, debugOverlay ? 900 : 760), boxStyle);
            GUILayout.Label("TACTICAL SAILING // BATCH 14", titleStyle);
            GUILayout.Label("Vessel profiles and broad-hull wave laboratory", smallStyle);
            GUILayout.Space(9);
            GUILayout.Label(paused ? "PAUSED" : "UNDERWAY", valueStyle);
            GUILayout.Label($"Vessel       {VesselProfiles.GetLabel(player.Profile)}", valueStyle);
            GUILayout.Label($"Speed        {player.Velocity.magnitude,5:0.0} / {vesselSurfSpeed:0.0}", labelStyle);
            GUILayout.Label($"Cruise cap   {vesselCruiseSpeed,5:0.0}   mass {player.Mass:0.0}", smallStyle);
            GUILayout.Label($"Hull         {player.Health,5:0.0}%", labelStyle);
            GUILayout.Label($"Depth        {depth,5:0.0} m", labelStyle);
            GUILayout.Label($"Wind drive   {windEfficiency * 100f,5:0}%", labelStyle);
            GUILayout.Label($"Ambient sea  {waveForce.magnitude,5:0.0}", labelStyle);
            GUILayout.Label($"Position     {Format(player.Position)}", smallStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "▶ Resume" : "Ⅱ Pause", buttonStyle)) TogglePause();
            if (GUILayout.Button("Step ›", buttonStyle)) { paused = true; StepOnce(); }
            if (GUILayout.Button(mapView ? "Follow" : "Map", buttonStyle))
                cameraController.ToggleMapView();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", buttonStyle)) ResetSimulation();
            if (GUILayout.Button("Swell Front", buttonStyle)) SpawnSwellFront(player.Position - Vector2.right * 7f);
            if (GUILayout.Button("Local Burst", buttonStyle)) SpawnLocalBreakerBurst(player.Position - Vector2.right * 7f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Skiff", buttonStyle)) SpawnBoat(
                player.Position + Vector2.up * 5f, VesselProfileId.ArcadeSkiff);
            if (GUILayout.Button("Heavy", buttonStyle)) SpawnBoat(
                player.Position + Vector2.down * 6f, VesselProfileId.HeavyCutter);
            if (GUILayout.Button("Switch Hull", buttonStyle)) TogglePlayerVesselProfile();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cargo", buttonStyle)) SpawnFloatingObject(
                FloatingObjectKind.Cargo, player.Position + Vector2.up * 7f);
            if (GUILayout.Button("Wreckage", buttonStyle)) SpawnFloatingObject(
                FloatingObjectKind.Wreckage, player.Position + Vector2.down * 7f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Seed −", buttonStyle)) { selectedSeed--; ResetSimulation(); }
            if (GUILayout.Button("Seed +", buttonStyle)) { selectedSeed++; ResetSimulation(); }
            if (GUILayout.Button(debugOverlay ? "Debug On" : "Debug", buttonStyle)) debugOverlay = !debugOverlay;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(target.Enabled ? "Target On" : "Target Off", buttonStyle)) ToggleTarget();
            if (GUILayout.Button("Relocate", buttonStyle)) RelocateTarget();
            if (GUILayout.Button(showTargetBearing ? "Arrow On" : "Arrow Off", buttonStyle))
                ToggleTargetBearing();
            if (GUILayout.Button("Reset Visits", buttonStyle)) ResetTargetCounter();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Radius -", buttonStyle)) AdjustTargetRadius(-1f);
            GUILayout.Label($"{target.VisitRadius:0} units", valueStyle, GUILayout.Width(82f));
            if (GUILayout.Button("Radius +", buttonStyle)) AdjustTargetRadius(1f);
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            Vector2 worldHalf = simulation.Config.WorldHalfExtents;
            GUILayout.Label($"WORLD {worldHalf.x * 2f:0} x {worldHalf.y * 2f:0}   FRONTS {density.WorldCount}   SYSTEMS {simulation.SwellSystems.Count}", smallStyle);
            GUILayout.Label($"LOCAL {density.LocalCount}/{density.DesiredVisibleCount} reference", smallStyle);
            GUILayout.Label(target.Enabled
                ? $"TARGET {targetDistance:0} away   VISITS {target.VisitCount}   RADIUS {target.VisitRadius:0}"
                : $"TARGET OFF   VISITS {target.VisitCount}", smallStyle);
            GUILayout.Label($"FLOATING {simulation.FloatingObjects.Count}   SALVAGE {simulation.CollectedSalvageCount} / {simulation.CollectedSalvageValue:0} value", smallStyle);
            GUILayout.Label($"SEGMENTS {simulation.ActiveWaveSegmentCount}/{simulation.TotalWaveSegmentCount}   BREAK {breaking} / FOAM {foam}", smallStyle);
            GUILayout.Label($"SOURCES {simulation.ActiveWaveSourceCount}/{simulation.WaveSources.Count} active   ROCKS {simulation.Environment.Rocks.Count}", smallStyle);
            GUILayout.Label($"SEED {selectedSeed}   TICK {simulation.Tick:N0}", smallStyle);
            GUILayout.Label($"HASH   {GetCachedStateHash():X16}", smallStyle);
            if (debugOverlay)
            {
                GUILayout.Label($"FRAME  {diagnostics.AverageFrameMilliseconds:0.0} ms avg / {diagnostics.MaximumFrameMilliseconds:0.0} max   DYN {oceanRenderer.DynamicVertexCount:N0}v", smallStyle);
                for (int i = 0; i < simulation.WaveSources.Count; i++)
                {
                    WaveSourceData source = simulation.WaveSources[i];
                    if (!source.Enabled)
                    {
                        GUILayout.Label($"SRC {source.Id}  {WaveSimulation.GetWaveSourceLabel(source.Kind)}  DISABLED", smallStyle);
                        continue;
                    }
                    float wait = source.NextEmissionTick > simulation.Tick
                        ? (source.NextEmissionTick - simulation.Tick) * simulation.Config.FixedDeltaTime : 0f;
                    GUILayout.Label($"SRC {source.Id}  {WaveSimulation.GetWaveSourceLabel(source.Kind)}  {source.SpawnedPackets} phases  next {wait:0.0}s", smallStyle);
                }
                int displayedSystems = Mathf.Min(5, simulation.SwellSystems.Count);
                for (int i = 0; i < displayedSystems; i++)
                {
                    SwellSystemData system = simulation.SwellSystems[i];
                    GUILayout.Label($"STREAM {system.Id}  SRC {system.SourceId}  {system.ActivePacketCount}/{system.EmittedPacketCount} active/emitted  period {system.CalmGapSeconds:0.0}s", smallStyle);
                }
            }
            GUILayout.Space(7);
            GUILayout.Label("EVENT FEED", smallStyle);
            foreach (string entry in diagnostics.EventLog) GUILayout.Label(entry, smallStyle);
            GUILayout.EndArea();

            if (showHelp)
            {
                const float width = 280f;
                GUILayout.BeginArea(new Rect(Screen.width - width - 16, 16, width, 458), boxStyle);
                GUILayout.Label("CONTROLS", titleStyle);
                GUILayout.Label("W / ↑      Forward", labelStyle);
                GUILayout.Label("S / ↓       Brake / reverse", labelStyle);
                GUILayout.Label("A D / ← →  Steer", labelStyle);
                GUILayout.Space(5);
                GUILayout.Label("M            Full map / follow", labelStyle);
                GUILayout.Label("Wheel      Follow-camera zoom", labelStyle);
                GUILayout.Label("Q             Full swell front at cursor", labelStyle);
                GUILayout.Label("Shift + Q     Local breaker burst (debug)", labelStyle);
                GUILayout.Label("B             Skiff at cursor", labelStyle);
                GUILayout.Label("Shift + B     Heavy cutter at cursor", labelStyle);
                GUILayout.Label("Y             Switch player hull (debug)", labelStyle);
                GUILayout.Label("C / X        Cargo / wreckage at cursor", labelStyle);
                GUILayout.Label("T             Relocate target", labelStyle);
                GUILayout.Label("V             Toggle target", labelStyle);
                GUILayout.Label("K             Toggle target arrow", labelStyle);
                GUILayout.Label("[ / ]          Target radius", labelStyle);
                GUILayout.Label("P / .         Pause / single tick", labelStyle);
                GUILayout.Label("F3           Swell + energy overlay", labelStyle);
                GUILayout.Label("R             Reset same seed", labelStyle);
                GUILayout.Label("H / F1     Hide controls", smallStyle);
                GUILayout.EndArea();
            }

            if (debugOverlay) DrawWaveInspector();
            GUI.Label(new Rect(Screen.width - 375, Screen.height - 30, 360, 22),
                mapView ? "MAP VIEW  •  M TO RETURN TO BOAT" : "M: MAP   •   F3: SWELL / ENERGY DEBUG", smallStyle);
        }

        private void DrawWaveInspector()
        {
            Vector2 mouseWorld = ScreenToWorld(Input.mousePosition);
            int nearestWave = -1;
            int nearestSegment = -1;
            float nearestDistance = Mathf.Pow(worldCamera.orthographicSize * 0.07f, 2f);
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveSegmentCollection segments = simulation.Waves[i].Segments;
                for (int segment = 0; segment < segments.Length; segment++)
                {
                    if (!segments[segment].Active) continue;
                    float distance = (segments[segment].Position - mouseWorld).sqrMagnitude;
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearestWave = i;
                    nearestSegment = segment;
                }
            }
            if (nearestWave < 0) return;
            WaveData wave = simulation.Waves[nearestWave];
            WaveSegmentData local = wave.Segments[nearestSegment];
            WaveDerived derived = new WaveDerived(local.Energy, local.SampledDepth, wave.PacketLength);
            GUILayout.BeginArea(new Rect(Screen.width - 310, Screen.height - 238, 294, 190), boxStyle);
            GUILayout.Label("CREST SEGMENT INSPECTOR", titleStyle);
            GUILayout.Label($"#{wave.Id}.{nearestSegment}   {local.State.ToString().ToUpperInvariant()}", valueStyle);
            GUILayout.Label($"Source {simulation.GetWaveSourceLabel(wave.SourceId)}   System {(wave.SwellSystemId == 0 ? "MANUAL" : wave.SwellSystemId.ToString())}", smallStyle);
            GUILayout.Label($"Energy {local.Energy:0.00}   Amplitude {derived.Amplitude:0.00}", labelStyle);
            GUILayout.Label($"Break {local.BreakingIntensity:0.00}   Foam {local.FoamEnergy:0.00}", labelStyle);
            GUILayout.Label($"Segment {nearestSegment + 1}/{wave.Segments.Length}   Speed {local.Speed:0.0}", smallStyle);
            GUILayout.Label($"Depth {local.SampledDepth:0.0}   Steepness {derived.Steepness:0.00}", smallStyle);
            GUILayout.EndArea();
        }

        private static string Format(Vector2 value) => $"({value.x:0.0}, {value.y:0.0})";
    }
}
