using System;

using MetaMystia.MetaLib;

var failures = CodecChecks.Run();
foreach (var failure in failures)
    Console.Error.WriteLine(failure);
Console.WriteLine(failures.Count == 0 ? "MetaLib 存储格式检查全部通过。" : $"失败：{failures.Count}");
return failures.Count == 0 ? 0 : 1;
