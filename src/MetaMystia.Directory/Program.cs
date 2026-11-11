using MetaMystia.Directory;

await using var app = DirectoryApi.Create(new(
    Environment.GetEnvironmentVariable("MM_DIRECTORY_LISTEN") ?? "http://127.0.0.1:40900",
    Environment.GetEnvironmentVariable("MM_DIRECTORY_DATA") ?? Path.Combine(AppContext.BaseDirectory, "data", "instances.json"),
    Environment.GetEnvironmentVariable("MM_DIRECTORY_TOKEN") ?? "",
    Environment.GetEnvironmentVariable("MM_INSTANCE_TOKEN") ?? ""));
await app.RunAsync();
