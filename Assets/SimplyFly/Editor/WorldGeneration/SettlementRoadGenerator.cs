using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class SettlementRoadGenerator : IWorldGenerator
    {
        public string Name => "Settlement roads";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableTowns || context.settlements.Count == 0) return;
            GameObject root = new("Settlement_Roads");
            Undo.RegisterCreatedObjectUndo(root, "Generate settlement roads");
            Transform roads = context.root.transform.Find("Roads");
            if (roads == null)
            {
                GameObject roadsObject = new("Roads");
                Undo.RegisterCreatedObjectUndo(roadsObject, "Create roads root");
                roadsObject.transform.SetParent(context.root.transform, false);
                roads = roadsObject.transform;
            }
            root.transform.SetParent(roads, false);
            System.Random random = new(context.settings.seed + context.settings.townSeedOffset + 701);
            foreach (SettlementData settlement in context.settlements)
            {
                Vector3 roadPoint = Phase3GenerationUtility.NearestPointOnRoad(context,
                    settlement.center, out Vector3 roadTangent);
                List<Vector3> entrance = RoadGenerator.BuildCurvedPath(context, roadPoint,
                    settlement.center, random, 4);
                AddStreet(context, root.transform, entrance, settlement.name + "_Main_Street");
                int sideCount = settlement.isVillage ? 2 : 4;
                Vector3 mainDirection = Vector3.ProjectOnPlane(settlement.center - roadPoint,
                    Vector3.up).normalized;
                if (mainDirection.sqrMagnitude < 0.1f) mainDirection = roadTangent;
                Vector3 side = Vector3.Cross(Vector3.up, mainDirection);
                for (int i = 0; i < sideCount; i++)
                {
                    float along = Mathf.Lerp(-0.45f, 0.45f, i / Mathf.Max(1f, sideCount - 1f));
                    Vector3 streetCenter = settlement.center + mainDirection * along * settlement.radius;
                    List<Vector3> sideStreet = new()
                    { streetCenter - side * settlement.radius * 0.72f, streetCenter,
                      streetCenter + side * settlement.radius * 0.72f };
                    sideStreet = Phase2GenerationUtility.SmoothPath(sideStreet, 2);
                    Phase2GenerationUtility.ConformToTerrain(context, sideStreet);
                    AddStreet(context, root.transform, sideStreet,
                        settlement.name + $"_Side_Street_{i + 1}");
                }
            }
        }

        private static void AddStreet(WorldGenerationContext context, Transform parent,
            List<Vector3> path, string name)
        {
            context.settlementRoads.Add(path); context.roadPaths.Add(path);
            float width = Mathf.Max(4f, context.settings.secondaryRoadWidth * 0.72f);
            Phase2GenerationUtility.ShapeTerrainAlongPath(context, path, width * 0.5f, 5f, 0f);
            Phase2GenerationUtility.ConformToTerrain(context, path, 0.1f);
            Phase2GenerationUtility.CreateStrip(name, parent, path, width,
                Phase2GenerationUtility.ResolveMaterial(context.settings.roadMaterial,
                    "Road", new Color(0.12f, 0.12f, 0.12f)), 0.05f);
        }
    }
}
