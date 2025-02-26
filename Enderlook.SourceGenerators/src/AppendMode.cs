namespace Enderlook.SourceGenerators;

/// <summary>
/// Determines how the value is appended to the <see cref="IndentedSourceBuilder"/>.
/// </summary>
public enum AppendMode
{
    /// <summary>
    /// Appends the entire <see cref="string"/> and do nothing else.
    /// </summary>
    Continuous = 0,

    /// <summary>
    /// Appends the entire <see cref="string"/>, then call <see cref="IndentedSourceBuilder.AppendLine()"/>.
    /// </summary>
    SingleLine = IndentedSourceBuilder.END_WITH_NEW_LINE,

    /// <summary>
    /// The entire <see cref="string"/> is separated into multiple lines using as delimiter <see cref="Environment.NewLine"/>.<br/>
    /// Then each line is appended, followed by a call to <see cref="IndentedSourceBuilder.AppendLine()"/>.
    /// </summary>
    Multiline = IndentedSourceBuilder.SUPPORT_MULTILINE | IndentedSourceBuilder.END_WITH_NEW_LINE,

    /// <summary>
    /// The entire <see cref="string"/> is separated into multiple lines using as delimiter <see cref="Environment.NewLine"/>.<br/>
    /// Then each line is appended, followed by a call to <see cref="IndentedSourceBuilder.AppendLine()"/>, except the last line, which doesn't perform a call.
    /// </summary>
    MultilineSkipFinalNewline = IndentedSourceBuilder.SUPPORT_MULTILINE,
}