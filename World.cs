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
    public Chunk(ChunkCoord coord, BiomeType biome)
    {
        Coord = coord;
        Biome = biome;
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