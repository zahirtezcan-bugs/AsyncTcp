using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace AsyncTcpServer;

internal class Server : BackgroundService
{
    private const int BufferSize = 24;
    private static readonly IPAddress Address = IPAddress.Parse("127.0.0.1");
    private const int Port = 13337;

    private readonly ILogger<Server> logger;

    public Server(ILogger<Server> logger)
    {
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        TcpListener? listener = null;

        try
        {
            listener = new TcpListener(Address, Port);
            listener.Start();

            await AcceptClients(listener, cancellation);
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Unexpected error while trying to initialize listener");
        }
        finally
        {
            if (listener != null)
            {
                listener.Stop();
            }
        }
    }

    private async Task AcceptClients(TcpListener listener, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellation);
                logger.LogInformation("Accepted a client from {remoteEp}", client.Client.RemoteEndPoint);
                var _ = Task.Run(() => Serve(client, cancellation));
            }
            catch (OperationCanceledException)
            { }
            catch (Exception exc)
            {
                logger.LogError(exc, "Unexpected error while accepting a client");
            }
        }
    }

    private async Task Serve(TcpClient client, CancellationToken cancellation)
    {
        Stream stream = client.GetStream();
        var buffer = new byte[BufferSize];
        var remoteEp = client.Client.RemoteEndPoint;

        while (!cancellation.IsCancellationRequested && client.Connected)
        {
            try
            {
                int readCount = await ReadAll(stream, remoteEp, buffer, cancellation);
                if (readCount == 0)
                {
                    logger.LogWarning("{time} Cannot read from {remoteEp}, IsConnected: {isConnected}", DateTime.Now, remoteEp, client.Client.Connected);
                }
                else if (readCount != BufferSize)
                {
                    logger.LogWarning("Partially read {readCount} from {remoteEp}, IsConnected: {isConnected}", readCount, remoteEp, client.Client.Connected);

                    await stream.WriteAsync(buffer[0..readCount], cancellation);
                }
                else
                {
                    logger.LogInformation("Received {readCount} bytes from {remoteEp}", readCount, remoteEp);
                    await stream.WriteAsync(buffer, cancellation);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Read operation has been canceled");
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "Unexpected error while reading from client");
            }
        }
    }

    private async Task<int> ReadAll(Stream stream, EndPoint? remoteEp, Memory<byte> buffer, CancellationToken cancellation)
    {
        int readCount = await stream.ReadAsync(buffer, cancellation);
        if (readCount == 0 || readCount == buffer.Length)
        {
            return readCount;
        }

        var remaining = buffer[readCount..];
        while (!cancellation.IsCancellationRequested)
        {
            readCount = await stream.ReadAsync(remaining, cancellation);
            if (readCount == 0 || readCount == remaining.Length)
            {
                return buffer.Length - remaining.Length + readCount;
            }

            remaining = remaining[readCount..];
        }

        return buffer.Length - remaining.Length;
    }
}
