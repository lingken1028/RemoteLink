using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RemoteLink.Host;

public sealed class PairingServer : IAsyncDisposable
{
    public const int Port = 43821;
    private readonly object _gate = new();
    private WebApplication? _app;
    private string _pairCode = "";
    private DateTimeOffset _codeExpiresAt;
    private byte[]? _sessionToken;
    private string? _clientName;
    private bool _desktopPreviewEnabled;
    private DateTimeOffset _lastDesktopFrameAt = DateTimeOffset.MinValue;
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Window)> _attempts = new();

    public event Action<string>? StatusChanged;
    public string PairCode { get { lock (_gate) return _pairCode; } }
    public DateTimeOffset CodeExpiresAt { get { lock (_gate) return _codeExpiresAt; } }
    public bool IsRunning => _app is not null;
    public bool DesktopPreviewEnabled { get { lock (_gate) return _desktopPreviewEnabled; } }
    public bool HasPairedClient { get { lock (_gate) return _sessionToken is not null; } }

    public void SetDesktopPreviewEnabled(bool enabled)
    {
        lock (_gate) _desktopPreviewEnabled = enabled && _sessionToken is not null;
        StatusChanged?.Invoke(DesktopPreviewEnabled ? "桌面預覽已啟用；已配對用戶端可查看畫面。" : "桌面預覽已停止。");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null) return;
        lock (_gate)
        {
            _pairCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _codeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            _sessionToken = null;
            _clientName = null;
            _desktopPreviewEnabled = false;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new
        {
            service = "RemoteLink",
            pairingEnabled = true,
            hostName = Environment.MachineName
        }));

        app.MapPost("/pair", (PairRequest request, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTimeOffset.UtcNow;
            var attempt = _attempts.AddOrUpdate(ip,
                _ => (1, now),
                (_, old) => now - old.Window > TimeSpan.FromMinutes(1)
                    ? (1, now)
                    : (old.Count + 1, old.Window));

            if (attempt.Count > 8)
                return (IResult)Results.Json(new
                {
                    error = "too_many_attempts",
                    message = "嘗試次數過多，請稍後再試。"
                }, statusCode: 429);

            string pairedClient;
            string token;
            lock (_gate)
            {
                if (_sessionToken is not null)
                    return (IResult)Results.Conflict(new
                    {
                        error = "already_paired",
                        message = "主機已有連線中的用戶端。"
                    });
                if (now > _codeExpiresAt)
                    return (IResult)Results.Json(new
                    {
                        error = "code_expired",
                        message = "配對碼已過期，請在主機重新啟動配對。"
                    }, statusCode: 410);

                var supplied = Encoding.ASCII.GetBytes(request.Code ?? "");
                var expected = Encoding.ASCII.GetBytes(_pairCode);
                if (supplied.Length != expected.Length ||
                    !CryptographicOperations.FixedTimeEquals(supplied, expected))
                    return (IResult)Results.Unauthorized();

                _sessionToken = RandomNumberGenerator.GetBytes(32);
                var requestedName = request.ClientName?.Trim();
                _clientName = string.IsNullOrWhiteSpace(requestedName)
                    ? "Android client"
                    : requestedName[..Math.Min(requestedName.Length, 64)];
                _pairCode = "";
                pairedClient = _clientName;
                token = Convert.ToBase64String(_sessionToken);
            }

            StatusChanged?.Invoke($"已配對：{pairedClient}");
            return (IResult)Results.Ok(new { token, hostName = Environment.MachineName });
        });

        app.MapGet("/session", (HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return (IResult)Results.Unauthorized();
                return (IResult)Results.Ok(new
                {
                    connected = true,
                    clientName = _clientName,
                    desktopPreviewEnabled = _desktopPreviewEnabled
                });
            }
        });

        app.MapPost("/session/close", (HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return (IResult)Results.Unauthorized();
                _sessionToken = null;
                _clientName = null;
                _desktopPreviewEnabled = false;
                _pairCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
                _codeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            }
            StatusChanged?.Invoke("用戶端已中斷；新配對碼有效 5 分鐘。");
            return (IResult)Results.Ok(new { disconnected = true });
        });

        app.MapPost("/input/mouse", (MouseRequest request, HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return Results.Unauthorized();
                if (!_desktopPreviewEnabled) return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var x = Math.Clamp(request.X, 0.0, 1.0);
            var y = Math.Clamp(request.Y, 0.0, 1.0);
            var screenWidth = GetSystemMetrics(0);
            var screenHeight = GetSystemMetrics(1);
            if (screenWidth <= 0 || screenHeight <= 0)
                return Results.Problem("無法取得 Windows 螢幕尺寸。", statusCode: 500);

            var px = Math.Clamp((int)Math.Round(x * (screenWidth - 1)), 0, screenWidth - 1);
            var py = Math.Clamp((int)Math.Round(y * (screenHeight - 1)), 0, screenHeight - 1);
            if (!SetCursorPos(px, py))
                return Results.Problem("無法移動 Windows 滑鼠游標。", statusCode: 500);

            switch ((request.Action ?? "").Trim().ToLowerInvariant())
            {
                case "move":
                    break;
                case "click":
                    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                    break;
                case "rightclick":
                    mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
                    break;
                default:
                    return Results.BadRequest(new { error = "unsupported_mouse_action" });
            }

            return Results.Ok(new { accepted = true, x = px, y = py });
        });

        app.MapGet("/desktop/info", (HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return Results.Unauthorized();
                if (!_desktopPreviewEnabled) return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            return Results.Ok(new { width = GetSystemMetrics(0), height = GetSystemMetrics(1) });
        });

        app.MapGet("/desktop.jpg", async (HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return Results.Unauthorized();
                if (!_desktopPreviewEnabled) return Results.StatusCode(StatusCodes.Status403Forbidden);
                var now = DateTimeOffset.UtcNow;
                if (now - _lastDesktopFrameAt < TimeSpan.FromMilliseconds(800))
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                _lastDesktopFrameAt = now;
            }

            try
            {
                var jpeg = await Task.Run(() => DesktopCapture.CaptureJpeg(), context.RequestAborted);
                return Results.File(jpeg, "image/jpeg");
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch
            {
                return Results.Problem("無法擷取桌面畫面。", statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        await app.StartAsync(cancellationToken);
        _app = app;
        StatusChanged?.Invoke("RemoteLink 主機已啟動，等待 Android 配對。");
    }

    public async Task StopAsync()
    {
        var app = _app;
        if (app is null) return;
        _app = null;
        lock (_gate)
        {
            _sessionToken = null;
            _clientName = null;
            _desktopPreviewEnabled = false;
            _pairCode = "";
        }
        await app.StopAsync();
        await app.DisposeAsync();
        StatusChanged?.Invoke("RemoteLink 主機已停止。");
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (_sessionToken is null) return false;
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;

        byte[] supplied;
        try
        {
            supplied = Convert.FromBase64String(header["Bearer ".Length..].Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        return supplied.Length == _sessionToken.Length &&
               CryptographicOperations.FixedTimeEquals(supplied, _sessionToken);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    private sealed record PairRequest(string? Code, string? ClientName);
    private sealed record MouseRequest(string? Action, double X, double Y);
}
