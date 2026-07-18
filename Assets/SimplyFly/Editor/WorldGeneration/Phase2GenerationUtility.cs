using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public static class Phase2GenerationUtility
    {
        public static List<Vector3> SmoothPath(IReadOnlyList<Vector3> input, int iterations)
        {
            List<Vector3> path = new(input);
            for (int pass = 0; pass < Mathf.Clamp(iterations, 0, 6); pass++)
            {
                if (path.Count < 3) break;
                List<Vector3> smooth = new() { path[0] };
                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 a = path[i - 1]; Vector3 b = path[i];
                    smooth.Add(Vector3.Lerp(a, b, 0.25f));
                    smooth.Add(Vector3.Lerp(a, b, 0.75f));
                }
                smooth.Add(path[path.Count - 1]);
                path = smooth;
            }
            return path;
        }

        public static void ConformToTerrain(WorldGenerationContext context,
            List<Vector3> path, float offset = 0.15f)
        {
            Vector3 origin = context.terrain.transform.position;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path[i];
                point.y = context.terrain.SampleHeight(point) + origin.y + offset;
                path[i] = point;
            }
        }

        public static GameObject CreateStrip(string name, Transform parent,
            IReadOnlyList<Vector3> path, float width, Material material,
            float yOffset = 0f)
        {
            GameObject gameObject = new(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            Vector3[] vertices = new Vector3[path.Count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(path.Count - 1) * 6];
            float distance = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 direction = i == 0 ? path[1] - path[0] :
                    i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
                direction.y = 0f;
                Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
                Vector3 center = path[i] + Vector3.up * yOffset;
                vertices[i * 2] = gameObject.transform.InverseTransformPoint(center - right * width * 0.5f);
                vertices[i * 2 + 1] = gameObject.transform.InverseTransformPoint(center + right * width * 0.5f);
                if (i > 0) distance += Vector3.Distance(path[i - 1], path[i]);
                uv[i * 2] = new Vector2(0f, distance / Mathf.Max(1f, width));
                uv[i * 2 + 1] = new Vector2(1f, distance / Mathf.Max(1f, width));
                if (i == path.Count - 1) continue;
                int triangle = i * 6; int vertex = i * 2;
                triangles[triangle] = vertex; triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1; triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2; triangles[triangle + 5] = vertex + 3;
            }
            Mesh mesh = new() { name = name + " Mesh", vertices = vertices,
                triangles = triangles, uv = uv };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            return gameObject;
        }

        public static Material ResolveMaterial(Material assigned, string name, Color color)
        {
            if (assigned != null) return assigned;
            TerrainGenerator.EnsureFolders();
            string path = $"{TerrainGenerator.GeneratedFolder}/{name}_Placeholder.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = name + " Placeholder", color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void ShapeTerrainAlongPath(WorldGenerationContext context,
            IReadOnlyList<Vector3> path, float innerWidth, float blendWidth,
            float verticalOffset, bool forceDownhill = false)
        {
            TerrainData data = context.terrainData;
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            float cell = context.settings.terrainSize / (resolution - 1f);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                Vector3 point = new(x * cell, 0f, z * cell);
                float distance = WorldGenerationMath.DistanceToPathXZ(point, path);
                if (distance > innerWidth + blendWidth) continue;
                int nearest = 0; float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < path.Count; i++)
                {
                    float candidate = (new Vector2(path[i].x, path[i].z) -
                        new Vector2(point.x, point.z)).sqrMagnitude;
                    if (candidate < nearestDistance) { nearestDistance = candidate; nearest = i; }
                }
                float target = Mathf.Clamp01((path[nearest].y + verticalOffset) /
                    context.settings.terrainHeight);
                float weight = distance <= innerWidth ? 1f : 1f - Mathf.SmoothStep(0f, 1f,
                    (distance - innerWidth) / Mathf.Max(0.1f, blendWidth));
                heights[z, x] = forceDownhill ? Mathf.Min(heights[z, x],
                    Mathf.Lerp(heights[z, x], target, weight)) :
                    Mathf.Lerp(heights[z, x], target, weight * 0.72f);
            }
            data.SetHeights(0, 0, heights);
            context.heights = heights;
            EditorUtility.SetDirty(data);
            ConformToTerrain(context, path as List<Vector3> ?? new List<Vector3>(path));
        }

        public static float PathLength(IReadOnlyList<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        public static bool SegmentIntersectionXZ(Vector3 a, Vector3 b, Vector3 c,
            Vector3 d, out Vector3 crossing)
        {
            Vector2 p = new(a.x, a.z); Vector2 r = new(b.x - a.x, b.z - a.z);
            Vector2 q = new(c.x, c.z); Vector2 s = new(d.x - c.x, d.z - c.z);
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 0.0001f) { crossing = default; return false; }
            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / denominator;
            float u = (qp.x * r.y - qp.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f) { crossing = default; return false; }
            Vector3 value = Vector3.Lerp(a, b, t);
            crossing = value;
            return true;
        }
    }
}
