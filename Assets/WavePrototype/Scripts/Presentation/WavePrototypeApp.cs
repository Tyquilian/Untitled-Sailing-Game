using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class WavePrototypeApp : MonoBehaviour
    {
        private const int InitialSeed = 1847;
        private const int FrameDiagnosticWindow = 240;
        private WaveSimulation simulation;
        private Camera worldCamera;
        private Mesh staticMesh;
        private Mesh dynamicMesh;
        private Material material;
        private float accumulator;
        private bool paused;
        private bool mapView;
        private bool debugOverlay;
        private bool showHelp = true;
        private bool showTargetBearing = true;
        private bool automatedTestDrive;
        private int selectedSeed = InitialSeed;
        private float cameraZoom = 18f;
        private Vector3 cameraVelocity;
        private Vector2 smoothedCameraLookAhead;
        private Vector2 cameraLookAheadVelocity;
        private Dictionary<int, WaveData> previousWaves = new Dictionary<int, WaveData>(700);
        private Dictionary<int, WaveData> currentWaves = new Dictionary<int, WaveData>(700);
        private Dictionary<int, BoatData> previousBoats = new Dictionary<int, BoatData>(8);
        private Dictionary<int, BoatData> currentBoats = new Dictionary<int, BoatData>(8);
        private readonly List<Vector3> staticVertices = new List<Vector3>(140000);
        private readonly List<Color32> staticColors = new List<Color32>(140000);
        private readonly List<int> staticTriangles = new List<int>(210000);
        private readonly List<Vector3> dynamicVertices = new List<Vector3>(24000);
        private readonly List<Color32> dynamicColors = new List<Color32>(24000);
        private readonly List<int> dynamicTriangles = new List<int>(40000);
        private readonly float[] frameDiagnosticSamples = new float[FrameDiagnosticWindow];
        private int frameDiagnosticCount;
        private float averageFrameMilliseconds;
        private float maximumFrameMilliseconds;
        private int lastDynamicVertexCount;
        private ulong cachedStateHash;
        private ulong cachedHashTick = ulong.MaxValue;
        private float nextHashRefreshTime;
        private readonly Queue<string> eventLog = new Queue<string>();
        private GUIStyle titleStyle, labelStyle, smallStyle, valueStyle, buttonStyle, boxStyle;

        public WaveSimulation Simulation => simulation;

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
            SetupCamera();
            SetupRendering();
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
                Debug.Log($"[WAVE-SMOKE] PASS batch=13 ticks={simulation.Tick} waves={simulation.Waves.Count} segments={simulation.ActiveWaveSegmentCount}/{simulation.TotalWaveSegmentCount} systems={simulation.SwellSystems.Count} sources={simulation.ActiveWaveSourceCount}/{simulation.WaveSources.Count} objects={simulation.FloatingObjects.Count} salvage={simulation.CollectedSalvageCount}/{simulation.CollectedSalvageValue:0} rocks={simulation.Environment.Rocks.Count} visits={simulation.Target.VisitCount} hash={simulation.CalculateStateHash():X16}");
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
            CaptureCamera(Path.Combine(buildDirectory, "Batch13-preview.png"));
            Vector3 previousPosition = worldCamera.transform.position;
            float previousSize = worldCamera.orthographicSize;
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
            worldCamera.orthographicSize = GetMapViewSize();
            CaptureCamera(Path.Combine(buildDirectory, "Batch13-map-preview.png"));
            worldCamera.transform.position = previousPosition;
            worldCamera.orthographicSize = previousSize;
            Debug.Log("[WAVE-PREVIEW] Captured Batch 13 follow and map previews in " + buildDirectory);
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
            Debug.Log($"[WAVE-FRAME] batch=13 frames={measuredFrames} avgMs={average:0.00} p99Ms={p99:0.00} maxMs={maximum:0.00} gen0={gen0Collections} heapDelta={heapAfter - heapBefore} movingRepeats={repeatedMovingFrames} maxBoatStep={maximumRenderedStep:0.000} finalSpeed={renderedPlayer.Velocity.magnitude:0.00} staticVerts={staticVertices.Count} dynamicVerts={lastDynamicVertexCount}");
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

        private void SetupCamera()
        {
            GameObject cameraObject = new GameObject("Simulation Camera");
            cameraObject.transform.SetParent(transform, false);
            worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = cameraZoom;
            Vector2 start = simulation.Boats[0].Position;
            worldCamera.transform.position = new Vector3(start.x, start.y, -10f);
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.012f, 0.045f, 0.072f);
            worldCamera.nearClipPlane = 0.01f;
            worldCamera.farClipPlane = 50f;
        }

        private void SetupRendering()
        {
            Shader shader = Shader.Find("WavePrototype/VertexColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Ocean Vertex Color Material" };

            staticMesh = new Mesh { name = "Static Ocean Geometry" };
            staticMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            GameObject staticObject = new GameObject("Static Bathymetry and Rocks");
            staticObject.transform.SetParent(transform, false);
            staticObject.AddComponent<MeshFilter>().sharedMesh = staticMesh;
            MeshRenderer staticRenderer = staticObject.AddComponent<MeshRenderer>();
            staticRenderer.sharedMaterial = material;
            staticRenderer.sortingOrder = 0;

            dynamicMesh = new Mesh { name = "Interpolated Ocean Actors" };
            dynamicMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            dynamicMesh.MarkDynamic();
            GameObject dynamicObject = new GameObject("Dynamic Waves and Boats");
            dynamicObject.transform.SetParent(transform, false);
            dynamicObject.AddComponent<MeshFilter>().sharedMesh = dynamicMesh;
            MeshRenderer dynamicRenderer = dynamicObject.AddComponent<MeshRenderer>();
            dynamicRenderer.sharedMaterial = material;
            dynamicRenderer.sortingOrder = 1;

            InitializeSnapshots();
            RebuildStaticMesh();
            BuildDynamicMesh();
        }

        private void Update()
        {
            HandleKeyboard();
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
            float scroll = Input.mouseScrollDelta.y;
            if (!mapView && Mathf.Abs(scroll) > 0.01f)
                cameraZoom = Mathf.Clamp(cameraZoom * (1f - scroll * 0.08f), 10.5f, 27f);
            BuildDynamicMesh();
            RecordFrameDiagnostic(Time.unscaledDeltaTime * 1000f);
        }

        private void LateUpdate()
        {
            Vector3 target;
            float targetZoom;
            if (mapView)
            {
                target = new Vector3(0f, 0f, -10f);
                targetZoom = GetMapViewSize();
            }
            else
            {
                BoatData player = GetInterpolatedPlayer(RenderInterpolationAlpha);
                Vector2 desiredLookAhead = Vector2.ClampMagnitude(player.Velocity * 0.82f, 9f);
                smoothedCameraLookAhead = Vector2.SmoothDamp(smoothedCameraLookAhead, desiredLookAhead,
                    ref cameraLookAheadVelocity, 0.22f, 32f, Time.unscaledDeltaTime);
                target = new Vector3(player.Position.x + smoothedCameraLookAhead.x,
                    player.Position.y + smoothedCameraLookAhead.y, -10f);
                targetZoom = cameraZoom;
            }
            // Constrain against the larger of current and destination zoom so transitions
            // from the full map cannot briefly drag the still-wide viewport beyond the sea.
            target = ConstrainCameraTarget(target, Mathf.Max(targetZoom, worldCamera.orthographicSize));
            worldCamera.transform.position = Vector3.SmoothDamp(worldCamera.transform.position, target, ref cameraVelocity, mapView ? 0.16f : 0.28f, 1000f, Time.unscaledDeltaTime);
            worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, targetZoom, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.P)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Period)) { paused = true; StepOnce(); }
            if (Input.GetKeyDown(KeyCode.R)) ResetSimulation();
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Vector2 cursor = ScreenToWorld(Input.mousePosition);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    SpawnLocalBreakerBurst(cursor);
                else
                    SpawnSwellFront(cursor);
            }
            if (Input.GetKeyDown(KeyCode.B)) SpawnBoat(ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.C)) SpawnFloatingObject(
                FloatingObjectKind.Cargo, ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.X)) SpawnFloatingObject(
                FloatingObjectKind.Wreckage, ScreenToWorld(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.T)) RelocateTarget();
            if (Input.GetKeyDown(KeyCode.V)) ToggleTarget();
            if (Input.GetKeyDown(KeyCode.K)) ToggleTargetBearing();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) AdjustTargetRadius(-1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) AdjustTargetRadius(1f);
            if (Input.GetKeyDown(KeyCode.M)) mapView = !mapView;
            if (Input.GetKeyDown(KeyCode.F3)) debugOverlay = !debugOverlay;
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }

        private void TogglePause()
        {
            paused = !paused;
            accumulator = 0f;
            // Collapse the interval so pausing or resuming never displays an older tick.
            InitializeSnapshots();
        }

        private void FeedPlayerControl()
        {
            if (automatedTestDrive)
            {
                float automatedSteering = Mathf.Sin((float)simulation.Tick * 0.045f) * 0.16f;
                simulation.SetPlayerControl(1f, automatedSteering);
                return;
            }

            float throttle = 0f;
            float steering = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) throttle -= 0.35f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steering += 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steering -= 1f;
            simulation.SetPlayerControl(throttle, steering);
        }

        private Vector2 ScreenToWorld(Vector3 screen)
        {
            Vector3 world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2(world.x, world.y);
        }

        private void StepOnce()
        {
            SwapSnapshotBuffers();
            simulation.Step();
            CaptureCurrentSnapshots();
            for (int i = 0; i < simulation.Events.Count; i++)
            {
                SimulationEvent e = simulation.Events[i];
                if (e.BoatId != 1) continue;
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

        private void ResetSimulation()
        {
            simulation.Reset(selectedSeed);
            accumulator = 0f;
            eventLog.Clear();
            mapView = false;
            smoothedCameraLookAhead = Vector2.zero;
            cameraLookAheadVelocity = Vector2.zero;
            cameraVelocity = Vector3.zero;
            InvalidateCachedHash();
            InitializeSnapshots();
            RebuildStaticMesh();
            Vector2 start = simulation.Boats[0].Position;
            worldCamera.transform.position = new Vector3(start.x, start.y, -10f);
            PushLog("Reset to deterministic seed " + selectedSeed);
        }

        private void SpawnSwellFront(Vector2 position)
        {
            if (!simulation.SpawnSwellFront(position, 2.65f))
            {
                PushLog("No active swell system is available");
                return;
            }
            RefreshSnapshotsAfterExternalMutation();
            PushLog("Full segmented swell front spawned");
        }

        private void SpawnLocalBreakerBurst(Vector2 position)
        {
            Vector2 direction = simulation.Config.WindDirection.normalized;
            Vector2 crest = new Vector2(-direction.y, direction.x);
            for (int i = -3; i <= 3; i++)
                simulation.SpawnWave(position + crest * i * 2.4f, direction, 2.65f - Mathf.Abs(i) * 0.12f);
            RefreshSnapshotsAfterExternalMutation();
            PushLog("Local seven-packet breaker burst spawned");
        }

        private void SpawnBoat(Vector2 position)
        {
            simulation.AddBoat(position, 0f);
            RefreshSnapshotsAfterExternalMutation();
            PushLog("Passive test boat spawned");
        }

        private void SpawnFloatingObject(FloatingObjectKind kind, Vector2 position)
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

        private void RelocateTarget()
        {
            if (simulation.RelocateTarget())
            {
                InvalidateCachedHash();
                PushLog("Target relocated");
            }
            else PushLog("No safe target water found");
        }

        private void ToggleTarget()
        {
            simulation.SetTargetEnabled(!simulation.Target.Enabled);
            InvalidateCachedHash();
            PushLog(simulation.Target.Enabled ? "Target enabled" : "Target hidden");
        }

        private void ToggleTargetBearing()
        {
            showTargetBearing = !showTargetBearing;
            PushLog(showTargetBearing ? "Target bearing arrow enabled" : "Target bearing arrow hidden");
        }

        private void AdjustTargetRadius(float delta)
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

        private float GetMapViewSize()
        {
            Vector2 half = simulation.Config.WorldHalfExtents;
            float aspect = worldCamera == null ? 16f / 9f : Mathf.Max(0.1f, worldCamera.aspect);
            return Mathf.Max(half.y + 4f, (half.x + 4f) / aspect);
        }

        private Vector3 ConstrainCameraTarget(Vector3 target, float orthographicSize)
        {
            Vector2 half = simulation.Config.WorldHalfExtents;
            float viewHalfHeight = orthographicSize;
            float viewHalfWidth = orthographicSize * Mathf.Max(0.1f, worldCamera.aspect);
            float maximumX = Mathf.Max(0f, half.x - viewHalfWidth);
            float maximumY = Mathf.Max(0f, half.y - viewHalfHeight);
            target.x = Mathf.Clamp(target.x, -maximumX, maximumX);
            target.y = Mathf.Clamp(target.y, -maximumY, maximumY);
            return target;
        }

        private void PushLog(string message)
        {
            eventLog.Enqueue($"{simulation?.SimulatedTime ?? 0f,6:0.0}s  {message}");
            while (eventLog.Count > 6) eventLog.Dequeue();
        }

        private float RenderInterpolationAlpha => paused
            ? 1f
            : Mathf.Clamp01(accumulator / Mathf.Max(0.0001f, simulation.Config.FixedDeltaTime));

        private void InitializeSnapshots()
        {
            previousWaves.Clear();
            currentWaves.Clear();
            previousBoats.Clear();
            currentBoats.Clear();
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveData wave = simulation.Waves[i];
                previousWaves[wave.Id] = wave;
                currentWaves[wave.Id] = wave;
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
            {
                BoatData boat = simulation.Boats[i];
                previousBoats[boat.Id] = boat;
                currentBoats[boat.Id] = boat;
            }
        }

        private void SwapSnapshotBuffers()
        {
            Dictionary<int, WaveData> waveSwap = previousWaves;
            previousWaves = currentWaves;
            currentWaves = waveSwap;
            Dictionary<int, BoatData> boatSwap = previousBoats;
            previousBoats = currentBoats;
            currentBoats = boatSwap;
        }

        private void CaptureCurrentSnapshots()
        {
            currentWaves.Clear();
            currentBoats.Clear();
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveData wave = simulation.Waves[i];
                currentWaves[wave.Id] = wave;
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
            {
                BoatData boat = simulation.Boats[i];
                currentBoats[boat.Id] = boat;
            }
        }

        private void RefreshSnapshotsAfterExternalMutation()
        {
            CaptureCurrentSnapshots();
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveData wave = simulation.Waves[i];
                if (!previousWaves.ContainsKey(wave.Id)) previousWaves[wave.Id] = wave;
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
            {
                BoatData boat = simulation.Boats[i];
                if (!previousBoats.ContainsKey(boat.Id)) previousBoats[boat.Id] = boat;
            }
            InvalidateCachedHash();
        }

        private WaveData InterpolateWave(WaveData current, float alpha)
        {
            if (!previousWaves.TryGetValue(current.Id, out WaveData previous)) return current;
            Vector2 direction = Vector2.Lerp(previous.TravelDirection, current.TravelDirection, alpha);
            if (direction.sqrMagnitude < 0.0001f) direction = current.TravelDirection;
            else direction.Normalize();
            current.Position = Vector2.Lerp(previous.Position, current.Position, alpha);
            current.TravelDirection = direction;
            current.Energy = Mathf.Lerp(previous.Energy, current.Energy, alpha);
            current.Speed = Mathf.Lerp(previous.Speed, current.Speed, alpha);
            current.PacketLength = Mathf.Lerp(previous.PacketLength, current.PacketLength, alpha);
            current.CrestLength = Mathf.Lerp(previous.CrestLength, current.CrestLength, alpha);
            return current;
        }

        private BoatData InterpolateBoat(BoatData current, float alpha)
        {
            if (!previousBoats.TryGetValue(current.Id, out BoatData previous)) return current;
            current.Position = Vector2.Lerp(previous.Position, current.Position, alpha);
            current.Velocity = Vector2.Lerp(previous.Velocity, current.Velocity, alpha);
            current.Heading = Mathf.LerpAngle(previous.Heading, current.Heading, alpha);
            current.Health = Mathf.Lerp(previous.Health, current.Health, alpha);
            return current;
        }

        private BoatData GetInterpolatedPlayer(float alpha)
        {
            BoatData authoritative = simulation.Boats[0];
            if (!currentBoats.TryGetValue(authoritative.Id, out BoatData current)) current = authoritative;
            return InterpolateBoat(current, alpha);
        }

        private void RebuildStaticMesh()
        {
            if (simulation == null || staticMesh == null) return;
            staticVertices.Clear();
            staticColors.Clear();
            staticTriangles.Clear();
            AddBathymetry(staticVertices, staticColors, staticTriangles);
            for (int i = 0; i < simulation.Environment.Rocks.Count; i++)
            {
                RockData rock = simulation.Environment.Rocks[i];
                AddCircle(staticVertices, staticColors, staticTriangles, rock.Position, rock.Radius * 1.12f,
                    new Color(0.075f, 0.085f, 0.082f), 0.16f, 14);
                AddCircle(staticVertices, staticColors, staticTriangles, rock.Position + Vector2.one * 0.1f,
                    rock.Radius * 0.72f, new Color(0.31f, 0.32f, 0.29f), 0.12f, 12);
            }
            staticMesh.Clear(true);
            staticMesh.SetVertices(staticVertices);
            staticMesh.SetColors(staticColors);
            staticMesh.SetTriangles(staticTriangles, 0, false);
            ApplyWorldMeshBounds(staticMesh);
        }

        private void BuildDynamicMesh()
        {
            if (simulation == null || dynamicMesh == null) return;
            dynamicVertices.Clear();
            dynamicColors.Clear();
            dynamicTriangles.Clear();
            float alpha = RenderInterpolationAlpha;

            if (debugOverlay)
            {
                AddSwellStructureBands(alpha);
                AddWaveSourceDiagnostics();
            }

            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveData authoritative = simulation.Waves[i];
                if (!currentWaves.TryGetValue(authoritative.Id, out WaveData current)) current = authoritative;
                AddWave(dynamicVertices, dynamicColors, dynamicTriangles, InterpolateWave(current, alpha));
            }

            if (simulation.Target.Enabled) AddTargetMarker(simulation.Target);

            for (int i = 0; i < simulation.FloatingObjects.Count; i++)
                AddFloatingObject(simulation.FloatingObjects[i], alpha);

            if (debugOverlay)
            {
                for (int i = 0; i < simulation.Environment.Rocks.Count; i++)
                {
                    RockData rock = simulation.Environment.Rocks[i];
                    AddCircle(dynamicVertices, dynamicColors, dynamicTriangles, rock.Position, rock.Radius + 1.15f,
                        new Color(0.95f, 0.2f, 0.12f, 0.16f), 0.31f, 14);
                }
            }

            BoatData player = default;
            if (simulation.Boats.Count > 0)
            {
                player = GetInterpolatedPlayer(alpha);
                AddCircle(dynamicVertices, dynamicColors, dynamicTriangles, player.Position, 2.05f,
                    new Color(1f, 0.62f, 0.08f, 0.16f), 0.1f, 20);
                if (showTargetBearing && simulation.Target.Enabled)
                    AddTargetBearingArrow(player.Position, simulation.Target.Position);
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
            {
                BoatData authoritative = simulation.Boats[i];
                if (!currentBoats.TryGetValue(authoritative.Id, out BoatData current)) current = authoritative;
                AddBoat(dynamicVertices, dynamicColors, dynamicTriangles, InterpolateBoat(current, alpha), i == 0);
            }

            if (debugOverlay && simulation.Boats.Count > 0)
            {
                Vector2 waveForce = simulation.SampleAmbientWaveField(player.Position);
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, player.Position, waveForce * 0.42f,
                    new Color(0.98f, 0.24f, 0.14f, 0.86f), 0.04f);
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, player.Position, simulation.WindVelocity * 0.72f,
                    new Color(1f, 0.86f, 0.28f, 0.86f), 0.05f);
            }

            lastDynamicVertexCount = dynamicVertices.Count;
            dynamicMesh.Clear(true);
            dynamicMesh.SetVertices(dynamicVertices);
            dynamicMesh.SetColors(dynamicColors);
            dynamicMesh.SetTriangles(dynamicTriangles, 0, false);
            ApplyWorldMeshBounds(dynamicMesh);
        }

        private void AddWaveSourceDiagnostics()
        {
            for (int i = 0; i < simulation.WaveSources.Count; i++)
            {
                WaveSourceData source = simulation.WaveSources[i];
                Color color = source.Enabled ? SourceColor(source.Kind)
                    : new Color(0.42f, 0.48f, 0.5f, 0.08f);
                Vector2 segment = source.SegmentEnd - source.SegmentStart;
                float angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
                Vector2 midpoint = (source.SegmentStart + source.SegmentEnd) * 0.5f;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, midpoint,
                    new Vector2(segment.magnitude, 0.22f), angle, color, 0.27f);
                if (source.Enabled)
                    AddVector(dynamicVertices, dynamicColors, dynamicTriangles, midpoint,
                        source.Direction * 7f, color, 0.28f, 0.18f);
            }
        }

        private void AddSwellStructureBands(float alpha)
        {
            for (int systemIndex = 0; systemIndex < simulation.SwellSystems.Count; systemIndex++)
            {
                SwellSystemData system = simulation.SwellSystems[systemIndex];
                Vector2 direction = system.Direction.sqrMagnitude < 0.001f
                    ? Vector2.right : system.Direction.normalized;
                Vector2 crestAxis = new Vector2(-direction.y, direction.x);
                float minimumProjection = float.MaxValue;
                float maximumProjection = float.MinValue;
                float lateralSum = 0f;
                int count = 0;

                for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
                {
                    WaveData authoritative = simulation.Waves[waveIndex];
                    if (authoritative.SwellSystemId != system.Id) continue;
                    if (!currentWaves.TryGetValue(authoritative.Id, out WaveData current)) current = authoritative;
                    WaveData wave = InterpolateWave(current, alpha);
                    float projection = Vector2.Dot(wave.Position, direction);
                    minimumProjection = Mathf.Min(minimumProjection, projection);
                    maximumProjection = Mathf.Max(maximumProjection, projection);
                    lateralSum += Vector2.Dot(wave.Position, crestAxis);
                    count++;
                }

                if (count < 2) continue;
                float centerProjection = (minimumProjection + maximumProjection) * 0.5f;
                Vector2 center = direction * centerProjection + crestAxis * (lateralSum / count);
                float length = maximumProjection - minimumProjection + system.PacketSpacing * 0.72f;
                float width = Mathf.Max(5f, system.MeanCrestLength * 1.12f);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Color color = SwellColor(system.SourceId, system.Id, 0.13f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, center,
                    new Vector2(length, width), angle, color, 0.64f);
                if (debugOverlay)
                    AddVector(dynamicVertices, dynamicColors, dynamicTriangles, center,
                        direction * Mathf.Min(10f, length * 0.35f),
                        SwellColor(system.SourceId, system.Id, 0.68f), 0.6f, 0.18f);
            }
        }

        private static Color SwellColor(int sourceId, int systemId, float alpha)
        {
            Color baseColor;
            if (sourceId == 1) baseColor = new Color(0.26f, 0.72f, 0.92f);
            else if (sourceId == 2) baseColor = new Color(0.62f, 0.54f, 0.94f);
            else baseColor = new Color(0.28f, 0.88f, 0.72f);
            float variation = ((systemId * 37) % 7) / 6f;
            baseColor = Color.Lerp(baseColor, Color.white, variation * 0.16f);
            baseColor.a = alpha;
            return baseColor;
        }

        private static Color SourceColor(WaveSourceKind kind)
        {
            switch (kind)
            {
                case WaveSourceKind.WesternSwell: return new Color(0.28f, 1f, 0.68f, 0.42f);
                case WaveSourceKind.NorthernCrossSea: return new Color(0.85f, 0.42f, 1f, 0.42f);
                default: return new Color(1f, 0.58f, 0.26f, 0.42f);
            }
        }

        private void AddTargetMarker(TargetMarkerData target)
        {
            Color ring = new Color(1f, 0.76f, 0.12f, 0.82f);
            AddRing(dynamicVertices, dynamicColors, dynamicTriangles, target.Position,
                target.VisitRadius, 0.26f, ring, 0.68f, 40);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.12f;
            AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, target.Position,
                Vector2.one * 2.4f * pulse, 45f, new Color(1f, 0.86f, 0.22f, 0.96f), 0.69f);
            AddVector(dynamicVertices, dynamicColors, dynamicTriangles,
                target.Position + Vector2.down * 3.5f, Vector2.up * 2.2f,
                new Color(1f, 0.92f, 0.52f, 0.9f), 0.7f, 0.18f);
        }

        private void AddTargetBearingArrow(Vector2 boatPosition, Vector2 targetPosition)
        {
            Vector2 direction = targetPosition - boatPosition;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            Vector2 start = boatPosition + direction * 3f;
            AddArrow(dynamicVertices, dynamicColors, dynamicTriangles, start,
                direction * 5.5f, new Color(1f, 0.82f, 0.18f, 0.86f), 0.72f, 0.2f);
        }

        private void AddFloatingObject(FloatingObjectData item, float alpha)
        {
            Vector2 position = Vector2.Lerp(item.PreviousPosition, item.Position, alpha);
            if (item.Kind == FloatingObjectKind.Cargo)
            {
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    Vector2.one * item.Radius * 1.75f, 45f,
                    new Color(0.94f, 0.64f, 0.12f, 0.98f), 0.7f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 1.45f, 0.16f), 45f,
                    new Color(1f, 0.9f, 0.48f, 0.94f), 0.71f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 1.45f, 0.16f), -45f,
                    new Color(1f, 0.9f, 0.48f, 0.94f), 0.71f);
            }
            else
            {
                float angle = item.Id * 37f + item.Position.x * 0.4f;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 2.25f, item.Radius * 0.62f), angle,
                    new Color(0.34f, 0.22f, 0.12f, 0.98f), 0.69f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                    position + new Vector2(0.18f, 0.14f),
                    new Vector2(item.Radius * 1.65f, item.Radius * 0.34f), angle + 67f,
                    new Color(0.5f, 0.34f, 0.18f, 0.95f), 0.7f);
            }
            if (debugOverlay)
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    item.Velocity * 1.4f, new Color(1f, 0.74f, 0.28f, 0.72f), 0.73f, 0.08f);
        }

        private void ApplyWorldMeshBounds(Mesh targetMesh)
        {
            Vector2 half = simulation.Config.WorldHalfExtents;
            targetMesh.bounds = new Bounds(Vector3.zero,
                new Vector3(half.x * 2f + 12f, half.y * 2f + 12f, 8f));
        }

        private void RecordFrameDiagnostic(float milliseconds)
        {
            frameDiagnosticSamples[frameDiagnosticCount++] = milliseconds;
            if (frameDiagnosticCount < frameDiagnosticSamples.Length) return;
            float total = 0f;
            float maximum = 0f;
            for (int i = 0; i < frameDiagnosticSamples.Length; i++)
            {
                total += frameDiagnosticSamples[i];
                maximum = Mathf.Max(maximum, frameDiagnosticSamples[i]);
            }
            averageFrameMilliseconds = total / frameDiagnosticSamples.Length;
            maximumFrameMilliseconds = maximum;
            frameDiagnosticCount = 0;
        }

        private void InvalidateCachedHash()
        {
            cachedHashTick = ulong.MaxValue;
            nextHashRefreshTime = 0f;
        }

        private ulong GetCachedStateHash()
        {
            if (cachedHashTick == ulong.MaxValue ||
                (cachedHashTick != simulation.Tick && Time.unscaledTime >= nextHashRefreshTime))
            {
                cachedStateHash = simulation.CalculateStateHash();
                cachedHashTick = simulation.Tick;
                nextHashRefreshTime = Time.unscaledTime + 0.25f;
            }
            return cachedStateHash;
        }

        private void AddBathymetry(List<Vector3> v, List<Color32> c, List<int> t)
        {
            const int cellsX = 225;
            const int cellsY = 125;
            Vector2 half = simulation.Config.WorldHalfExtents;
            float width = half.x * 2f / cellsX;
            float height = half.y * 2f / cellsY;
            for (int y = 0; y < cellsY; y++)
            {
                float centerY = -half.y + (y + 0.5f) * height;
                for (int x = 0; x < cellsX; x++)
                {
                    float centerX = -half.x + (x + 0.5f) * width;
                    Vector2 center = new Vector2(centerX, centerY);
                    float depth = simulation.Environment.SampleDepth(center);
                    Color color = DepthColor(depth);
                    AddQuad(v, c, t, center, new Vector2(width + 0.04f, height + 0.04f), 0f, color, 2f);
                }
            }
        }

        private static Color DepthColor(float depth)
        {
            if (depth <= 0.24f) return new Color(0.12f, 0.17f, 0.125f);
            if (depth < 0.72f) return Color.Lerp(new Color(0.36f, 0.42f, 0.28f), new Color(0.08f, 0.46f, 0.42f), depth / 0.72f);
            if (depth < 3.5f) return Color.Lerp(new Color(0.07f, 0.5f, 0.45f), new Color(0.025f, 0.31f, 0.4f), (depth - 0.72f) / 2.78f);
            if (depth < 6.5f) return Color.Lerp(new Color(0.025f, 0.3f, 0.4f),
                new Color(0.018f, 0.18f, 0.31f), Mathf.InverseLerp(3.5f, 6.5f, depth));
            return Color.Lerp(new Color(0.018f, 0.18f, 0.31f), new Color(0.012f, 0.075f, 0.15f), Mathf.InverseLerp(6.5f, 12f, depth));
        }

        private void AddWave(List<Vector3> v, List<Color32> c, List<int> t, WaveData wave)
        {
            WaveSegmentData[] segments = wave.Segments;
            if (segments == null || segments.Length == 0) return;
            float alpha = RenderInterpolationAlpha;
            float nominalSpacing = segments.Length == 1
                ? wave.CrestLength : wave.CrestLength / (segments.Length - 1f);
            float maximumLink = nominalSpacing * simulation.Config.WaveSegmentLinkBreakMultiplier;

            for (int index = 0; index < segments.Length; index++)
            {
                WaveSegmentData segment = segments[index];
                if (!segment.Active) continue;
                Vector2 position = Vector2.Lerp(segment.PreviousPosition, segment.Position, alpha);
                bool linkLeft = index > 0 && segments[index - 1].Active &&
                    Vector2.Distance(position, InterpolatedSegmentPosition(segments[index - 1], alpha)) <= maximumLink;
                bool linkRight = index + 1 < segments.Length && segments[index + 1].Active &&
                    Vector2.Distance(position, InterpolatedSegmentPosition(segments[index + 1], alpha)) <= maximumLink;
                Vector2 left = linkLeft ? InterpolatedSegmentPosition(segments[index - 1], alpha) : position;
                Vector2 right = linkRight ? InterpolatedSegmentPosition(segments[index + 1], alpha) : position;
                Vector2 tangent = linkLeft && linkRight ? right - left
                    : linkLeft ? position - left
                    : linkRight ? right - position
                    : new Vector2(-segment.TravelDirection.y, segment.TravelDirection.x);
                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = new Vector2(-segment.TravelDirection.y, segment.TravelDirection.x);
                tangent.Normalize();

                float leftExtent = linkLeft ? Vector2.Distance(position, left) * 0.5f
                    : (index == 0 ? nominalSpacing * 0.5f : nominalSpacing * 0.24f);
                float rightExtent = linkRight ? Vector2.Distance(position, right) * 0.5f
                    : (index == segments.Length - 1 ? nominalSpacing * 0.5f : nominalSpacing * 0.24f);
                if (segments.Length == 1) leftExtent = rightExtent = wave.CrestLength * 0.5f;
                float visualSpan = Mathf.Max(0.8f, leftExtent + rightExtent);
                Vector2 center = position + tangent * ((rightExtent - leftExtent) * 0.5f);

                WaveDerived derived = new WaveDerived(segment.Energy,
                    segment.SampledDepth, wave.PacketLength);
                float energy01 = Mathf.InverseLerp(0.08f, 3.2f, segment.Energy);
                Color color = SegmentColor(segment.State, energy01);
                float thickness = Mathf.Clamp(wave.PacketLength * 0.22f +
                    derived.Amplitude * 0.16f, 0.58f, 1.85f);
                float tangentAngle = Mathf.Atan2(-tangent.x, tangent.y) * Mathf.Rad2Deg;
                AddQuad(v, c, t, center, new Vector2(thickness, visualSpan),
                    tangentAngle, color, 0.52f);

                float shoaling = Mathf.InverseLerp(6.5f, 1.1f, segment.SampledDepth);
                if (segment.State == WaveState.Traveling && shoaling > 0.05f)
                    AddQuad(v, c, t, center - segment.TravelDirection * thickness * 0.72f,
                        new Vector2(thickness * 0.3f, visualSpan * 0.88f), tangentAngle,
                        new Color(0.72f, 0.96f, 0.91f, shoaling * 0.28f), 0.525f);

                float foam01 = Mathf.InverseLerp(simulation.Config.MinimumFoamEnergy,
                    0.45f, segment.FoamEnergy);
                if (segment.State == WaveState.Breaking)
                {
                    AddQuad(v, c, t, center - segment.TravelDirection * thickness * 0.9f,
                        new Vector2(thickness * 0.72f, visualSpan * 0.82f), tangentAngle,
                        new Color(0.94f, 1f, 0.94f,
                            0.3f + segment.BreakingIntensity * 0.38f), 0.53f);
                    AddQuad(v, c, t, center - segment.TravelDirection * thickness * 1.65f,
                        new Vector2(thickness * 0.52f, visualSpan * 0.68f), tangentAngle,
                        new Color(0.82f, 0.98f, 0.94f,
                            0.12f + Mathf.Max(segment.BreakingIntensity, foam01) * 0.28f), 0.54f);
                }
                else if (segment.FoamEnergy >= simulation.Config.MinimumFoamEnergy)
                    AddQuad(v, c, t, center - segment.TravelDirection * thickness * 0.7f,
                        new Vector2(thickness * 0.34f, visualSpan * 0.7f), tangentAngle,
                        new Color(0.92f, 1f, 0.97f, 0.08f + foam01 * 0.26f), 0.54f);

                if (debugOverlay)
                    AddVector(v, c, t, position, segment.TravelDirection *
                        (1.2f + energy01 * 2.4f), new Color(1f, 0.76f, 0.18f, 0.65f),
                        0.42f, 0.075f);
            }
        }

        private static Vector2 InterpolatedSegmentPosition(WaveSegmentData segment, float alpha)
            => Vector2.Lerp(segment.PreviousPosition, segment.Position, alpha);

        private Color SegmentColor(WaveState state, float energy01)
        {
            if (state == WaveState.Breaking)
                return Color.Lerp(new Color(0.72f, 0.93f, 0.92f, 0.82f),
                    new Color(1f, 0.98f, 0.82f, 0.98f), energy01);
            if (state == WaveState.Spent)
                return new Color(0.92f, 1f, 0.97f, 0.3f);
            if (debugOverlay)
                return Color.Lerp(new Color(0.12f, 0.62f, 0.82f, 0.55f),
                    new Color(1f, 0.27f, 0.1f, 0.94f), energy01);
            return Color.Lerp(new Color(0.18f, 0.67f, 0.82f, 0.52f),
                new Color(0.66f, 0.94f, 1f, 0.92f), energy01);
        }

        private static void AddBoat(List<Vector3> v, List<Color32> c, List<int> t, BoatData boat, bool player)
        {
            float a = boat.Heading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 side = new Vector2(-forward.y, forward.x);
            Color hull = boat.Health > 35f ? (player ? new Color(1f, 0.55f, 0.08f) : new Color(0.78f, 0.66f, 0.38f)) : new Color(0.9f, 0.12f, 0.08f);
            AddTriangle(v, c, t,
                boat.Position + forward * 1.75f,
                boat.Position - forward * 1.2f + side * 0.82f,
                boat.Position - forward * 1.2f - side * 0.82f,
                hull, 0.025f);
            Color sail = player ? new Color(1f, 0.92f, 0.72f, 0.95f) : new Color(0.78f, 0.84f, 0.78f, 0.86f);
            AddTriangle(v, c, t,
                boat.Position + forward * 0.95f,
                boat.Position - forward * 0.72f + side * 0.18f,
                boat.Position - forward * 0.68f + side * 1.08f,
                sail, 0.015f);
        }

        private static void AddTriangle(List<Vector3> v, List<Color32> c, List<int> t, Vector2 a, Vector2 b, Vector2 d, Color color, float z)
        {
            int start = v.Count; Color32 value = color;
            v.Add((Vector3)a + Vector3.forward * z); v.Add((Vector3)b + Vector3.forward * z); v.Add((Vector3)d + Vector3.forward * z);
            c.Add(value); c.Add(value); c.Add(value); t.Add(start); t.Add(start + 2); t.Add(start + 1);
        }

        private static void AddVector(List<Vector3> v, List<Color32> c, List<int> t, Vector2 start, Vector2 vector, Color color, float z, float width = 0.14f)
        {
            float length = vector.magnitude;
            if (length < 0.08f) return;
            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            AddQuad(v, c, t, start + vector * 0.5f, new Vector2(length, width), angle, color, z);
        }

        private static void AddArrow(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 start, Vector2 vector, Color color, float z, float width)
        {
            float length = vector.magnitude;
            if (length < 0.2f) return;
            Vector2 direction = vector / length;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float headLength = Mathf.Min(1.35f, length * 0.32f);
            Vector2 tip = start + vector;
            AddVector(v, c, t, start, direction * (length - headLength * 0.45f),
                color, z, width);
            AddTriangle(v, c, t, tip,
                tip - direction * headLength + side * headLength * 0.55f,
                tip - direction * headLength - side * headLength * 0.55f,
                color, z - 0.001f);
        }

        private static void AddQuad(List<Vector3> v, List<Color32> c, List<int> t, Vector2 center, Vector2 size, float degrees, Color color, float z)
        {
            int start = v.Count;
            float rad = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            Vector2 right = new Vector2(cs, sn) * size.x * 0.5f;
            Vector2 up = new Vector2(-sn, cs) * size.y * 0.5f;
            v.Add((Vector3)(center - right - up) + Vector3.forward * z); v.Add((Vector3)(center + right - up) + Vector3.forward * z);
            v.Add((Vector3)(center + right + up) + Vector3.forward * z); v.Add((Vector3)(center - right + up) + Vector3.forward * z);
            Color32 value = color; c.Add(value); c.Add(value); c.Add(value); c.Add(value);
            t.Add(start); t.Add(start + 2); t.Add(start + 1); t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }

        private static void AddRing(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 center, float radius, float thickness, Color color, float z, int segments)
        {
            float segmentLength = Mathf.Max(thickness, 2f * Mathf.PI * radius / segments * 1.08f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                float radians = angle * Mathf.Deg2Rad;
                Vector2 position = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                AddQuad(v, c, t, position, new Vector2(segmentLength, thickness),
                    angle + 90f, color, z);
            }
        }

        private static void AddCircle(List<Vector3> v, List<Color32> c, List<int> t, Vector2 center, float radius, Color color, float z, int segments)
        {
            int start = v.Count; Color32 value = color;
            v.Add((Vector3)center + Vector3.forward * z); c.Add(value);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add((Vector3)(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius) + Vector3.forward * z); c.Add(value);
            }
            for (int i = 0; i < segments; i++) { t.Add(start); t.Add(start + i + 2); t.Add(start + i + 1); }
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
                WaveSegmentData[] segments = simulation.Waves[i].Segments;
                if (segments == null) continue;
                for (int segment = 0; segment < segments.Length; segment++)
                {
                    if (!segments[segment].Active) continue;
                    if (segments[segment].State == WaveState.Breaking) breaking++;
                    if (segments[segment].FoamEnergy >= simulation.Config.MinimumFoamEnergy) foam++;
                }
            }

            GUILayout.BeginArea(new Rect(16, 16, 380, debugOverlay ? 860 : 710), boxStyle);
            GUILayout.Label("TACTICAL SAILING // BATCH 13", titleStyle);
            GUILayout.Label("Phase-locked swell and partial breaking laboratory", smallStyle);
            GUILayout.Space(9);
            GUILayout.Label(paused ? "PAUSED" : "UNDERWAY", valueStyle);
            GUILayout.Label($"Speed        {player.Velocity.magnitude,5:0.0} / {simulation.Config.BoatSurfSpeedCap:0.0}", labelStyle);
            GUILayout.Label($"Cruise cap   {simulation.Config.BoatCruiseSpeed,5:0.0}", smallStyle);
            GUILayout.Label($"Hull         {player.Health,5:0.0}%", labelStyle);
            GUILayout.Label($"Depth        {depth,5:0.0} m", labelStyle);
            GUILayout.Label($"Wind drive   {windEfficiency * 100f,5:0}%", labelStyle);
            GUILayout.Label($"Ambient sea  {waveForce.magnitude,5:0.0}", labelStyle);
            GUILayout.Label($"Position     {Format(player.Position)}", smallStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "▶ Resume" : "Ⅱ Pause", buttonStyle)) TogglePause();
            if (GUILayout.Button("Step ›", buttonStyle)) { paused = true; StepOnce(); }
            if (GUILayout.Button(mapView ? "Follow" : "Map", buttonStyle)) mapView = !mapView;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", buttonStyle)) ResetSimulation();
            if (GUILayout.Button("Swell Front", buttonStyle)) SpawnSwellFront(player.Position - Vector2.right * 7f);
            if (GUILayout.Button("Local Burst", buttonStyle)) SpawnLocalBreakerBurst(player.Position - Vector2.right * 7f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Boat", buttonStyle)) SpawnBoat(player.Position + Vector2.up * 5f);
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
                GUILayout.Label($"FRAME  {averageFrameMilliseconds:0.0} ms avg / {maximumFrameMilliseconds:0.0} max   DYN {lastDynamicVertexCount:N0}v", smallStyle);
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
            foreach (string entry in eventLog) GUILayout.Label(entry, smallStyle);
            GUILayout.EndArea();

            if (showHelp)
            {
                const float width = 280f;
                GUILayout.BeginArea(new Rect(Screen.width - width - 16, 16, width, 432), boxStyle);
                GUILayout.Label("CONTROLS", titleStyle);
                GUILayout.Label("W / ↑      Forward", labelStyle);
                GUILayout.Label("S / ↓       Brake / reverse", labelStyle);
                GUILayout.Label("A D / ← →  Steer", labelStyle);
                GUILayout.Space(5);
                GUILayout.Label("M            Full map / follow", labelStyle);
                GUILayout.Label("Wheel      Follow-camera zoom", labelStyle);
                GUILayout.Label("Q             Full swell front at cursor", labelStyle);
                GUILayout.Label("Shift + Q     Local breaker burst (debug)", labelStyle);
                GUILayout.Label("B             Passive boat at cursor", labelStyle);
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
                WaveSegmentData[] segments = simulation.Waves[i].Segments;
                if (segments == null) continue;
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
