using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

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

    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    public BinaryWriter Writer;

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
                    _reader.ReadString(); // password
                    
                    world.UpdatePosition(Username, 400, 300);
                    
                    // 1. Setup starting items
                    Inventory[0] = new ItemStack((byte)'A', 5);
                    Inventory[2] = new ItemStack((byte)'D', 30);

                    // 2. Send Login Success
                    Writer.Write((byte)0);
                    Writer.Write(true);
                    
                    // 3. Send Initial Inventory
                    SendFullInventory();
                    
                    Console.WriteLine($"[Handshake] {Username} is in.");
                }
                else if (packetId == 1) // Move Player
                {
                    float x = _reader.ReadSingle();
                    float y = _reader.ReadSingle();
                    world.UpdatePosition(Username, x, y);
                    BroadcastMove(Username, x, y);
                }
                else if (packetId == 3) // Move Item Request
                {
                    byte from = _reader.ReadByte();
                    byte to = _reader.ReadByte();
                    
                    // Swap items in server memory
                    ItemStack temp = Inventory[from];
                    Inventory[from] = Inventory[to];
                    Inventory[to] = temp;

                    // Sync change back to client
                    SendFullInventory();
                }
                // Inside Player.Listen() loop
                else if (packetId == 6) // Attack Packet
                {
                    string victimName = _reader.ReadString();

                    // 1. Find the victim in the connected players list
                    Player victim = Program.ConnectedPlayers.Find(p => p.Username == victimName);

                    if (victim != null && victim.Username != this.Username)
                    {
                        // 2. Get positions from the World source of truth
                        Vector2 myPos = world.PlayerLocations[this.Username];
                        Vector2 victimPos = world.PlayerLocations[victim.Username];

                        // 3. Distance Check (400 units)
                        float distance = Vector2.Distance(myPos, victimPos);

                        if (distance <= 400f)
                        {
                            // 4. Apply damage using the authoritative method we made
                            victim.TakeDamage(5); 
                        }
                        else 
                        {
                            Console.WriteLine($"[PVP] {Username} tried to attack {victimName} but was too far! ({distance})");
                        }
                    }
                }
            }
        }
        catch (Exception e) { Console.WriteLine($"Client Error: {e.Message}"); }
        finally { world.RemovePlayer(Username); _client.Close(); }
    }

    public void SendFullInventory() {
        Writer.Write((byte)4); // Packet ID 4: Sync
        for (int i = 0; i < 24; i++) {
            Writer.Write(Inventory[i].ItemID);
            Writer.Write(Inventory[i].Count);
        }
        Writer.Flush();
    }

    private void BroadcastMove(string name, float x, float y)
    {
        foreach (var p in Program.ConnectedPlayers)
        {
            try {
                if (p.Username == name) continue; 
                p.Writer.Write((byte)1);
                p.Writer.Write(name);
                p.Writer.Write(x);
                p.Writer.Write(y);
                p.Writer.Flush();
            } catch { }
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
        Writer.Write((byte)5); // Packet ID 5: Health Sync
        Writer.Write(Health);
        Writer.Write(MaxHealth);
        Writer.Flush();
    }
}