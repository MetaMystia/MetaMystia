// ICF（相同代码折叠）检查，规则见 docs/rules/harmony-hook.md。
//   check <模组 DLL> <BepInEx 依赖目录>          检查 Hook 目标是否与其他方法共享原生地址
//   index <Il2CppDumper 输出目录> <游戏版本>    从 DummyDll 与 script.json 生成折叠表，写到标准输出

Console.OutputEncoding = System.Text.Encoding.UTF8;
return args switch
{
    ["check", var mod, var deps] => IcfCheck.Run(mod, deps),
    ["index", var dumper, var game] => IcfIndex.Generate(dumper, game, Console.Out),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("用法：check <模组 DLL> <BepInEx 依赖目录> | index <Il2CppDumper 输出目录> <游戏版本>");
    return 2;
}
