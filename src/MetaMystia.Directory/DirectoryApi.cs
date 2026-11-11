using System.Text.Json;

using MetaMystia.Discovery;

namespace MetaMystia.Directory;

public sealed record DirectoryOptions(string Url, string DataPath, string AdminToken, string InstanceToken);

public static class DirectoryApi
{
    public static WebApplication Create(DirectoryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminToken) || string.IsNullOrWhiteSpace(options.InstanceToken))
            throw new ArgumentException("Directory and instance tokens are required");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(DirectoryApi).Assembly.FullName });
        builder.WebHost.UseUrls(options.Url);
        builder.Services.AddSingleton(new InstanceRegistry(options.DataPath, options.InstanceToken));
        builder.Services.AddHostedService<RegistryWorker>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            string? expected = context.Request.Path.StartsWithSegments("/admin") ? options.AdminToken
                : context.Request.Path.StartsWithSegments("/internal") ? options.InstanceToken : null;
            if (expected != null && context.Request.Headers.Authorization != "Bearer " + expected)
            { context.Response.StatusCode = 401; return; }
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or JsonException or HttpRequestException)
            {
                context.Response.StatusCode = e is InvalidOperationException ? 409 : e is HttpRequestException ? 502 : 400;
                await context.Response.WriteAsJsonAsync(new { error = e.Message });
            }
        });
        app.MapGet("/v1/servers", (InstanceRegistry registry) => registry.Catalog());
        app.MapGet("/admin/instances", (InstanceRegistry registry) => registry.List());
        app.MapGet("/internal/instances/{id}", (string id, InstanceRegistry registry) =>
            registry.Get(id) is { } record ? Results.Ok(record) : Results.NotFound());
        app.MapPut("/admin/instances/{id}", async (string id, InstanceRegistration body, InstanceRegistry registry, CancellationToken token) =>
        { await registry.RegisterAsync(id, body, token); return Results.Ok(registry.Get(id)); });
        app.MapPost("/admin/instances/{id}/activate", async (string id, ActivationRequest body, InstanceRegistry registry, CancellationToken token) =>
        { await registry.ActivateAsync(id, body, token); return Results.Accepted(value: registry.Get(id)); });
        app.MapPost("/internal/instances/{id}/stopped", async (string id, InstanceRegistry registry, CancellationToken token) =>
        { await registry.MarkStoppedAsync(id, token); return Results.NoContent(); });
        return app;
    }
}

internal sealed class RegistryWorker(InstanceRegistry registry, ILogger<RegistryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await registry.ReconcileAsync(stoppingToken); }
            catch (IOException error) { log.LogError(error, "目录状态保存失败，将重试"); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
