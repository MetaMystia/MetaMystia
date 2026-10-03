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
