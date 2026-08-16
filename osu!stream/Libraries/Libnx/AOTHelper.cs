using System;

namespace Libnx;

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class MonoPInvokeCallbackAttribute(Type type) : Attribute;