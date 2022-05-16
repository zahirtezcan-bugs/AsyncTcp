using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace AsyncTcpClient;

internal class Client : BackgroundService
{
    private const int BufferSize = 24;
    private static readonly IPAddress Address = IPAddress.Parse("127.0.0.1");
    private const int Port = 13337;

    private readonly ILogger<Client> logger;

    public Client(ILogger<Client> logger)
    {
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var client = new TcpClient();

                await Connect(client, cancellation);

                if (client.Connected)
                {
                    logger.LogInformation("Connected to server");
                    await Operate(client, cancellation);
                }
            }
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Unexpected error while initializing client");
        }
    }

    private async Task Connect(TcpClient client, CancellationToken cancellation)
    {
        try
        {
            await client.ConnectAsync(Address, Port, cancellation);
        }
        catch (OperationCanceledException)
        { }
        catch (SocketException)
        {
            logger.LogWarning("Unable to connect to server");
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Unexpected error while trying to connect");
        }
    }

    private async Task Operate(TcpClient client, CancellationToken cancellation)
    {
        Stream stream = client.GetStream();
        var buffer = new byte[BufferSize];
        var remoteEp = client.Client.RemoteEndPoint;

        if (!await SendSome(stream, remoteEp, buffer, cancellation))
        {
            return;
        }

        while (!cancellation.IsCancellationRequested && client.Connected)
        {
            try
            {
                int readCount = await ReadAll(stream, remoteEp, buffer, cancellation);
                if (readCount == 0)
                {
                    logger.LogWarning("Cannot read from {remoteEp}, IsConnected: {isConnected}", remoteEp, client.Client.Connected);
                }
                else if (readCount != BufferSize)
                {
                    logger.LogWarning("Partially read {readCount} from {remoteEp}, IsConnected: {isConnected}", readCount, remoteEp, client.Client.Connected);
                }
                else
                {
                    logger.LogInformation("Received {readCount} bytes from {remoteEp}", readCount, remoteEp);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Read operation has been canceled; IsConnected: {isConnected}", client.Connected);
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "Unexpected error while reading from client");
            }
        }
    }

    private async Task<bool> SendSome(Stream stream, EndPoint? remoteEp, Memory<byte> buffer, CancellationToken cancellation)
    {
        try
        {
            await stream.WriteAsync(buffer, cancellation);
            return true;
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Unexpected error while trying to send {bufferLength} bytes to {remoteEp}", buffer.Length, remoteEp);
        }

        return false;
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