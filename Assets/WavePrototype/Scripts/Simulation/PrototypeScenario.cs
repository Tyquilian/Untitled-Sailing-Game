using UnityEngine;

namespace WavePrototype.Simulation
{
    internal static class PrototypeScenario
    {
        public static int AddInitialBoats(WaveSimulation simulation)
        {
            Vector2 scale = new Vector2(simulation.Config.WorldHalfExtents.x / 225f,
                simulation.Config.WorldHalfExtents.y / 125f);
            int playerBoatId = simulation.AddBoat(Vector2.Scale(
                new Vector2(-175f, -93f), scale), 0f);
            simulation.AddBoat(Vector2.Scale(new Vector2(-165f, 84f), scale), -12f);
            simulation.AddBoat(Vector2.Scale(new Vector2(103f, 96f), scale), 190f);
            return playerBoatId;
        }
    }
}
