using System.Net;
using System.Net.Sockets;

class Program
{
    public static World BulletboxWorld = new World();
    // Keep a list of all active players for broadcasting
    public static List<Player> ConnectedPlayers = new List<Player>();

    static async Task Main()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 32308);
        listener.Start();
        Console.WriteLine("Server Started on 32308...");

        while (true)
        {
            TcpClient clientSocket = await listener.AcceptTcpClientAsync();
            Console.WriteLine("New raw connection detected...");

            Player newPlayer = new Player(clientSocket);
            
            lock(ConnectedPlayers) { ConnectedPlayers.Add(newPlayer); }
            
            // Use Task.Run to ensure the listener doesn't block the next connection
            _ = Task.Run(async () => {
                await newPlayer.Listen(BulletboxWorld);
                
                // Cleanup when they leave
                lock(ConnectedPlayers) { ConnectedPlayers.Remove(newPlayer); }
            });
        }
    }
}