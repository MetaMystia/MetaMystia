namespace MetaMystia.Gateway;

// The page holds no secrets and asks for the admin token itself, so it is served without one.
public static class GatewayPanel
{
    private const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; " +
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private static readonly Dictionary<string, (string Resource, string Type)> files = new()
    {
        ["/admin/"] = ("index.html", "text/html; charset=utf-8"),
        ["/admin/panel.css"] = ("panel.css", "text/css; charset=utf-8"),
        ["/admin/panel.js"] = ("panel.js", "text/javascript; charset=utf-8"),
        ["/admin/favicon.svg"] = ("favicon.svg", "image/svg+xml"),
    };

    public static async Task<bool> TryServe(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) return false;
        if (context.Request.Path == "/admin") { context.Response.Redirect("/admin/"); return true; }
        if (!files.TryGetValue(context.Request.Path.Value ?? "", out var file)) return false;
        using var stream = typeof(GatewayPanel).Assembly.GetManifestResourceStream("panel/" + file.Resource)
            ?? throw new InvalidOperationException("Missing panel resource " + file.Resource);
        var headers = context.Response.Headers;
        headers.ContentType = file.Type;
        headers.CacheControl = "no-cache";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        if (file.Resource == "index.html")
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XFrameOptions = "DENY";
        }
        context.Response.ContentLength = stream.Length;
        if (HttpMethods.IsGet(context.Request.Method)) await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        return true;
    }
}
