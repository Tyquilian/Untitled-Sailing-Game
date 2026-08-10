using System.Collections.Generic;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    /// <summary>
    /// Maintains the two authoritative tick snapshots used by presentation interpolation.
    /// Snapshot values are never fed back into the simulation.
    /// </summary>
    internal sealed class PrototypeSnapshotBuffer
    {
        private Dictionary<int, WaveData> previousWaves = new Dictionary<int, WaveData>(700);
        private Dictionary<int, WaveData> currentWaves = new Dictionary<int, WaveData>(700);
        private Dictionary<int, BoatData> previousBoats = new Dictionary<int, BoatData>(8);
        private Dictionary<int, BoatData> currentBoats = new Dictionary<int, BoatData>(8);

        public void Initialize(WaveSimulation simulation)
        {
            previousWaves.Clear();
            currentWaves.Clear();
            previousBoats.Clear();
            currentBoats.Clear();
            Capture(simulation, previousWaves, previousBoats);
            Capture(simulation, currentWaves, currentBoats);
        }

        public void BeginStep()
        {
            Dictionary<int, WaveData> waveSwap = previousWaves;
            previousWaves = currentWaves;
            currentWaves = waveSwap;
            Dictionary<int, BoatData> boatSwap = previousBoats;
            previousBoats = currentBoats;
            currentBoats = boatSwap;
        }

        public void EndStep(WaveSimulation simulation)
        {
            currentWaves.Clear();
            currentBoats.Clear();
            Capture(simulation, currentWaves, currentBoats);
        }

        public void RefreshAfterExternalMutation(WaveSimulation simulation)
        {
            EndStep(simulation);
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
        }

        public WaveData GetWave(WaveData authoritative, float alpha)
        {
            if (!currentWaves.TryGetValue(authoritative.Id, out WaveData current))
                current = authoritative;
            if (!previousWaves.TryGetValue(current.Id, out WaveData previous)) return current;
            Vector2 direction = Vector2.Lerp(previous.TravelDirection,
                current.TravelDirection, alpha);
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

        public BoatData GetBoat(BoatData authoritative, float alpha)
        {
            if (!currentBoats.TryGetValue(authoritative.Id, out BoatData current))
                current = authoritative;
            if (!previousBoats.TryGetValue(current.Id, out BoatData previous)) return current;
            current.Position = Vector2.Lerp(previous.Position, current.Position, alpha);
            current.Velocity = Vector2.Lerp(previous.Velocity, current.Velocity, alpha);
            current.Heading = Mathf.LerpAngle(previous.Heading, current.Heading, alpha);
            current.Health = Mathf.Lerp(previous.Health, current.Health, alpha);
            return current;
        }

        public BoatData GetPlayer(WaveSimulation simulation, float alpha)
            => GetBoat(simulation.Boats[0], alpha);

        private static void Capture(WaveSimulation simulation,
            Dictionary<int, WaveData> waveTarget, Dictionary<int, BoatData> boatTarget)
        {
            for (int i = 0; i < simulation.Waves.Count; i++)
            {
                WaveData wave = simulation.Waves[i];
                waveTarget[wave.Id] = wave;
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
            {
                BoatData boat = simulation.Boats[i];
                boatTarget[boat.Id] = boat;
            }
        }
    }
}
