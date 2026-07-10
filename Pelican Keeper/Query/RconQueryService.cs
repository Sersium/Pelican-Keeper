using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Pelican_Keeper.Query;

/// <summary>
/// Queries game servers using RCON protocol.
/// </summary>
public sealed class RconQueryService : IQueryService
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _password;
    private int _packetId;
    private bool _isAuthenticated;
    private bool _disposed;

    /// <inheritdoc />
    public string Ip { get; set; }

    /// <inheritdoc />
    public int Port { get; set; }

    private const int AuthPacket = 3;
    private const int AuthResponse = 2;
    private const int ExecCommand = 2;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Initializes a new RCON query service.
    /// </summary>
    public RconQueryService(string ip, int port, string password)
    {
        Ip = ip;
        Port = port;
        _password = password;
    }

    /// <inheritdoc />
    public async Task ConnectAsync()
    {
        if (_isAuthenticated && _client?.Connected == true) return;

        _client = new TcpClient { ReceiveTimeout = 5000, SendTimeout = 5000 };

        try
        {
            var connectTask = _client.ConnectAsync(Ip, Port);
            var timeoutTask = Task.Delay(ConnectTimeout);

            if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                throw new TimeoutException($"RCON connection timed out for {Ip}:{Port}");

            await connectTask;
            _stream = _client.GetStream();
            _isAuthenticated = await AuthenticateAsync();
        }
        catch (Exception)
        {
            Cleanup();
        }
    }

    /// <inheritdoc />
    public async Task<string> QueryAsync() => await QueryAsync("status", null);

    /// <summary>
    /// Executes an RCON command and optionally extracts data using regex.
    /// </summary>
    public async Task<string> QueryAsync(string command, string? extractRegex)
    {
        if (!_isAuthenticated || _stream == null) return "N/A";

        try
        {
            var packet = BuildPacket(ExecCommand, command);
            await _stream.WriteAsync(packet);

            var response = new StringBuilder();
            do
            {
                var buffer = await ReadPacketAsync();
                if (buffer == null || buffer.Length < 14) break;

                var packetSize = BitConverter.ToInt32(buffer, 0);
                var bodyLength = packetSize - 10;
                if (bodyLength > 0)
                    response.Append(Encoding.UTF8.GetString(buffer, 12, bodyLength));
            } while (_stream.DataAvailable);

            var responseText = response.ToString();
            if (extractRegex != null)
            {
                var match = Regex.Match(responseText, extractRegex);
                return match.Success ? match.Groups[1].Value : "N/A";
            }

            return responseText;
        }
        catch
        {
            return "N/A";
        }
    }

    private async Task<bool> AuthenticateAsync()
    {
        if (_stream == null) return false;

        try
        {
            var authPacket = BuildPacket(AuthPacket, _password);
            var authId = _packetId;
            await _stream.WriteAsync(authPacket);

            for (var i = 0; i < 2; i++)
            {
                var buffer = await ReadPacketAsync();
                if (buffer == null || buffer.Length < 12) return false;

                var responseId = BitConverter.ToInt32(buffer, 4);
                var responseType = BitConverter.ToInt32(buffer, 8);
                if (responseId == -1) return false;
                if (responseType == AuthResponse && responseId == authId) return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private byte[] BuildPacket(int type, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var packetSize = 10 + bodyBytes.Length;
        var packet = new byte[4 + packetSize];

        BitConverter.GetBytes(packetSize).CopyTo(packet, 0);
        BitConverter.GetBytes(++_packetId).CopyTo(packet, 4);
        BitConverter.GetBytes(type).CopyTo(packet, 8);
        bodyBytes.CopyTo(packet, 12);

        return packet;
    }

    private async Task<byte[]?> ReadPacketAsync()
    {
        if (_stream == null) return null;

        var sizeBytes = new byte[4];
        if (!await ReadExactlyAsync(sizeBytes)) return null;

        var packetSize = BitConverter.ToInt32(sizeBytes, 0);
        if (packetSize < 10 || packetSize > 65535) return null;

        var payload = new byte[packetSize];
        if (!await ReadExactlyAsync(payload)) return null;

        var packet = new byte[4 + packetSize];
        sizeBytes.CopyTo(packet, 0);
        payload.CopyTo(packet, 4);
        return packet;
    }

    private async Task<bool> ReadExactlyAsync(byte[] buffer)
    {
        if (_stream == null) return false;

        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset));
            if (read == 0) return false;
            offset += read;
        }

        return true;
    }

    private void Cleanup()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _isAuthenticated = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        _disposed = true;
    }
}
