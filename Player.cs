using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Numerics;

// Data structure must match client exactly
public struct ItemStack {
    public byte ItemID;
    public int Count;
    public ItemStack(byte id, int count) { ItemID = id; Count = count; }
}

public class Player
{

    public string Username = "";
    public int Health = 100;
    public int MaxHealth = 100;
    public float Rotation = 0f;

    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    public BinaryWriter Writer;
    private DateTime _lastAttackTime = DateTime.MinValue;
    private DateTime _lastHitTime = DateTime.MinValue;
    public int SelectedSlot = 0;

    public readonly object WriterLock = new();

    // The Server's source of truth
    public ItemStack[] Inventory = new ItemStack[24];

    public Player(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new BinaryReader(_stream);
        Writer = new BinaryWriter(_stream);

        // Initialize empty
        for (int i = 0; i < 24; i++) Inventory[i] = new ItemStack((byte)' ', 0);
    }

    public async Task Listen(World world)
    {
        try
        {
            while (_client.Connected)
            {
                byte packetId = _reader.ReadByte();

                if (packetId == 0) // Login
                {
                    Username = _reader.ReadString();
                    string clientVer = _reader.ReadString();
                    _reader.ReadString(); // password
                    
                    world.UpdatePosition(Username, 400, 300);
                    
                    Inventory[0] = new ItemStack((byte)'K', 1); 
                    Inventory[1] = new ItemStack((byte)'S', 1);
                    Inventory[2] = new ItemStack((byte)'D', 1);
                    Inventory[3] = new ItemStack((byte)'P', 1);

                    lock (WriterLock)
                    {
                        Writer.Write((byte)0);
                        Writer.Write(true);
                        SendFullInventory();
                    }
                    Console.WriteLine($"[Handshake] {Username} is in.");
                }
                else if (packetId == 1) // Move Player
                {
                    float x = _reader.ReadSingle();
                    float y = _reader.ReadSingle();
                    Rotation = _reader.ReadSingle();
                    world.UpdatePosition(Username, x, y);
                    BroadcastMove(Username, x, y, Rotation, Inventory[SelectedSlot].ItemID);
                }
                else if (packetId == 2) // Slot Selection
                {
                    byte slot = _reader.ReadByte();
                    if (slot < 24) SelectedSlot = slot;
                }
                else if (packetId == 3) // Move Item Request
                {
                    byte from = _reader.ReadByte();
                    byte to = _reader.ReadByte();
                    
                    if (from < 24 && to < 24)
                    {
                        // Swap items in server memory
                        ItemStack temp = Inventory[from];
                        Inventory[from] = Inventory[to];
                        Inventory[to] = temp;
                        SendFullInventory();
                    }
                }
                else if (packetId == 10) // Chunk Request (NEW)
                {
                    int chunkX = _reader.ReadInt32();
                    int chunkY = _reader.ReadInt32();
                    var chunk = world.GetOrGenerateChunk(chunkX, chunkY);
                    // Respond with chunk data
                    lock (WriterLock)
                    {
                        Writer.Write((byte)10); // Packet ID 10: Chunk Data
                        Writer.Write(chunk.Coord.X);
                        Writer.Write(chunk.Coord.Y);
                        Writer.Write((byte)chunk.Biome);
                        Writer.Write((byte)chunk.Feature);
                        Writer.Flush();
                    }
                }
                else if (packetId == 6) {
                    string victimName = _reader.ReadString();
                    byte heldId = Inventory[SelectedSlot].ItemID; 

                    float elapsed = (float)(DateTime.Now - _lastAttackTime).TotalSeconds;
                    float timeSinceHit = (float)(DateTime.Now - _lastHitTime).TotalSeconds;

                    var (dmg, kb, range) = WeaponStats.Calculate(heldId, elapsed, timeSinceHit);

                    if (dmg > 0) {
                        Player? victim;
                        lock (Program.ConnectedPlayers)
                        {
                            victim = Program.ConnectedPlayers.Find(p => p.Username == victimName);
                        }

                        if (victim != null) {
                            Vector2 myPos = world.PlayerLocations[this.Username];
                            Vector2 victimPos = world.PlayerLocations[victim.Username];
                            float dist = Vector2.Distance(myPos, victimPos);

                            if (dist <= range) {
                                _lastAttackTime = DateTime.Now; // Log the attempt
                                _lastHitTime = DateTime.Now;   // Log the success for combo
                                
                                victim.Damage((int)dmg);

                                // Apply Knockback
                                if (Math.Abs(kb) > 0.1f) {
                                    Vector2 dir = Vector2.Normalize(victimPos - myPos);
                                    lock (victim.WriterLock)
                                    {
                                        victim.Writer.Write((byte)7); // Packet ID 7: Knockback
                                        victim.Writer.Write(dir.X * kb);
                                        victim.Writer.Write(dir.Y * kb);
                                        victim.Writer.Flush();
                                    }
                                }
                            }
                        }
                    }
                    else {
                        // Even if they do 0 damage, we reset attack time so they can't spam
                        _lastAttackTime = DateTime.Now;
                    }
                }
                else if (packetId == 8) // Chat Message
                {
                    string msg = _reader.ReadString();
                    Console.WriteLine($"[Chat] {Username}: {msg}");
                    BroadcastChat(Username, msg);
                }
            }
        }
        catch (Exception e) { Console.WriteLine($"Client Error: {e.Message}"); }
        finally { world.RemovePlayer(Username); _client.Close(); }
    }

    public void SendFullInventory() {
        lock (WriterLock)
        {
            Writer.Write((byte)4); // Packet ID 4: Sync
            for (int i = 0; i < 24; i++) {
                Writer.Write(Inventory[i].ItemID);
                Writer.Write(Inventory[i].Count);
            }
            Writer.Flush();
        }
    }

    private void BroadcastMove(string name, float x, float y, float rot, byte heldItemId)
    {
        List<Player> playersToNotify;
        lock (Program.ConnectedPlayers)
        {
            playersToNotify = new List<Player>(Program.ConnectedPlayers);
        }

        foreach (var p in playersToNotify)
        {
            try {
                if (p.Username == name) continue; 
                lock (p.WriterLock)
                {
                    p.Writer.Write((byte)1);
                    p.Writer.Write(name);
                    p.Writer.Write(x);
                    p.Writer.Write(y);
                    p.Writer.Write(rot);
                    p.Writer.Write(heldItemId);
                    p.Writer.Flush();
                }
            } catch { }
        }
    }

    private void BroadcastChat(string sender, string message)
    {
        List<Player> playersToNotify;
        lock (Program.ConnectedPlayers)
        {
            playersToNotify = new List<Player>(Program.ConnectedPlayers);
        }

        foreach (var p in playersToNotify)
        {
            try
            {
                lock (p.WriterLock)
                {
                    p.Writer.Write((byte)8); // Packet ID 8: Chat
                    p.Writer.Write(sender);
                    p.Writer.Write(message);
                    p.Writer.Flush();
                }
            }
            catch { }
        }
    }

    public void Damage(int amount)
    {
        Health -= amount;
        if (Health < 0) Health = 0;
        
        Console.WriteLine($"[Combat] {Username} took {amount} damage. Health: {Health}");
        SyncHealth();

        if (Health <= 0) OnDeath();
    }

    public void Heal(int amount)
    {
        Health += amount;
        if (Health > MaxHealth) Health = MaxHealth;
        
        SyncHealth();
    }

    private void OnDeath()
    {
        Console.WriteLine($"[World] {Username} has died.");
        // Basic respawn logic
        Health = MaxHealth;
        SyncHealth();
        // You could also reset position here
    }

    // Tells the client what their health actually is
    public void SyncHealth()
    {
        if (!_client.Connected) return;
        lock (WriterLock)
        {
            Writer.Write((byte)5); // Packet ID 5: Health Sync
            Writer.Write(Health);
            Writer.Write(MaxHealth);
            Writer.Flush();
        }
    }
}