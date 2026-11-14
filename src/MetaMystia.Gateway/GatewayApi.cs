using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using MetaMystia.Hosting;
using MetaMystia.Network;

namespace MetaMystia.Gateway;

public static class GatewayApi
{
    private const int MaxForwardBody = 64 * 1024;
    // 实例接口路径：小写字母、数字、连字符，按 / 分段；status 与 drain 必须经过 Gateway 自己的接口。
    private static readonly Regex forwardPath = new("^[a-z0-9-]+(/[a-z0-9-]+)*$", RegexOptions.CultureInvariant);

    public static WebApplication Create(string url, string path, string adminToken, string instanceToken)
    {
        if (string.IsNullOrWhiteSpace(adminToken) || string.IsNullOrWhiteSpace(instanceToken))
            throw new ArgumentException("Management tokens are required");
        var address = new Uri(url);
        if (address.Scheme != "http" || !IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip))
            throw new ArgumentException("Management HTTP must bind to loopback");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(GatewayApi).Assembly.FullName });
        builder.WebHost.UseUrls(url);
        // 面板会轮询，ASP.NET 自带的逐请求日志会刷满 journald。
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Services.AddSingleton(new InstanceRegistry(path, instanceToken));
        builder.Services.AddSingleton(new GatewayStats());
        var app = builder.Build();
        var stats = app.Services.GetRequiredService<GatewayStats>();
        void Audit(string message)
        {
            stats.Logs.Add("info", message);
            app.Logger.LogInformation("{Message}", message);
        }
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.Authorization != "Bearer " + adminToken)
            { context.Response.StatusCode = 401; return; }
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (KeyNotFoundException) { context.Response.StatusCode = 404; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException or JsonException)
            { context.Response.StatusCode = error is InvalidOperationException ? 409 : 400; }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
            { context.Response.StatusCode = 502; }
        });

        app.MapGet("/admin/status", () => new { connections = stats.Active });
        app.MapGet("/admin/gateway", () => new { process = ProcessMetrics.Snapshot(), connections = stats.Active, totals = stats.Totals });
        app.MapGet("/admin/connections", (int? limit) => stats.Recent(limit ?? 500));
        app.MapGet("/admin/logs", (long? after, int? limit) => stats.Logs.Read(after, limit));
        app.MapGet("/admin/history", () => stats.History.Snapshot());

        app.MapGet("/admin/instances", (InstanceRegistry registry) => registry.List());
        app.MapPut("/admin/instances/{id}", async (string id, InstanceRegistration body, InstanceRegistry registry, CancellationToken token) =>
        {
            await registry.RegisterAsync(id, body, token);
            Audit($"登记实例 {id}（模组 {body.ModVersion}，端口 {body.Port}）。");
            return Results.NoContent();
        });
        app.MapDelete("/admin/instances/{id}", async (string id, InstanceRegistry registry, CancellationToken token) =>
        {
            await registry.DeleteAsync(id, token);
            Audit($"删除实例登记 {id}。");
            return Results.NoContent();
        });
        app.MapGet("/admin/instances/{id}/status", (string id, InstanceRegistry registry, CancellationToken token) => registry.StatusAsync(id, token));
        app.MapPost("/admin/instances/{id}/drain", async (string id, DrainRequest body, InstanceRegistry registry, CancellationToken token) =>
        {
            Audit($"请求退役实例 {id}，目标模组 {ServerLogEntry.Quote(body.TargetModVersion ?? "")}。");
            await registry.DrainAsync(id, body, token);
            return Results.Accepted();
        });
        // 其余实例接口原样转发，实例新增接口时无需更新 Gateway。
        app.Map("/admin/instances/{id}/api/{**rest}", async Task (HttpContext context, string id, string rest, InstanceRegistry registry) =>
        {
            var method = context.Request.Method;
            if (!forwardPath.IsMatch(rest) || rest is "status" or "drain" ||
                !(HttpMethods.IsGet(method) || HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method)))
            { context.Response.StatusCode = 400; return; }
            byte[]? body = null;
            if (!HttpMethods.IsGet(method))
            {
                if (context.Request.ContentLength > MaxForwardBody) { context.Response.StatusCode = 413; return; }
                using var buffer = new MemoryStream();
                await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
                if (buffer.Length > MaxForwardBody) { context.Response.StatusCode = 413; return; }
                body = buffer.Length == 0 ? null : buffer.ToArray();
            }
            var result = await registry.ForwardAsync(id, new HttpMethod(method), "/admin/" + rest + context.Request.QueryString, body, context.RequestAborted);
            if (!HttpMethods.IsGet(method)) Audit($"向实例 {id} 转发 {method} {rest}，结果 {result.StatusCode}。");
            context.Response.StatusCode = result.StatusCode;
            if (result.Body.Length > 0)
            {
                context.Response.ContentType = result.ContentType ?? "application/json";
                await context.Response.Body.WriteAsync(result.Body, context.RequestAborted);
            }
        });
        return app;
    }
}
