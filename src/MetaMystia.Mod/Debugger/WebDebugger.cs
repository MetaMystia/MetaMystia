using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityEngine;

namespace MetaMystia.Debugger
{
    public class WebDebugger : IDisposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _disposed;
        private string _token;
        private ManualLogSource _log => Plugin.Instance.Log;

        private const string AllowedOrigin = "http://127.0.0.1:21101";

        private delegate Task RouteHandler(HttpListenerRequest request, HttpListenerResponse response);
        private readonly Dictionary<string, RouteHandler> _getRoutes = new();
        private readonly Dictionary<string, RouteHandler> _postRoutes = new();

        public WebDebugger()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:21101/");

            RegisterRoutes();

            // Initialize Lua VM when WebDebugger is created
            LuaDebugger.Initialize();
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WebDebugger));

            if (_isRunning)
            {
                OpenInBrowser();
                return;
            }

            try
            {
                _token = GenerateToken();
                _cts = new CancellationTokenSource();
                _listener.Start();
                _isRunning = true;
                _log.LogWarning($"Web Debugger started.");
                OpenInBrowser();
                Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                _log.LogError($"Failed to start Web Debugger: {ex}");
            }
        }

        public void OpenInBrowser()
        {
            if (string.IsNullOrEmpty(_token))
            {
                _log.LogWarning("Web Debugger token is not generated yet.");
                return;
            }
            string url = $"http://127.0.0.1:21101/?token={_token}";
            Application.OpenURL(url);
        }

        private string GenerateToken()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < 16; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException) when (!_isRunning)
                {
                    // Expected when listener is stopped during shutdown
                    break;
                }
                catch (ObjectDisposedException) when (!_isRunning)
                {
                    // Expected when listener is disposed during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        _log.LogError($"Error in Web Debugger loop: {ex}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Only accept connections from 127.0.0.1
                var remoteIp = request.RemoteEndPoint.Address;
                if (!IPAddress.IsLoopback(remoteIp))
                {
                    _log.LogWarning($"Rejected connection from non-loopback address: {remoteIp}");
                    response.StatusCode = 403;
                    response.Close();
                    return;
                }

                // Security headers
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["X-Frame-Options"] = "DENY";
                response.Headers["Access-Control-Allow-Origin"] = AllowedOrigin;
                response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

                // Handle CORS preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    return;
                }

                string reqToken = request.QueryString["token"];
                bool isAuthenticated = !string.IsNullOrEmpty(reqToken) && reqToken == _token;

                // Route dispatch
                var path = request.Url.AbsolutePath;
                bool isApiRoute = path != "/" && path != "";

                if (!isAuthenticated)
                {
                    if (isApiRoute)
                    {
                        WriteText(response, "Unauthorized", 401);
                        return;
                    }
                    WriteHtml(response, WebResources.LoginHtmlContent);
                    return;
                }

                var routes = request.HttpMethod == "POST" ? _postRoutes : _getRoutes;

                if (routes.TryGetValue(path, out var handler))
                {
                    await handler(request, response);
                    return;
                }

                // Query-string action dispatch (legacy GET endpoints)
                string action = request.QueryString["action"];
                if (action != null && _getRoutes.TryGetValue("?action=" + action, out var actionHandler))
                {
                    await actionHandler(request, response);
                    return;
                }

                // Default: serve main page
                string content = WebResources.HtmlContent.Replace("[[TOKEN]]", _token);
                WriteHtml(response, content);
            }
            catch (Exception ex)
            {
                _log.LogError($"Unhandled error processing {request.HttpMethod} {request.Url?.AbsolutePath}: {ex}");
                try { WriteText(response, "Internal Server Error", 500); }
                catch { /* Response may already be closed */ }
            }
            finally
            {
                try { response.OutputStream.Close(); }
                catch { /* Already closed or disposed */ }
            }
        }

        #region Route Registration

        private void RegisterRoutes()
        {
            _postRoutes["/eval"] = HandleEval;
            _postRoutes["/eval-lua"] = HandleEvalLua;
            _postRoutes["/sprite-preview"] = HandleSpritePreview;
            _getRoutes["/scene-hierarchy"] = HandleSceneHierarchy;
            _getRoutes["/threads"] = HandleThreads;
            _getRoutes["?action=search_resources"] = HandleSearchResources;
            _getRoutes["?action=select_resource"] = HandleSelectResource;
        }

        #endregion

        #region Route Handlers

        private async Task HandleEval(HttpListenerRequest request, HttpListenerResponse response)
        {
            string expression = await ReadBodyAsync(request);
            InspectionResult result;
            try { result = await RunOnMainThreadAsync(() => ReflectionEvaluator.Inspect(expression)); }
            catch (Exception ex)
            {
                _log.LogWarning($"Eval error: {ex.Message}");
                result = new InspectionResult { Error = ex.ToString() };
            }
            WriteJson(response, result);
        }

        private async Task HandleEvalLua(HttpListenerRequest request, HttpListenerResponse response)
        {
            string expression = await ReadBodyAsync(request);

            if (LuaDebugger.LuaVm == null)
            {
                WriteText(response, "Lua VM not initialized");
                return;
            }

            string result;
            try
            {
                result = await RunOnMainThreadAsync(() =>
                {
                    var debugHelper = LuaDebugger.DebugHelper;
                    object[] res = LuaDebugger.LuaVm.DoString(expression);

                    var sb = new StringBuilder();
                    if (debugHelper != null)
                    {
                        string printOutput = debugHelper.GetLuaOutput();
                        if (!string.IsNullOrEmpty(printOutput))
                            sb.AppendLine(printOutput.TrimEnd());
                    }
                    if (res != null && res.Length > 0)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append("=> ");
                        foreach (var r in res) sb.Append(r?.ToString() + " ");
                    }

                    return sb.Length > 0 ? sb.ToString() : "OK";
                });
            }
            catch (Exception ex) { result = "Error: " + ex.Message; }
            WriteText(response, result);
        }

        private async Task HandleSpritePreview(HttpListenerRequest request, HttpListenerResponse response)
        {
            string expression = await ReadBodyAsync(request);
            try
            {
                var (pngData, error) = await RunOnMainThreadAsync(() =>
                {
                    object obj = ReflectionEvaluator.Evaluate(expression);
                    if (obj is Sprite sprite)
                        return (SpritePreview.SpriteToPng(sprite), (string)null);
                    return ((byte[])null, $"Expression result is not a Sprite (got {obj?.GetType().Name ?? "null"})");
                });

                if (pngData != null)
                    WriteBytes(response, pngData, "image/png");
                else
                    WriteText(response, error ?? "Unknown error", 400);
            }
            catch (Exception ex) { WriteText(response, ex.ToString(), 400); }
        }

        private async Task HandleSceneHierarchy(HttpListenerRequest request, HttpListenerResponse response)
        {
            object result;
            try { result = await RunOnMainThreadAsync(() => ReflectionEvaluator.GetSceneHierarchy()); }
            catch (Exception ex) { result = new { error = ex.ToString() }; }
            WriteJson(response, result);
        }

        private Task HandleThreads(HttpListenerRequest request, HttpListenerResponse response)
        {
            var threads = new List<object>();
            foreach (System.Diagnostics.ProcessThread t in System.Diagnostics.Process.GetCurrentProcess().Threads)
            {
                string state = "";
                try { state = t.ThreadState.ToString(); } catch { state = "Unknown"; }

                string priority = "";
#pragma warning disable CA1416
                try { priority = t.PriorityLevel.ToString(); } catch { priority = "Unknown"; }
#pragma warning restore CA1416

                string waitReason = "";
                try { if (t.ThreadState == System.Diagnostics.ThreadState.Wait) waitReason = t.WaitReason.ToString(); } catch { }

                threads.Add(new { Id = t.Id, State = state, Priority = priority, WaitReason = waitReason });
            }

            ThreadPool.GetAvailableThreads(out int wA, out int ioA);
            ThreadPool.GetMaxThreads(out int wM, out int ioM);

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var data = new
            {
                Stats = $"Worker Threads: {wA}/{wM}, IO Threads: {ioA}/{ioM}",
                Threads = threads,
                Process = new
                {
                    Name = proc.ProcessName,
                    Id = proc.Id,
                    Memory = (proc.WorkingSet64 / 1024 / 1024) + " MB",
                    StartTime = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    RealTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Threads = proc.Threads.Count
                }
            };

            WriteJson(response, data);
            return Task.CompletedTask;
        }

        private async Task HandleSearchResources(HttpListenerRequest request, HttpListenerResponse response)
        {
            string className = request.QueryString["className"];
            string filter = request.QueryString["filter"];
            object result;
            try
            {
                result = string.IsNullOrWhiteSpace(filter)
                    ? await RunOnMainThreadAsync(() => ReflectionEvaluator.FindResources(className))
                    : await RunOnMainThreadAsync(() => ReflectionEvaluator.FindResources(className, filter));
            }
            catch (Exception ex) { result = new { error = ex.Message }; }
            WriteJson(response, result);
        }

        private async Task HandleSelectResource(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!int.TryParse(request.QueryString["index"], out int index))
            {
                WriteText(response, "Invalid or missing 'index' parameter", 400);
                return;
            }
            string result;
            try
            {
                result = await RunOnMainThreadAsync(() =>
                {
                    ReflectionEvaluator.SelectResource(index);
                    return "Selected";
                });
            }
            catch (Exception ex) { result = "Error: " + ex.Message; }
            WriteText(response, result);
        }

        #endregion

        #region Helpers

        private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }

        private async Task<T> RunOnMainThreadAsync<T>(Func<T> func)
        {
            if (PluginManager.Instance == null)
                throw new InvalidOperationException("PluginManager not initialized");

            var tcs = new TaskCompletionSource<T>();
            PluginManager.Instance.RunOnMainThread(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return await tcs.Task;
        }

        private static void WriteJson(HttpListenerResponse response, object data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteText(HttpListenerResponse response, string text, int statusCode = 200)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteHtml(HttpListenerResponse response, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteBytes(HttpListenerResponse response, byte[] data, string contentType)
        {
            response.ContentType = contentType;
            response.ContentLength64 = data.Length;
            response.OutputStream.Write(data, 0, data.Length);
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;

            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }

            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
                _listener.Close();
            }
            catch (ObjectDisposedException) { }
        }
    }
}
