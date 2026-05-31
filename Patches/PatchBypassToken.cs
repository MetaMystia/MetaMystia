namespace MetaMystia.Patch;

public class PatchBypassToken
{
    private int _count;

    public void SetCount(int count)
    {
        _count = count;
    }

    public void Reset()
    {
        _count = 0;
    }
    
    public void Grant()
    {
        _count++;
    }

    public void Grant(int count)
    {
        _count += count;
    }

    public bool TryConsume()
    {
        if (_count <= 0) return false;
        _count--;
        return true;
    }

    public int Pending => _count;
}

/// <summary>
/// 兼容旧命名，避免把非顾客重构相关的 patch 一起改掉。
/// </summary>
public sealed class PatchSkipPermit : PatchBypassToken
{
}
