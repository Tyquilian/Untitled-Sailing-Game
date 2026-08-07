using UnityEngine;

namespace WavePrototype.Simulation
{
    internal static class PrototypeScenario
    {
        public static int AddInitialBoats(WaveSimulation simulation)
        {
            int playerBoatId = simulation.AddBoat(new Vector2(-175f, -93f), 0f);
            simulation.AddBoat(new Vector2(-165f, 84f), -12f);
            simulation.AddBoat(new Vector2(103f, 96f), 190f);
            return playerBoatId;
        }
    }
}
