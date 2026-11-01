using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
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
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Window)> _attempts = new();

    public event Action<string>? StatusChanged;
    public string PairCode { get { lock (_gate) return _pairCode; } }
    public DateTimeOffset CodeExpiresAt { get { lock (_gate) return _codeExpiresAt; } }
    public bool IsRunning => _app is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null) return;
        lock (_gate)
        {
            _pairCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _codeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            _sessionToken = null;
            _clientName = null;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { service = "RemoteLink", pairingEnabled = true, hostName = Environment.MachineName }));

        app.MapPost("/pair", (PairRequest request, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTimeOffset.UtcNow;
            var attempt = _attempts.AddOrUpdate(ip,
                _ => (1, now),
                (_, old) => now - old.Window > TimeSpan.FromMinutes(1) ? (1, now) : (old.Count + 1, old.Window));
            if (attempt.Count > 8)
                return (IResult)Results.Json(new { error = "too_many_attempts", message = "嘗試次數過多，請稍後再試。" }, statusCode: 429);

            lock (_gate)
            {
                if (_sessionToken is not null)
                    return (IResult)Results.Conflict(new { error = "already_paired", message = "主機已有連線中的用戶端。" });
                if (now > _codeExpiresAt)
                    return (IResult)Results.Json(new { error = "code_expired", message = "配對碼已過期，請在主機重新啟動配對。" }, statusCode: 410);
                var supplied = Encoding.ASCII.GetBytes(request.Code ?? "");
                var expected = Encoding.ASCII.GetBytes(_pairCode);
                if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
                    return (IResult)Results.Unauthorized();

                _sessionToken = RandomNumberGenerator.GetBytes(32);
                _clientName = string.IsNullOrWhiteSpace(request.ClientName) ? "Android client" : request.ClientName.Trim()[..Math.Min(request.ClientName.Trim().Length, 64)];
                _pairCode = "";
                StatusChanged?.Invoke($"已配對：{_clientName}");
                return (IResult)Results.Ok(new { token = Convert.ToBase64String(_sessionToken), hostName = Environment.MachineName });
            }
        });

        app.MapGet("/session", (HttpContext context) =>
        {
            lock (_gate)
                return IsAuthorized(context) ? (IResult)Results.Ok(new { connected = true, clientName = _clientName }) : Results.Unauthorized();
        });

        app.MapPost("/session/close", (HttpContext context) =>
        {
            lock (_gate)
            {
                if (!IsAuthorized(context)) return Results.Unauthorized();
                _sessionToken = null;
                _clientName = null;
                _pairCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
                _codeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
                StatusChanged?.Invoke("用戶端已中斷；新配對碼有效 5 分鐘。");
                return (IResult)Results.Ok(new { disconnected = true });
            }
        });

        _app = app;
        try
        {
            await app.StartAsync(cancellationToken);
            StatusChanged?.Invoke("配對服務已啟動，等待 Android 用戶端配對。");
        }
        catch
        {
            _app = null;
            await app.DisposeAsync();
            throw;
        }
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (_sessionToken is null) return false;
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var supplied = Convert.FromBase64String(header[7..].Trim());
            return supplied.Length == _sessionToken.Length && CryptographicOperations.FixedTimeEquals(supplied, _sessionToken);
        }
        catch (FormatException) { return false; }
    }

    public async Task StopAsync()
    {
        var app = _app;
        _app = null;
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
        lock (_gate)
        {
            _sessionToken = null;
            _clientName = null;
            _pairCode = "";
        }
        StatusChanged?.Invoke("主機已停止，所有工作階段授權已撤銷。");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
    private sealed record PairRequest(string? Code, string? ClientName);
}
