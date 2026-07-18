using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class BiomeGenerator : IWorldGenerator
    {
        public string Name => "Biome masks";

        public void Generate(WorldGenerationContext context)
        {
            WorldGenerationSettings settings = context.settings;
            int resolution = settings.alphamapResolution;
            WorldBiome[,] biomes = new WorldBiome[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (float)(resolution - 1);
                float nz = z / (float)(resolution - 1);
                float worldX = nx * settings.terrainSize;
                float worldZ = nz * settings.terrainSize;
                if (WorldGenerationMath.InAirportOrApproach(settings, worldX, worldZ, 80f))
                {
                    biomes[z, x] = WorldBiome.Grassland;
                    continue;
                }
                float region = WorldGenerationMath.FractalNoise(worldX, worldZ,
                    settings.seed ^ 0x2c1b3c6d, settings.biomeScale, 3);
                float moisture = WorldGenerationMath.FractalNoise(worldX + 942f,
                    worldZ - 521f, settings.seed ^ 0x119de1f3,
                    settings.biomeScale * 1.25f, 2);
                float elevation = context.terrainData.GetInterpolatedHeight(nx, nz) /
                    Mathf.Max(1f, settings.terrainHeight);
                float steepness = context.terrainData.GetSteepness(nx, nz) / 90f;
                if (steepness > 0.32f || elevation > settings.rockyThreshold)
                    biomes[z, x] = WorldBiome.RockyHills;
                else if (region > settings.forestThreshold && moisture > 0.43f)
                    biomes[z, x] = WorldBiome.Forest;
                else if (region > settings.farmlandThreshold && moisture < 0.55f && steepness < 0.12f)
                    biomes[z, x] = WorldBiome.Farmland;
                else
                    biomes[z, x] = WorldBiome.Grassland;
            }
            context.biomes = biomes;
        }
    }
}
