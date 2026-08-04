namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill required for C# 9 'init' accessors and records to compile
    /// under runtimes older than .NET 5 (e.g. Unity's .NET Standard 2.1).
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
