using System;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private readonly struct ImpactProbe
        {
            public readonly float LateralDisplacement;
            public readonly float HeadingChange;
            public ImpactProbe(float lateralDisplacement, float headingChange)
            {
                LateralDisplacement = lateralDisplacement;
                HeadingChange = headingChange;
            }
        }

        private readonly struct CrestCoverageProbe
        {
            public readonly float CrestLength;
            public readonly float InsideOffset;
            public readonly float OutsideOffset;
            public readonly int InsideHits;
            public readonly int OutsideHits;

            public CrestCoverageProbe(float crestLength, float insideOffset,
                float outsideOffset, int insideHits, int outsideHits)
            {
                CrestLength = crestLength;
                InsideOffset = insideOffset;
                OutsideOffset = outsideOffset;
                InsideHits = insideHits;
                OutsideHits = outsideHits;
            }
        }

        private readonly struct TravelingPassageProbe
        {
            public readonly int ContactTicks;
            public readonly int MaximumConsecutiveContactTicks;
            public readonly int BreakingEvents;
            public readonly float BoatDisplacement;
            public readonly float PeakBoatSpeed;
            public readonly float WaveLead;

            public TravelingPassageProbe(int contactTicks, int maximumConsecutiveContactTicks,
                int breakingEvents, float boatDisplacement, float peakBoatSpeed, float waveLead)
            {
                ContactTicks = contactTicks;
                MaximumConsecutiveContactTicks = maximumConsecutiveContactTicks;
                BreakingEvents = breakingEvents;
                BoatDisplacement = boatDisplacement;
                PeakBoatSpeed = peakBoatSpeed;
                WaveLead = waveLead;
            }
        }

        private readonly struct StateImpactProbe
        {
            public readonly float Displacement;
            public readonly float HeadingChange;
            public readonly int ContactTicks;
            public readonly int BreakingEvents;

            public StateImpactProbe(float displacement, float headingChange,
                int contactTicks, int breakingEvents)
            {
                Displacement = displacement;
                HeadingChange = headingChange;
                ContactTicks = contactTicks;
                BreakingEvents = breakingEvents;
            }
        }

        private readonly struct SegmentOcclusionProbe
        {
            public readonly int InitialSegments;
            public readonly int ActiveSegments;
            public readonly bool CenterActive;
            public readonly float CenterLag;

            public SegmentOcclusionProbe(int initialSegments, int activeSegments,
                bool centerActive, float centerLag)
            {
                InitialSegments = initialSegments;
                ActiveSegments = activeSegments;
                CenterActive = centerActive;
                CenterLag = centerLag;
            }
        }

        private readonly struct ShelfDeformationProbe
        {
            public readonly int InitialSegments;
            public readonly int ActiveSegments;
            public readonly float ForwardSpread;

            public ShelfDeformationProbe(int initialSegments, int activeSegments,
                float forwardSpread)
            {
                InitialSegments = initialSegments;
                ActiveSegments = activeSegments;
                ForwardSpread = forwardSpread;
            }
        }

        private readonly struct SpeedProbe
        {
            public readonly float SpeedBeforeImpact;
            public readonly float PeakAfterImpact;
            public readonly float MinimumAfterImpact;
            public SpeedProbe(float before, float peak, float minimum)
            {
                SpeedBeforeImpact = before; PeakAfterImpact = peak; MinimumAfterImpact = minimum;
            }
        }

        private readonly struct CruiseProbe
        {
            public readonly float PeakSpeed;
            public readonly float FinalSpeed;
            public readonly int CollisionEvents;

            public CruiseProbe(float peakSpeed, float finalSpeed, int collisionEvents)
            {
                PeakSpeed = peakSpeed;
                FinalSpeed = finalSpeed;
                CollisionEvents = collisionEvents;
            }
        }

        private readonly struct RockSweepProbe
        {
            public readonly int RockIndex;
            public readonly int ImpactEvents;
            public readonly int EscapeImpactEvents;
            public readonly float CombinedRadius;
            public readonly float PostImpactProjection;
            public readonly float EscapeDistance;
            public readonly bool Tunneled;
            public readonly bool Deterministic;

            public RockSweepProbe(int rockIndex, int impactEvents, int escapeImpactEvents,
                float combinedRadius, float postImpactProjection, float escapeDistance,
                bool tunneled, bool deterministic)
            {
                RockIndex = rockIndex;
                ImpactEvents = impactEvents;
                EscapeImpactEvents = escapeImpactEvents;
                CombinedRadius = combinedRadius;
                PostImpactProjection = postImpactProjection;
                EscapeDistance = escapeDistance;
                Tunneled = tunneled;
                Deterministic = deterministic;
            }
        }

        private readonly struct PerformanceProbe
        {
            public readonly int Ticks;
            public readonly int MinimumWaveCount;
            public readonly int FinalWaveCount;
            public readonly double CpuSeconds;
            public readonly double WallSeconds;
            public readonly ulong FinalHash;
            public readonly bool StateFinite;
            public double UpdatesPerSecond => Ticks / Math.Max(0.000001, CpuSeconds);

            public PerformanceProbe(int ticks, int minimumWaveCount, int finalWaveCount,
                double cpuSeconds, double wallSeconds, ulong finalHash, bool stateFinite)
            {
                Ticks = ticks;
                MinimumWaveCount = minimumWaveCount;
                FinalWaveCount = finalWaveCount;
                CpuSeconds = cpuSeconds;
                WallSeconds = wallSeconds;
                FinalHash = finalHash;
                StateFinite = stateFinite;
            }
        }

        private readonly struct ReplayProbe
        {
            public readonly int Ticks;
            public readonly int CommandCount;
            public readonly ulong OriginalHash;
            public readonly ulong ReplayHash;
            public bool Deterministic => OriginalHash == ReplayHash;

            public ReplayProbe(int ticks, int commandCount, ulong originalHash, ulong replayHash)
            {
                Ticks = ticks;
                CommandCount = commandCount;
                OriginalHash = originalHash;
                ReplayHash = replayHash;
            }
        }

        private readonly struct TargetProbe
        {
            public readonly int VisitEvents;
            public readonly int VisitCountAfterArrival;
            public readonly int DisabledVisitCount;
            public readonly int FinalVisitCount;
            public readonly float RelocationDistance;
            public readonly bool Deterministic;

            public TargetProbe(int visitEvents, int visitCountAfterArrival,
                int disabledVisitCount, int finalVisitCount, float relocationDistance,
                bool deterministic)
            {
                VisitEvents = visitEvents;
                VisitCountAfterArrival = visitCountAfterArrival;
                DisabledVisitCount = disabledVisitCount;
                FinalVisitCount = finalVisitCount;
                RelocationDistance = relocationDistance;
                Deterministic = deterministic;
            }
        }

        private readonly struct FloatingObjectProbe
        {
            public readonly int CollectionEvents;
            public readonly int CollectedCount;
            public readonly float CollectedValue;
            public readonly int WreckageEvents;
            public readonly float WreckageSpeedChange;
            public readonly bool Deterministic;

            public FloatingObjectProbe(int collectionEvents, int collectedCount,
                float collectedValue, int wreckageEvents, float wreckageSpeedChange,
                bool deterministic)
            {
                CollectionEvents = collectionEvents;
                CollectedCount = collectedCount;
                CollectedValue = collectedValue;
                WreckageEvents = wreckageEvents;
                WreckageSpeedChange = wreckageSpeedChange;
                Deterministic = deterministic;
            }
        }

        private readonly struct OffshoreBreakingProbe
        {
            public readonly int DeepControlBreakingEvents;
            public readonly int ShelfBreakingEvents;
            public readonly float BreakingDepth;

            public OffshoreBreakingProbe(int deepControlBreakingEvents,
                int shelfBreakingEvents, float breakingDepth)
            {
                DeepControlBreakingEvents = deepControlBreakingEvents;
                ShelfBreakingEvents = shelfBreakingEvents;
                BreakingDepth = breakingDepth;
            }
        }

        private readonly struct BreakingDebrisProbe
        {
            public readonly float TravelingSpeed;
            public readonly float BreakingSpeed;
            public readonly int BreakingEvents;
            public readonly bool Deterministic;

            public BreakingDebrisProbe(float travelingSpeed, float breakingSpeed,
                int breakingEvents, bool deterministic)
            {
                TravelingSpeed = travelingSpeed;
                BreakingSpeed = breakingSpeed;
                BreakingEvents = breakingEvents;
                Deterministic = deterministic;
            }
        }

        private readonly struct SourceCadenceProbe
        {
            public readonly int ExpectedPeriodTicks;
            public readonly int ExpectedFirstTick;
            public readonly int ActualFirstTick;
            public readonly int EmissionCount;
            public readonly int MaximumTickBurst;
            public readonly int MinimumIntervalTicks;
            public readonly int MaximumIntervalTicks;
            public readonly int MinimumPopulation;

            public SourceCadenceProbe(int expectedPeriodTicks, int expectedFirstTick,
                int actualFirstTick, int emissionCount, int maximumTickBurst,
                int minimumIntervalTicks, int maximumIntervalTicks, int minimumPopulation)
            {
                ExpectedPeriodTicks = expectedPeriodTicks;
                ExpectedFirstTick = expectedFirstTick;
                ActualFirstTick = actualFirstTick;
                EmissionCount = emissionCount;
                MaximumTickBurst = maximumTickBurst;
                MinimumIntervalTicks = minimumIntervalTicks;
                MaximumIntervalTicks = maximumIntervalTicks;
                MinimumPopulation = minimumPopulation;
            }
        }

        private readonly struct BreakingLifecycleProbe
        {
            public readonly float InitialEnergy;
            public readonly float OneSecondEnergy;
            public readonly float FinalEnergy;
            public readonly float PeakFoamEnergy;
            public readonly int BreakingEvents;
            public readonly int ActiveSegments;
            public readonly bool ResumedTraveling;

            public BreakingLifecycleProbe(float initialEnergy, float oneSecondEnergy,
                float finalEnergy, float peakFoamEnergy, int breakingEvents,
                int activeSegments, bool resumedTraveling)
            {
                InitialEnergy = initialEnergy;
                OneSecondEnergy = oneSecondEnergy;
                FinalEnergy = finalEnergy;
                PeakFoamEnergy = peakFoamEnergy;
                BreakingEvents = breakingEvents;
                ActiveSegments = activeSegments;
                ResumedTraveling = resumedTraveling;
            }
        }

    }
}
