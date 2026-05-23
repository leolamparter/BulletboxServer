using System.Collections.Concurrent;
using System.Numerics;

public enum BiomeType
{
    Meadow,
    Forest,
    Desert,
    StonyPeaks,
    Ocean,
    Beach,
    BrimstoneSprings,
    River
}

public enum FeatureType
{
    None,
    SmallTree,
    LargeTree,
    MeadowHedge,
    MeadowFlowers,
    Stone,
    PalmTree,
    DesertLog,
    Tumbleweed,
    OasisDesert,
    BeachUmbrella,
    Sailboat,
    SulfurSpring
}

public struct ChunkCoord
{
    public int X;
    public int Y;
    public ChunkCoord(int x, int y) { X = x; Y = y; }
    public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
    public override bool Equals(object? obj) => obj is ChunkCoord c && c.X == X && c.Y == Y;
}

public class Chunk
{
    public ChunkCoord Coord;
    public BiomeType Biome;
    public FeatureType Feature;
    public Chunk(ChunkCoord coord, BiomeType biome, FeatureType feature = FeatureType.None)
    {
        Coord = coord;
        Biome = biome;
        Feature = feature;
    }
}

public class World
{
    // Key: Username, Value: Last known position
    public ConcurrentDictionary<string, Vector2> PlayerLocations = new();

    // Key: ChunkCoord, Value: Chunk
    private ConcurrentDictionary<ChunkCoord, Chunk> Chunks = new();
    private static readonly Random rng = new();
    public const int ChunkSize = 16; // Very small chunks

    public void UpdatePosition(string username, float x, float y)
    {
        PlayerLocations[username] = new Vector2(x, y);
    }

    public void RemovePlayer(string username)
    {
        PlayerLocations.TryRemove(username, out _);
    }

    public Chunk GetOrGenerateChunk(int chunkX, int chunkY)
    {
        var coord = new ChunkCoord(chunkX, chunkY);
        if (!Chunks.TryGetValue(coord, out var chunk))
        {
            // Dedicated low-frequency noise for rare but massive oceans
            float oceanNoise = (Perlin.Noise(chunkX * 0.003f, chunkY * 0.003f) + 1f) * 0.5f;
            float scale = 0.008f;
            float riverNoise = Perlin.Noise(chunkX * 0.025f, chunkY * 0.025f);
            float noise = Perlin.Noise(chunkX * scale, chunkY * scale);
            float noise2 = Perlin.Noise(chunkX * scale * 0.5f + 1000, chunkY * scale * 0.5f - 1000) * 0.5f;
            float n = (noise + noise2 + 1f) * 0.5f;
            float landNoise = Perlin.Noise(chunkX * 0.018f + 5000, chunkY * 0.018f - 5000);
            float landN = (landNoise + 1f) * 0.5f;

            BiomeType biome;
            if (oceanNoise < 0.25f) {
                biome = BiomeType.Ocean;
            } else if (oceanNoise < 0.30f) {
                biome = BiomeType.Beach;
            } else if (Math.Abs(riverNoise) < 0.035f) {
                biome = BiomeType.River;
            } else if (n > 0.80f) {
                biome = BiomeType.BrimstoneSprings;
            } else if (n < 0.20f) {
                biome = BiomeType.StonyPeaks;
            } else if (landN < 0.46f) {
                biome = BiomeType.Meadow;
            } else if (landN < 0.54f) {
                biome = BiomeType.Forest;
            } else {
                biome = BiomeType.Desert;
            }

                        chunk = new Chunk(coord, biome);
            
            // Feature Generation (Reduced density to prevent "piling")
            int fHash = (chunkX * 73856093) ^ (chunkY * 19349663);
            int roll = Math.Abs(fHash) % 1000; // Switch to 1000 for finer control

            if (biome == BiomeType.Forest)
            {
                if (roll < 25) // 2.5% density
                {
                    int sub = Math.Abs(fHash >> 8) % 100;
                    if (sub < 60) chunk.Feature = FeatureType.SmallTree;
                    else if (sub < 90) chunk.Feature = FeatureType.LargeTree;
                    else chunk.Feature = FeatureType.Stone;
                }
            }
            else if (biome == BiomeType.Meadow)
            {
                if (roll < 40) // 4% density
                {
                    int sub = Math.Abs(fHash >> 8) % 100;
                    chunk.Feature = (sub < 30) ? FeatureType.MeadowHedge : FeatureType.MeadowFlowers;
                }
            }
            else if (biome == BiomeType.Desert)
            {
                if (roll < 15) // 1.5% density
                {
                    int sub = Math.Abs(fHash >> 8) % 100;
                    if (sub < 50) chunk.Feature = FeatureType.Tumbleweed;
                    else if (sub < 85) chunk.Feature = FeatureType.DesertLog;
                    else if (sub < 95) chunk.Feature = FeatureType.PalmTree;
                    else chunk.Feature = FeatureType.OasisDesert;
                }
            }
            else if (biome == BiomeType.Beach)
            {
                if (roll < 10) chunk.Feature = (Math.Abs(fHash >> 8) % 10 < 8) ? FeatureType.PalmTree : FeatureType.BeachUmbrella;
            }
            else if (biome == BiomeType.StonyPeaks)
            {
                if (roll < 30) chunk.Feature = FeatureType.Stone;
            }
            else if (biome == BiomeType.Ocean)
            {
                if (roll < 2) chunk.Feature = FeatureType.Sailboat;
            }
            else if (biome == BiomeType.BrimstoneSprings)
            {
                if (roll < 20) chunk.Feature = (Math.Abs(fHash >> 8) % 10 < 4) ? FeatureType.SulfurSpring : FeatureType.Stone;
            }

            Chunks[coord] = chunk;

        }
        return chunk;
    }

    public BiomeType GetBiomeAtWorldPos(float x, float y)
    {
        int chunkX = (int)MathF.Floor(x / ChunkSize);
        int chunkY = (int)MathF.Floor(y / ChunkSize);
        return GetOrGenerateChunk(chunkX, chunkY).Biome;
    }

    public Chunk? GetChunk(int chunkX, int chunkY)
    {
        var coord = new ChunkCoord(chunkX, chunkY);
        if (Chunks.TryGetValue(coord, out var chunk)) return chunk;
        return null;
    }
}