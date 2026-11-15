using System;
using System.Collections.Generic;

namespace MetaMystia;

public sealed class CookerOperations
{
    public sealed record Operation(Guid Id, int Revision, int Owner, bool Started = false);
    private readonly Dictionary<int, Operation> active = new();
    private readonly Dictionary<int, int> revisions = new();
    public Operation Get(int grid) => active.GetValueOrDefault(grid);
    public int Revision(int grid) => revisions.GetValueOrDefault(grid);

    public bool Accept(int grid, Operation operation)
    {
        if (operation.Id == Guid.Empty || operation.Revision != Revision(grid)) return false;
        var current = Get(grid);
        // 同一轮并发提交按同一个顺序收敛，不依赖各端收包先后。
        if (current != null && current.Id.CompareTo(operation.Id) <= 0) return false;
        active[grid] = operation;
        return true;
    }

    public bool Start(int grid, Guid id, int owner)
    {
        var current = Get(grid);
        if (current == null || current.Id != id || current.Owner != owner || current.Started) return false;
        active[grid] = current with { Started = true };
        return true;
    }

    public bool Complete(int grid, Guid id, int revision)
    {
        if (Get(grid)?.Id != id || Revision(grid) != revision) return false;
        active.Remove(grid);
        revisions[grid] = revision + 1;
        return true;
    }

    public void Clear()
    {
        active.Clear();
        revisions.Clear();
    }
}
