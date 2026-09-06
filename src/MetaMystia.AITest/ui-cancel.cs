using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    public static object Execute()
    {
        UILogicalUnit.TryInvokeCurrentCancel();
        return "Cancel submitted; verify next snapshot";
    }
}
