# Simply Fly Procedural World Generation

Open **Tools > Simply Fly > World Generator**. Configure the seed and world
dimensions, optionally assign Terrain Layers and tree/rock prefabs, then select
**Generate World**. **Regenerate** replaces every existing `World_Generated`
root; **Clear World** removes generated scene objects with Undo support.

The pipeline always uses Unity `Terrain` and `TerrainData`. It does not create a
custom terrain mesh. Generated assets are stored in
`Assets/SimplyFly/Generated/World`; generated scene objects are children of
`World_Generated`.

## Pipeline

1. `TerrainGenerator` creates deterministic rolling terrain and broad valleys.
2. `AirportZoneGenerator` flattens a blended airport rectangle and produces a
   shared airport/approach exclusion mask.
3. `BiomeGenerator` creates large Forest, Farmland, Grassland, and Rocky Hills
   regions from low-frequency seeded masks.
4. `TextureGenerator` paints grass, dirt, and rock from configurable slope and
   elevation thresholds. Neutral fallback layers are generated if none are
   assigned.
5. `VegetationGenerator` places assigned prefabs only in compatible biomes and
   never within the airport or either runway approach.

Phase 2 inserts `LakeGenerator`, `RiverGenerator`, and `RoadGenerator` after
biome masks and before texturing. Lake and river terrain edits therefore affect
the final slope/elevation paint. Roads produce a smooth cubic path from the
airport to a deterministic settlement target plus one to three branches.
Crossings are detected against the river path and produce assigned bridge
prefabs or generated placeholder decks. All Phase 2 paths are shared through
`WorldGenerationContext`, allowing vegetation exclusion and future town/road
consumers to use identical geometry.

Generation is intentionally locked to `Assets/Scenes/WorldGenerationTest.unity`.
Use **Create / Open World Test Scene** in the window before generation. Phase 2
objects use these exact hierarchies:

- `World_Generated/Roads`
- `World_Generated/Water/River`
- `World_Generated/Water/Lake`
- `World_Generated/Bridges`

If road, shoulder, water, or bridge assets are missing, the generator creates
neutral placeholder materials and a simple bridge deck in the generated asset
folder. **Clear Roads and Water** restores the cached Phase 1 heightmap and
regenerates texturing and vegetation without deleting non-generated objects.

## Phase 3: Towns, Farms, and Landmarks

Phase 3 adds six independent stages:

- `TownLocationGenerator` scores flat road-adjacent candidates outside airport,
  water, and steep-terrain exclusions.
- `SettlementRoadGenerator` connects town entrances to the generated network
  and creates curved main and side streets.
- `TownGenerator` places centre, residential, industrial, and rural zones with
  aligned, non-overlapping building footprints and stabilized terrain.
- `FarmGenerator` produces irregular low-profile fields, optional farm clusters,
  and selected access tracks on flat open terrain.
- `LandmarkGenerator` applies type-specific water, road, town, and elevation
  scoring and assigns stable navigation IDs such as `LMK-01`.
- `FinalCleanupGenerator` marks generated content and TerrainData dirty and
  provides the final extension point for later LOD/static-batching processing.

All building categories and landmark types accept prefabs. Missing houses,
shops, warehouses, barns, public buildings, farm props, and landmarks use
clearly named primitive placeholders with shared generated materials. Generated
objects are marked static where appropriate; no per-building runtime scripts are
added. **Clear Phase 3** restores the post-road Terrain and biome snapshots and
removes only settlement roads, settlements, fields, landmarks, and their Phase 3
vegetation effects.

Each stage implements `IWorldGenerator` and receives a shared
`WorldGenerationContext`. Future road, river, town, lake, landmark, or airport
stages can be added to the ordered generator list without coupling them to the
existing generator implementations.

## Verification

The Editor test assembly verifies same-seed reproducibility, different-seed
variation, airport flatness, approach placement exclusion, normalized terrain
texture weights, and duplicate-root cleanup. Run it through Unity Test Runner
in Edit Mode.
