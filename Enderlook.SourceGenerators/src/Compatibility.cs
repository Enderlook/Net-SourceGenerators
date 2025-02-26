using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
#if !NET5_0_OR_GREATER
    internal static class IsExternalInit
    {
    }
#endif
}

namespace System.Diagnostics.CodeAnalysis
{
#if !(NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER)
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute
    {
    }
#endif
#if !NET7_0_OR_GREATER
    internal sealed class StringSyntaxAttribute : Attribute
    {
        public const string CompositeFormat = nameof(CompositeFormat);
        public StringSyntaxAttribute(string syntax)
        {
        }
        public StringSyntaxAttribute(string syntax, params object?[] arguments)
        {
        }
    }
#endif
}