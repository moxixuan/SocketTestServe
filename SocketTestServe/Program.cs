// See https://aka.ms/new-console-template for more information

using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Sockets;
using System.Text;

var portRange = Enumerable.Range(6660, 10); // Ports from 6660 to 6669
var listeners = new List<TcpListener>();
var totalReadBytes = 0L;
int ChunkIndex = 0;

CancellationTokenSource source = new CancellationTokenSource();
CancellationToken token = source.Token;

// Create and start a TcpListener for each port
Parallel.ForEach(portRange, port =>
{
    IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Any, port);
    TcpListener listener = new TcpListener(ipEndpoint);
    listeners.Add(listener);
    listener.Start();

    // var mapName = Interlocked.Increment(ref ChunkIndex);
    using (var memoryMappedFile = MemoryMappedFile.CreateNew(null, 24 * 1024 * 1024))
    {
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(async () =>
                    {
                        client.ReceiveTimeout = client.SendTimeout = 0;
                        NetworkStream stream = client.GetStream();
                        stream.ReadTimeout = stream.WriteTimeout = Timeout.Infinite;
                        var currentReceivedBuffer = 0;
                        // using var viewAccessor = memoryMappedFile.CreateViewAccessor();

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var buffer = new byte[1024 * 1024];
                                int received = await stream.ReadAsync(buffer, 0, buffer.Length)
                                    .ConfigureAwait(false);
                                if (received > 0)
                                {
                                    Interlocked.Add(ref totalReadBytes, received);
                                    // viewAccessor.WriteArray(currentReceivedBuffer, buffer, 0, received);
                                    Interlocked.Add(ref currentReceivedBuffer, received);
                                    // Console.WriteLine($"totalReadBytes: {totalReadBytes}");
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch (SocketException se)
                            {
                                if (se.SocketErrorCode == SocketError.ConnectionReset ||
                                    se.SocketErrorCode == SocketError.Shutdown)
                                {
                                    break;
                                }

                                Console.WriteLine($"Socket exception caught: {se.Message}");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Unexpected exception caught: {e.Message}");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to accept a client: {ex.Message}");
                }
            }
        });
    }
});

// Wait for a key press or cancellation
Console.WriteLine("Press any key to stop listening...");
Console.ReadKey();
Console.WriteLine($"totalReadBytes: {totalReadBytes}");
source.Cancel();

// Stop all listeners
foreach (var listener in listeners)
{
    listener.Stop();
}


/*
using (CancellationTokenSource source = new CancellationTokenSource())
{
    var ipEndPoint = new IPEndPoint(IPAddress.Any, 6663);
    TcpListener Listener = new(ipEndPoint);
    // long len = 24L * 1024L * 1024L;
    long totalReadBytes = 0;
    // Start the TcpListener
    // TcpListener Listener = new TcpListener(IPAddress.Any, this.ExternalPort);
    Listener.Start();

    var Token = source.Token;

    // Continually wait for new client
    while (!Token.IsCancellationRequested)
    {
        // Handle the client asynchronously in a new thread
        TcpClient client = await Listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            client.ReceiveTimeout = client.SendTimeout = 0;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = Timeout.Infinite;
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[1_024];
                    int received = await stream.ReadAsync(buffer);
                    if (received > 0)
                    {
                        Interlocked.Add(ref totalReadBytes, received);
                        Console.WriteLine("totalReadBytes: {0}", totalReadBytes);
                    }
                    else
                    {
                        // 如果接收到的数据长度为0，这通常意味着连接已关闭
                        break;
                    }
                }
                catch (SocketException se)
                {
                    // 如果是由于连接关闭导致的SocketException，可以安全地忽略它
                    if (se.SocketErrorCode == SocketError.ConnectionReset ||
                        se.SocketErrorCode == SocketError.Shutdown)
                    {
                        break;
                    }
                    else
                    {
                        // 对于其他类型的SocketException，可能需要记录错误日志
                        Console.WriteLine($"Socket exception caught: {se.Message}");
                    }
                }
                catch (Exception e)
                {
                    // 捕获所有其他类型的异常
                    Console.WriteLine($"Unexpected exception caught: {e.Message}");
                }
            }
        });
    }
}
*/

// IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
// var ipEndPoint = new IPEndPoint(ipAddress, 6663);
//
// using TcpClient client = new();
// await client.ConnectAsync(ipEndPoint);
// await using NetworkStream stream = client.GetStream();
//
// var buffer = new byte[1_024];
// int received = await stream.ReadAsync(buffer);
//
// var message = Encoding.UTF8.GetString(buffer, 0, received);
// Console.WriteLine($"Message received: \"{message}\"");
// // Sample output:
// //     Message received: "📅 8/22/2022 9:07:17 AM 🕛"