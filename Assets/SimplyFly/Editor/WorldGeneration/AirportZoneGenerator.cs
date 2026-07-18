using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class AirportZoneGenerator : IWorldGenerator
    {
        public string Name => "Airport zone";

        public void Generate(WorldGenerationContext context)
        {
            WorldGenerationSettings settings = context.settings;
            int resolution = context.terrainData.heightmapResolution;
            float[,] heights = context.terrainData.GetHeights(0, 0, resolution, resolution);
            bool[,] exclusions = new bool[resolution, resolution];

            FlattenAndMask(settings, heights, exclusions);
            context.terrainData.SetHeights(0, 0, heights);
            context.heights = heights;
            context.exclusionMask = exclusions;
            context.airportPosition = context.NormalizedToWorld(
                settings.airportNormalizedPosition.x, settings.airportNormalizedPosition.y);
            context.airportRotation = Quaternion.Euler(0f, settings.airportHeading, 0f);

            GameObject marker = new("Airport_Zone");
            Undo.RegisterCreatedObjectUndo(marker, "Create airport generation zone");
            marker.transform.SetParent(context.root.transform, false);
            marker.transform.SetPositionAndRotation(context.airportPosition, context.airportRotation);
            GameObject forward = new("Runway_Forward");
            Undo.RegisterCreatedObjectUndo(forward, "Create runway direction marker");
            forward.transform.SetParent(marker.transform, false);
            forward.transform.localPosition = Vector3.forward * settings.airportLength * 0.5f;
            EditorUtility.SetDirty(context.terrainData);
        }

        public static void FlattenAndMask(WorldGenerationSettings settings,
            float[,] heights, bool[,] exclusions)
        {
            int resolution = heights.GetLength(0);
            Vector2 airportCenter = settings.airportNormalizedPosition * settings.terrainSize;
            int centerX = Mathf.RoundToInt(settings.airportNormalizedPosition.x * (resolution - 1));
            int centerZ = Mathf.RoundToInt(settings.airportNormalizedPosition.y * (resolution - 1));
            float targetHeight = heights[centerZ, centerX];

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = x / (float)(resolution - 1) * settings.terrainSize;
                float worldZ = z / (float)(resolution - 1) * settings.terrainSize;
                Vector2 local = WorldGenerationMath.Rotate(
                    new Vector2(worldX, worldZ) - airportCenter, -settings.airportHeading);
                float halfWidth = settings.airportWidth * 0.5f;
                float halfLength = settings.airportLength * 0.5f;
                float dx = Mathf.Max(0f, Mathf.Abs(local.x) - halfWidth);
                float dz = Mathf.Max(0f, Mathf.Abs(local.y) - halfLength);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float flatten = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0f, Mathf.Max(1f, settings.airportBlendMargin), distance));
                if (Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.y) <= halfLength)
                    flatten = 1f;
                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, flatten);
                exclusions[z, x] = WorldGenerationMath.InAirportOrApproach(
                    settings, worldX, worldZ, 30f);
            }
        }
    }
}
