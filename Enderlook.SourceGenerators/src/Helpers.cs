using System.Runtime.CompilerServices;

namespace Enderlook.SourceGenerators;

internal static class Helpers
{
    private static readonly string[] Indentations;

    static Helpers()
    {
        string[] array = Indentations = new string[10];
        for (int i = 0; i < array.Length; i++)
            array[i] = $"{Environment.NewLine}{IndentedSourceBuilder.INDENTATION_SEPARATOR * (IndentedSourceBuilder.INDENTATION_COUNT * i)}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReplaceIndentationFor(int level)
    {
        if (unchecked((uint)level < (uint)Indentations.Length))
            return Indentations[level];
        return $"{Environment.NewLine}{IndentedSourceBuilder.INDENTATION_SEPARATOR * (IndentedSourceBuilder.INDENTATION_COUNT * level)}";
    }
}