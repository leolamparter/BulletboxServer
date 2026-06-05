using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BulletboxClient; // To access Structure and StructureType

namespace BulletboxClient.Networking
{
    public class NetworkManager
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        public ConcurrentDictionary<(int, int), byte> ChunkBiomes = new();
        public ConcurrentDictionary<(int, int), byte> ChunkFeatures = new();
        public readonly object ChunkBiomesLock = new(); // Used by Playing.cs

        // New: Structures
        public ConcurrentDictionary<(int, int), Structure> Structures = new();
        public readonly object StructuresLock = new();

        public bool IsConnected() { return _client?.Connected ?? false; }

        // Placeholder methods (actual implementation would involve writing to _writer)
        public void SendChunkRequest(int cx, int cy)
        {
            // Example:
            // lock (_writerLock) { _writer.Write((byte)10); _writer.Write(cx); _writer.Write(cy); _writer.Flush(); }
        }

        public void SendRenderDistance(int radius)
        {
            // Placeholder
        }

        public void SendBlockingState(bool isBlocking)
        {
            // Placeholder
        }

        public void SendChat(string message)
        {
            // Placeholder
        }

        public void SendPosition(float x, float y, float rotation)
        {
            // Placeholder
        }

        public void SendSlotSwap(byte slot)
        {
            // Placeholder
        }

        public void SendAttack(string victimName)
        {
            // Placeholder
        }

        // This method would be called by the client's main network listening loop
        // to process incoming packets.
        public void ProcessIncomingPacket(byte packetId, BinaryReader reader)
        {
            // Existing packet handling (e.g., for chunk biomes/features) would go here.
            // For this example, we only add the structure packet handling.

            if (packetId == 10) // Chunk Data (existing)
            {
                int chunkX = reader.ReadInt32();
                int chunkY = reader.ReadInt32();
                byte biome = reader.ReadByte();
                byte feature = reader.ReadByte();
                lock (ChunkBiomesLock)
                {
                    ChunkBiomes[(chunkX, chunkY)] = biome;
                    ChunkFeatures[(chunkX, chunkY)] = feature;
                }
            }
            else if (packetId == 12) // Structure Data (new)
            {
                int chunkX = reader.ReadInt32();
                int chunkY = reader.ReadInt32();
                StructureType type = (StructureType)reader.ReadByte();
                float posX = reader.ReadSingle();
                float posY = reader.ReadSingle();
                lock (StructuresLock)
                {
                    Structures[(chunkX, chunkY)] = new Structure(new Vector2(posX, posY), type, chunkX, chunkY);
                }
            }
            // ... handle other packet IDs
        }
    }
}