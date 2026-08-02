using System;
using System.Text;

namespace MetaMystia.Debugger
{
    public class DebugHelper
    {
        private readonly StringBuilder _luaOutput = new StringBuilder();

        public string GetLuaOutput()
        {
            var result = _luaOutput.ToString();
            _luaOutput.Clear();
            return result;
        }

        public void Print(params object[] args)
        {
            var output = string.Join("\t", args);
            _luaOutput.AppendLine(output);
            BepInEx.Logging.Logger.CreateLogSource("Lua").LogInfo(output);
        }

        public object FromAddress(string typeName, string address)
        {
            try
            {
                long addr;
                if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    addr = Convert.ToInt64(address.Substring(2), 16);
                }
                else
                {
                    addr = Convert.ToInt64(address, 10);
                }
                IntPtr ptr = new IntPtr(addr);

                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeName);
                    if (type != null) break;
                }

                if (type != null)
                {
                    return Activator.CreateInstance(type, ptr);
                }
                return $"Type {typeName} not found";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
