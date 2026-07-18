using System.Collections.Generic;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class TownLocationGenerator : IWorldGenerator
    {
        public string Name => "Town locations";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableTowns) return;
            if (context.roadPaths.Count == 0)
            {
                Debug.LogWarning("Town generation skipped: no generated road network is available.");
                return;
            }
            System.Random random = new(context.settings.seed + context.settings.townSeedOffset);
            Vector3 main = FindCandidate(context, random, context.settlementTarget,
                context.settings.mainTownSize * 0.5f);
            context.settlements.Add(CreateSettlement("Main Town", main,
                context.settings.mainTownSize * 0.5f, false, random));
            for (int i = 0; i < context.settings.villageCount; i++)
            {
                Vector3 preferred = context.roadPaths.Count == 0 ? main :
                    context.roadPaths[i % context.roadPaths.Count][
                        context.roadPaths[i % context.roadPaths.Count].Count * (i + 1) /
                        (context.settings.villageCount + 1)];
                Vector3 village = FindCandidate(context, random, preferred,
                    context.settings.mainTownSize * 0.22f);
                context.settlements.Add(CreateSettlement($"Village {i + 1}", village,
                    context.settings.mainTownSize * 0.22f, true, random));
            }
        }

        public static Vector3 FindCandidate(WorldGenerationContext context,
            System.Random random, Vector3 preferred, float radius)
        {
            WorldGenerationSettings s = context.settings;
            Vector3 best = preferred; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 260; i++)
            {
                Vector3 point = i == 0 ? preferred : context.NormalizedToWorld(
                    Mathf.Lerp(0.06f, 0.94f, (float)random.NextDouble()),
                    Mathf.Lerp(0.06f, 0.94f, (float)random.NextDouble()));
                context.townCandidates.Add(point);
                float nx = point.x / s.terrainSize; float nz = point.z / s.terrainSize;
                float slope = context.terrainData.GetSteepness(nx, nz);
                float roadDistance = Phase3GenerationUtility.DistanceToRoad(context, point);
                bool rejected = slope > s.maximumBuildingSlope ||
                    roadDistance > s.townRoadProximityRange ||
                    WorldGenerationMath.InAirportOrApproach(s, point.x, point.z,
                        s.townAirportExclusionDistance + radius) ||
                    IsNearWater(context, point, radius);
                if (rejected) { context.rejectedTownCandidates.Add(point); continue; }
                float separation = float.PositiveInfinity;
                foreach (SettlementData settlement in context.settlements)
                    separation = Mathf.Min(separation, Vector3.Distance(point, settlement.center));
                if (separation < radius * 3f) continue;
                float score = -slope * 90f - roadDistance * 1.8f +
                    Vector3.Distance(point, context.airportPosition) * 0.06f;
                if (score > bestScore) { best = point; bestScore = score; }
            }
            return best;
        }

        private static bool IsNearWater(WorldGenerationContext context,
            Vector3 point, float radius) =>
            WorldGenerationMath.DistanceToPathXZ(point, context.riverPath) <
                radius + context.settings.riverBankWidth ||
            (context.lakeBoundary.Count > 2 &&
             (WorldGenerationMath.PointInPolygonXZ(point, context.lakeBoundary) ||
              DistanceToLake(context, point) < radius));

        private static float DistanceToLake(WorldGenerationContext context, Vector3 point)
        {
            if (context.lakeBoundary.Count < 2) return float.PositiveInfinity;
            List<Vector3> loop = new(context.lakeBoundary) { context.lakeBoundary[0] };
            return WorldGenerationMath.DistanceToPathXZ(point, loop);
        }

        private static SettlementData CreateSettlement(string name, Vector3 center,
            float radius, bool village, System.Random random)
        {
            SettlementData data = new() { name = name, center = center,
                radius = radius, isVillage = village };
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float irregular = Mathf.Lerp(0.82f, 1.08f, (float)random.NextDouble());
                data.boundary.Add(center + new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle)) * radius * irregular);
            }
            return data;
        }
    }
}
