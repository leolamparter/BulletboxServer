using System.Collections.Concurrent;
using System.Numerics;

public enum BiomeType
{
    Meadow,
    Forest
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
            // Use Perlin noise for smooth biome transitions
            float scale = 0.08f; // Lower = larger biomes
            float noise = Perlin.Noise(chunkX * scale, chunkY * scale);
            // Normalize noise to [0,1]
            float n = (noise + 1f) * 0.5f;
            var biome = n < 0.5f ? BiomeType.Meadow : BiomeType.Forest;
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