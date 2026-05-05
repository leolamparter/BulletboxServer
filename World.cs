using System.Collections.Concurrent;
using System.Numerics;

public class World
{
    // Key: Username, Value: Last known position
    public ConcurrentDictionary<string, Vector2> PlayerLocations = new();

    public void UpdatePosition(string username, float x, float y)
    {
        PlayerLocations[username] = new Vector2(x, y);
    }

    public void RemovePlayer(string username)
    {
        PlayerLocations.TryRemove(username, out _);
    }
}