using System;

namespace MetaMystia;

[AttributeUsage(AttributeTargets.Method)]
public sealed class OnMainThreadAttribute : Attribute { }
