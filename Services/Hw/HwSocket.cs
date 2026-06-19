using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CMS5000.Services.Hw;

/// <summary>
/// 원본 cSocket 포팅: 랙 하드웨어와의 TCP 통신(연결/해제/송신/수신).
/// 콜백(Connected/Disconnected/Sent/Received/Error)은 호출 스레드에서 발생하므로
/// UI 갱신은 Dispatcher 로 마샬링한다.
/// </summary>
public sealed class HwSocket : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;

    public string  Host { get; private set; } = "";
    public int     Port { get; private set; }
    public bool    IsConnected => _client?.Connected == true;

    public event Action?            Connected;
    public event Action?            Disconnected;
    public event Action<int>?       Sent;
    public event Action<HwPacket, byte[]>? Received;
    public event Action<string>?    Error;

    /// <summary>TCP 연결 시도 제한 시간(초). 닫힌/필터된 포트에서 OS 기본(~21초) 대기 대신 빠르게 실패.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    public async void Connect(string host, int port)
    {
        Host = host; Port = port;
        try
        {
            _client = new TcpClient();
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
            await _client.ConnectAsync(host, port, connectCts.Token);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            Connected?.Invoke();
            _ = ReceiveLoopAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { _client?.Dispose(); } catch { /* ignore */ }
            _client = null;
            Error?.Invoke($"연결 실패: {host}:{port} 응답 없음(시간 초과 {ConnectTimeoutSeconds}s). " +
                          "장비 전원/네트워크와 해당 TCP 포트 개방 여부를 확인하세요. (ping 성공만으로는 포트 개방을 보장하지 않습니다)");
        }
        catch (Exception ex)
        {
            try { _client?.Dispose(); } catch { /* ignore */ }
            _client = null;
            Error?.Invoke($"연결 실패: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _stream?.Close();
            _client?.Close();
        }
        catch { /* ignore */ }
        finally
        {
            _stream = null; _client = null;
            Disconnected?.Invoke();
        }
    }

    /// <summary>패킷(헤더+선택적 payload)을 전송한다.</summary>
    public async void Send(byte[] header, byte[]? payload = null)
    {
        if (_stream == null) { Error?.Invoke("연결되어 있지 않습니다."); return; }
        try
        {
            await _stream.WriteAsync(header);
            if (payload is { Length: > 0 }) await _stream.WriteAsync(payload);
            await _stream.FlushAsync();
            Sent?.Invoke(header.Length + (payload?.Length ?? 0));
        }
        catch (Exception ex)
        {
            Error?.Invoke($"전송 실패: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var head = new byte[HwPacket.HeaderSize];
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                if (!await ReadExactAsync(head, head.Length, ct)) break;
                var pk = HwPacket.Parse(head);
                if (pk == null) continue;

                byte[] payload = [];
                if (pk.Length > 0)
                {
                    payload = new byte[pk.Length];
                    if (!await ReadExactAsync(payload, payload.Length, ct)) break;
                }
                Received?.Invoke(pk, payload);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Error?.Invoke($"수신 오류: {ex.Message}");
        }
        finally
        {
            if (!ct.IsCancellationRequested) Disconnect();
        }
    }

    private async Task<bool> ReadExactAsync(byte[] buf, int len, CancellationToken ct)
    {
        int read = 0;
        while (read < len)
        {
            int n = await _stream!.ReadAsync(buf.AsMemory(read, len - read), ct);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    public void Dispose() => Disconnect();
}
