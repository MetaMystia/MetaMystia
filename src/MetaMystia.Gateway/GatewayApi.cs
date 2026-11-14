using System.Net;
using System.Text.Json;

using MetaMystia.Network;

namespace MetaMystia.Gateway;

public static class GatewayApi
{
    public static WebApplication Create(string url, string path, string adminToken, string instanceToken)
    {
        if (string.IsNullOrWhiteSpace(adminToken) || string.IsNullOrWhiteSpace(instanceToken))
            throw new ArgumentException("Management tokens are required");
        var address = new Uri(url);
        if (address.Scheme != "http" || !IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip))
            throw new ArgumentException("Management HTTP must bind to loopback");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(GatewayApi).Assembly.FullName });
        builder.WebHost.UseUrls(url);
        builder.Services.AddSingleton(new InstanceRegistry(path, instanceToken));
        var app = builder.Build();
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
        app.MapGet("/admin/instances", (InstanceRegistry registry) => registry.List());
        app.MapPut("/admin/instances/{id}", async (string id, InstanceRegistration body, InstanceRegistry registry, CancellationToken token) =>
        { await registry.RegisterAsync(id, body, token); return Results.NoContent(); });
        app.MapDelete("/admin/instances/{id}", async (string id, InstanceRegistry registry, CancellationToken token) =>
        { await registry.DeleteAsync(id, token); return Results.NoContent(); });
        app.MapGet("/admin/instances/{id}/status", (string id, InstanceRegistry registry, CancellationToken token) => registry.StatusAsync(id, token));
        app.MapPost("/admin/instances/{id}/drain", async (string id, DrainRequest body, InstanceRegistry registry, CancellationToken token) =>
        { await registry.DrainAsync(id, body, token); return Results.Accepted(); });
        return app;
    }
}
