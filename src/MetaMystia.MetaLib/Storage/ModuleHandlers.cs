using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MetaMystia.MetaLib.Storage;

internal sealed class ModuleHandlers
{
    private sealed record Handler(int Version, Func<ModuleData?, bool> Load, Func<JsonElement?> Save)
    {
        internal bool Loaded;
    }

    private readonly Dictionary<string, Handler> handlers = new(StringComparer.Ordinal);
    internal bool IsDispatching { get; private set; }

    internal bool TryRegister(string moduleId, int version, Func<ModuleData?, bool> onLoad, Func<JsonElement?> onSave, out string error)
    {
        error = "";
        if (IsDispatching || string.IsNullOrWhiteSpace(moduleId) || version < 1 || onLoad == null || onSave == null)
        {
            error = "回调注册参数无效，或正在分发回调。";
            return false;
        }
        if (handlers.TryAdd(moduleId, new(version, onLoad, onSave)))
            return true;
        error = $"模块 {moduleId} 已注册。";
        return false;
    }

    internal void Reset()
    {
        foreach (var handler in handlers.Values)
            handler.Loaded = false;
    }

    internal IReadOnlyList<string> Load(SaveDocument document)
    {
        var errors = new List<string>();
        Reset();
        IsDispatching = true;
        try
        {
            foreach (var (id, handler) in handlers)
            {
                document.TryRead(id, out var data, out var error);
                if (error.Length != 0 || data?.Version > handler.Version)
                {
                    // 清空上一个存档的模块状态，但不允许默认值覆盖异常或高版本记录。
                    handler.Load(null);
                    errors.Add(error.Length != 0 ? error : $"模块 {id} 的版本 {data!.Version} 高于支持版本 {handler.Version}。");
                    continue;
                }
                handler.Loaded = handler.Load(data);
                if (!handler.Loaded)
                    errors.Add($"模块 {id} 拒绝加载，原记录保留，保存回调停用至下次读档。");
            }
        }
        finally
        {
            IsDispatching = false;
        }
        return errors;
    }

    internal bool TryCapture(SaveDocument document, out string error)
    {
        error = "";
        if (IsDispatching)
        {
            error = "不能在回调中再次收集存档。";
            return false;
        }
        IsDispatching = true;
        try
        {
            foreach (var (id, handler) in handlers)
            {
                if (!handler.Loaded)
                    continue;
                var data = handler.Save();
                if (data.HasValue && !document.TryWrite(id, handler.Version, data.Value, out error))
                    return false;
            }
            return true;
        }
        finally
        {
            IsDispatching = false;
        }
    }
}
