using System;
using NLua;

namespace MetaMystia.Debugger
{
    /// <summary>
    /// Manages Lua VM instance for Web Debugger
    /// </summary>
    public class LuaDebugger
    {
        private static Lua _luaVm;
        private static DebugHelper _debugHelper;
        private static readonly object _lock = new object();

        public static Lua LuaVm
        {
            get
            {
                if (_luaVm == null)
                {
                    Initialize();
                }
                return _luaVm;
            }
        }

        public static DebugHelper DebugHelper
        {
            get
            {
                if (_debugHelper == null)
                {
                    Initialize();
                }
                return _debugHelper;
            }
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_luaVm != null) return;

                try
                {
                    _luaVm = new Lua();
                    _luaVm.LoadCLRPackage();

                    // Register global helper
                    _debugHelper = new DebugHelper();
                    _luaVm["debug"] = _debugHelper;

                    // Preload common assemblies for game scripting
                    _luaVm.DoString(@"
                        luanet.load_assembly('Assembly-CSharp')
                        luanet.load_assembly('UnityEngine.CoreModule')
                        luanet.load_assembly('Il2Cppmscorlib')
                    ");

                    // Override print to redirect to debugger
                    _luaVm.DoString(@"
                        _original_print = print
                        function print(...)
                            debug:Print(...)
                        end
                    ");

                    _luaVm.DoString("print('Lua VM Initialized for Web Debugger')");

                    BepInEx.Logging.Logger.CreateLogSource("LuaDebugger").LogInfo("Lua VM initialized successfully");
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("LuaDebugger").LogError($"Failed to initialize Lua VM: {ex}");
                    throw;
                }
            }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                if (_luaVm != null)
                {
                    _luaVm.Dispose();
                    _luaVm = null;
                    _debugHelper = null;
                }
            }
        }
    }
}
