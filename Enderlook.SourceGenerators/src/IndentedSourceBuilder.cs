using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Enderlook.SourceGenerators;

/// <summary>
/// Helper to build source files with Indentation.
/// </summary>
public sealed class IndentedSourceBuilder
{
    internal const int END_WITH_NEW_LINE = 1 << 1;
    internal const int SUPPORT_MULTILINE = 1 << 2;

    internal const int INDENTATION_COUNT = 4;
    internal const char INDENTATION_SEPARATOR = ' ';

    private readonly StringBuilder builder;
    private bool isAtNewLine;

    /// <summary>
    /// Gets or sets the maximum number of characters that can be contained in the memory allocated by the current instance.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => builder.Capacity;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => builder.Capacity = value;
    }

    /// <summary>
    /// Gets or sets the length of the current StringBuilder object.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => builder.Length;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => builder.Length = value;
    }

    /// <summary>
    /// Current indentation level.
    /// </summary>
    public int IndentCount { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] private set; }

    /// <summary>
    /// Gets or sets the character at the specified character position in this instance.
    /// </summary>
    /// <param name="index">The position of the character.</param>
    public char this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => builder[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => builder[index] = value;
    }

    /// <summary>
    /// Wraps an <see cref="StringBuilder"/> to support additional functionality.
    /// </summary>
    /// <param name="builder"><see cref="StringBuilder"/> to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is <see langword="null"/>.</exception>
    public IndentedSourceBuilder(StringBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));
        this.builder = builder;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IndentedSourceBuilder"/>.
    /// </summary>
    public IndentedSourceBuilder()
    {
        builder = new();
    }

    /// <summary>
    /// Appends the <see cref="string"/> representation of a specified value to this instance.
    /// </summary>
    /// <typeparam name="T">Type of value whose <see cref="string"/> representation will be appended.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append<T>(T? value, AppendMode mode = AppendMode.Continuous)
    {
        if (value is null)
            goto end;

        if (typeof(T).IsValueType)
        {
            if (typeof(T).IsPrimitive)
            {
                switch (Unsafe.SizeOf<T>())
                {
                    case 1:
                    {
                        if (typeof(T) == typeof(bool))
                            EnsureIndentation().Append(Unsafe.As<T, bool>(ref value));
                        else if (typeof(T) == typeof(sbyte))
                            EnsureIndentation().Append(Unsafe.As<T, sbyte>(ref value));
                        else if (typeof(T) == typeof(byte))
                            EnsureIndentation().Append(Unsafe.As<T, byte>(ref value));
                        else
                        {
                            Debug.Fail("Primitive not recognized.");
                            goto fallback;
                        }
                        break;
                    }
                    case 2:
                    {
                        if (typeof(T) == typeof(ushort))
                            EnsureIndentation().Append(Unsafe.As<T, ushort>(ref value));
                        else if (typeof(T) == typeof(short))
                            EnsureIndentation().Append(Unsafe.As<T, short>(ref value));
                        else if (typeof(T) == typeof(char))
                        {
                            char c = Unsafe.As<T, char>(ref value);
                            if (c is '\n' or '\r' && HandleMultilines(mode))
                            {
                                AppendLine();
                                goto endChar;
                            }
                            EnsureIndentation().Append(c);
                        endChar:;
                        }
                        else
                        {
                            Debug.Fail("Primitive not recognized.");
                            goto fallback;
                        }
                        break;
                    }
                    case 4:
                    {
                        if (typeof(T) == typeof(uint))
                            EnsureIndentation().Append(Unsafe.As<T, uint>(ref value));
                        else if (typeof(T) == typeof(int))
                            EnsureIndentation().Append(Unsafe.As<T, int>(ref value));
                        else if (typeof(T) == typeof(float))
                            EnsureIndentation().Append(Unsafe.As<T, float>(ref value));
                        else
                        {
                            Debug.Fail("Primitive not recognized.");
                            goto fallback;
                        }
                        break;
                    }
                    case 8:
                    {
                        if (typeof(T) == typeof(ulong))
                            EnsureIndentation().Append(Unsafe.As<T, ulong>(ref value));
                        else if (typeof(T) == typeof(long))
                            EnsureIndentation().Append(Unsafe.As<T, long>(ref value));
                        else if (typeof(T) == typeof(decimal))
                            EnsureIndentation().Append(Unsafe.As<T, decimal>(ref value));
                        else
                        {
                            Debug.Fail("Primitive not recognized.");
                            goto fallback;
                        }
                        break;
                    }
                    default:
                        Debug.Fail("Primitive not recognized.");
                        goto fallback;
                }
            }
            else if (typeof(T) == typeof(decimal))
                EnsureIndentation().Append(Unsafe.As<T, decimal>(ref value));
            else
                goto fallback;
        }
        else
            goto fallback;

        goto end;

    fallback:
        if (!HandleMultilines(mode))
        {
            StringBuilder builder = this.builder;
            int oldLength = builder.Length;
            EnsureIndentation();
            int length = builder.Length;
            builder.Append(value);
            if (builder.Length == length && length != oldLength)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
        }
        else
            return Append(value.ToString().AsSpan(), mode);

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends the content of a specified <see cref="StringBuilder"/> to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>    
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append(StringBuilder? value, AppendMode mode = AppendMode.Continuous)
    {
        if (value is null || value.Length == 0)
            goto end;

        if (HandleMultilines(mode))
        {
#if NET5_0_OR_GREATER
            StringBuilder.ChunkEnumerator enumerator = value.GetChunks();
            if (enumerator.MoveNext())
            {
                ReadOnlySpan<char> previous = enumerator.Current.Span;
            next:
                AppendIdent(previous);
                if (enumerator.MoveNext())
                {
                    ReadOnlySpan<char> next = enumerator.Current.Span;
                    if (next.Length > 0 && next[0] == '\n' && previous.Length > 0 && previous[previous.Length - 1] == '\r')
                    {
                        // Skip because it was an `\r\n` across two spans,
                        // and we already added the newline from the `\r` of the previous span.
                        next = next.Slice(1);
                    }
                    previous = next;
                    goto next;
                }
            }
#else
            return Append(value.ToString(), mode);
#endif
        }
        else
            EnsureIndentation().Append(value);

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends the content of a <see cref="IndentedSourceBuilder"/> to this instance.
    /// </summary>
    /// <param name="value">Content to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Append(IndentedSourceBuilder? value, AppendMode mode = AppendMode.Continuous) => Append(value?.builder, mode);

    /// <summary>
    /// Appends the <see cref="string"/> to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Append(string? value, AppendMode mode = AppendMode.Continuous) => Append(value.AsSpan(), mode);

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified <see cref="char"/> array to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Append(char[]? value, AppendMode mode = AppendMode.Continuous) => Append((ReadOnlySpan<char>)value, mode);

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified <see cref="char"/> array segment to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Append(ArraySegment<char> value, AppendMode mode = AppendMode.Continuous) => Append(value.AsSpan(), mode);

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified read-only <see cref="char"/> memory to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Append(ReadOnlyMemory<char> value, AppendMode mode = AppendMode.Continuous) => Append(value.Span, mode);

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified read-only <see cref="char"/> span to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append(ReadOnlySpan<char> value, AppendMode mode = AppendMode.Continuous)
    {
        if (value.Length == 0)
            goto end;

        if (!HandleMultilines(mode))
        {
            StringBuilder builder = EnsureIndentation();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            builder.Append(value);
#else
            unsafe
            {
                fixed (char* ptr = value)
                {
                    builder.Append(ptr, value.Length);
                }
            }
#endif
        }
        else
            AppendIdent(value);

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified read-only <see cref="char"/> sequence to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append(ReadOnlySequence<char> value, AppendMode mode = AppendMode.Continuous)
    {
        if (value.Length == 0)
            goto end;

        if (HandleMultilines(mode))
        {
            SequencePosition current = value.Start;
            if (value.TryGet(ref current, out ReadOnlyMemory<char> memory, true))
            {
                ReadOnlySpan<char> previous = memory.Span;
            next:
                AppendIdent(previous);
                if (value.TryGet(ref current, out memory, true))
                {
                    ReadOnlySpan<char> next = memory.Span;
                    if (next.Length > 0 && next[0] == '\n' && previous.Length > 0 && previous[previous.Length - 1] == '\r')
                    {
                        // Skip because it was an `\r\n` across two spans,
                        // and we already added the newline from the `\r` of the previous span
                        next = next.Slice(1);
                    }
                    previous = next;
                    goto next;
                }
            }
        }
        else
        {
            SequencePosition current = value.Start;
            EnsureIndentation();
            while (value.TryGet(ref current, out ReadOnlyMemory<char> memory, true))
            {
#if NET5_0_OR_GREATER
                builder.Append(memory);
#else
                ReadOnlySpan<char> span = memory.Span;
                unsafe
                {
                    fixed (char* ptr = span)
                    {
                        builder.Append(ptr, span.Length);
                    }
                }
#endif
            }
        }

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> representation of the Unicode characters in a specified read-only <see cref="char"/> span to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append(IEnumerable<char> value, AppendMode mode = AppendMode.Continuous)
    {
        switch (value)
        {
            case char[] array:
                return Append(array.AsSpan(), mode);
#if NET5_0_OR_GREATER
            case List<char> list:
                return Append(CollectionsMarshal.AsSpan(list), mode);
#endif
            case null:
                return this;
            default:
            {
                using IEnumerator<char> enumerator = value.GetEnumerator();
                if (HandleMultilines(mode))
                {
                    while (enumerator.MoveNext())
                    {
                        char c = enumerator.Current;
                        if (c is '\n' or '\r')
                        {
                            AppendLine();
                            if (c == '\r')
                            {
                                if (enumerator.MoveNext())
                                {
                                    c = enumerator.Current;
                                    if (c != '\n')
                                        EnsureIndentation().Append(c);
                                }
                                else
                                    break;
                            }
                        }
                        else
                            EnsureIndentation().Append(c);
                    }
                }
                else if (enumerator.MoveNext())
                {
                    StringBuilder builder = EnsureIndentation();
                    do
                        builder.Append(enumerator.Current);
                    while (enumerator.MoveNext());
                }
                return CheckEndNewline(mode);
            }
        }
    }

    /// <summary>
    /// Appends a specified number of copies of the <see cref="string"/> representation of a Unicode character to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="repeatCount">The number of times to append <paramref name="value"/>.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder Append(char value, int repeatCount, AppendMode mode = AppendMode.Continuous)
    {
        if (repeatCount == 0)
            goto end;

        if (HandleMultilines(mode) && value is '\n' or '\n')
        {
            isAtNewLine = true;
            string newLine = Environment.NewLine;
            StringBuilder builder = this.builder;
            for (int i = 0; i < repeatCount; i++)
                builder.Append(newLine);
            goto end;
        }

        EnsureIndentation().Append(value, repeatCount);

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends a copy of a substring within a specified <see cref="StringBuilder"/> to this instance.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="startIndex">The starting position of the substring within <paramref name="value"/>.</param>
    /// <param name="count">The number of characters in <paramref name="value"/> to append.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public unsafe IndentedSourceBuilder Append(StringBuilder? value, int startIndex, int count, AppendMode mode = AppendMode.Continuous)
    {
        if (value is null)
        {
            if (startIndex == 0 && count == 0)
                goto end;
            ThrowArgumentNullException_Value();
        }

#if !(NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER)
        if (startIndex == 0 && count == 0)
            return Append(value, mode);
        return Append(value.ToString(startIndex, count), mode);
#else
        if (startIndex < 0) ThrowArgumentOutOfRangeException_StartIndexIsNegative();
        if (count < 0) ThrowArgumentOutOfRangeException_CountIsNegative();
        int length = value.Length;
        if (count > length - startIndex) ThrowArgumentOutOfRangeException_StartIndexPlusCountIsLessThanLength();

        if (length == 0)
            goto end;

        if (HandleMultilines(mode))
        {
#if NET5_0_OR_GREATER
            string newLine = Environment.NewLine;
            int newLineLengthMinusOne = newLine.Length - 1;
            StringBuilder.ChunkEnumerator enumerator = value.GetChunks();
            if (enumerator.MoveNext())
            {
            skip:
                ReadOnlySpan<char> previous = enumerator.Current.Span;
                if (previous.Length > startIndex)
                {
                    startIndex -= previous.Length;
                    if (enumerator.MoveNext())
                        goto skip;
                    Debug.Fail("Error despite length was already checked.");
                    goto end;
                }
                else
                    previous = previous.Slice(startIndex);

            next:
                if (previous.Length > count)
                {
                    count -= previous.Length;
                }
                else if (count > 0)
                {
                    previous = previous.Slice(0, count);
                    count = 0;
                }
                else
                    goto end;

                AppendIdent(previous);
                if (enumerator.MoveNext())
                {
                    ReadOnlySpan<char> next = enumerator.Current.Span;
                    if (next.Length > 0 && next[0] == '\n' && previous.Length > 0 && previous[previous.Length - 1] == '\r')
                    {
                        // Skip because it was an `\r\n` across two spans,
                        // and we already added the newline from the `\r` of the previous span.
                        next = next.Slice(1);
                    }
                    previous = next;
                    goto next;
                }
            }
#else
            return Append(value.ToString(startIndex, count).AsSpan(), mode);
#endif
        }
        else
            EnsureIndentation().Append(value, startIndex, count);
#endif

    end:
        return CheckEndNewline(mode);
    }

    /// <summary>
    /// Appends <see cref="Environment.NewLine"/> to the end of this instance.<br/>
    /// The following append operation will be prefixed by the Indentation at the moment of the next append call, as long as the next append call is not empty nor is <see cref="AppendLine()"/>.
    /// </summary>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder AppendLine()
    {
        builder.Append(Environment.NewLine);
        isAtNewLine = true;
        return this;
    }

    /// <summary>
    /// Concatenates the <see cref="string"/> representation of the elements in a provided collection, usin the <see cref="string"/> representation of the specified separator between each element, then append the result to the current instance.
    /// </summary>
    /// <typeparam name="TSeparator">Type of value whose <see cref="string"/> representation will be appended as separator.</typeparam>
    /// <typeparam name="TValue">Type of value whose <see cref="string"/> representation will be appended as value.</typeparam>
    /// <param name="separator">The object whose <see cref="string"/> representation will be used as a separator. <paramref name="separator"/> is included in the joined strings only if <paramref name="values"/> has more than one element.</param>
    /// <param name="values">A collection that contains the objects whose <see cref="string"/> represetation will be concatenated and appended to the current instance.</param>
    /// <param name="separatorMode">Determines how the separator is appended.</param>
    /// <param name="valuesMode">Determines how each element in the values are appended.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedSourceBuilder AppendJoin<TSeparator, TValue>(TSeparator? separator, IEnumerable<TValue?> values, AppendMode separatorMode = AppendMode.Continuous, AppendMode valuesMode = AppendMode.Continuous)
    {
        if (!EndNewline(separatorMode) && (separator is null || (separator is string separator_ && string.IsNullOrEmpty(separator_))))
        {
            switch (values)
            {
                case TValue?[] array:
                {
                    foreach (TValue? value in array)
                        Append(value, valuesMode);
                    break;
                }
                case List<TValue?> list:
                {
                    foreach (TValue? value in list)
                        Append(value, valuesMode);
                    break;
                }
                case IList<TValue?> list:
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                        Append(list[i], valuesMode);
                    break;
                }
                case IReadOnlyList<TValue?> list:
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                        Append(list[i], valuesMode);
                    break;
                }
                default:
                {
                    foreach (TValue? value in values)
                        Append(value, valuesMode);
                    break;
                }
            }
        }
        else
        {
            switch (values)
            {
                case TValue?[] array:
                {
                    if (array.Length > 0)
                    {
                        for (int i = 0; i < array.Length - 1; i++)
                        {
                            Append(array[i], valuesMode);
                            Append(separator, separatorMode);
                        }
                        return Append(array[array.Length - 1], valuesMode);
                    }
                    break;
                }
                case List<TValue?> list:
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        for (int i = 0; i < count - 1; i++)
                        {
                            Append(list[i], valuesMode);
                            Append(separator, separatorMode);
                        }
                        return Append(list[count - 1], valuesMode);
                    }
                    break;
                }
                case IList<TValue?> list:
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        for (int i = 0; i < count - 1; i++)
                        {
                            Append(list[i], valuesMode);
                            Append(separator, separatorMode);
                        }
                        return Append(list[count - 1], valuesMode);
                    }
                    break;
                }
                case IReadOnlyList<TValue?> list:
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        for (int i = 0; i < count - 1; i++)
                        {
                            Append(list[i], valuesMode);
                            Append(separator, separatorMode);
                        }
                        return Append(list[count - 1], valuesMode);
                    }
                    break;
                }
                case null:
                    ThrowArgumentNullException_Value();
                    break;
                default:
                {
                    using IEnumerator<TValue?> enumerator = values.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                    again:
                        Append(enumerator.Current, valuesMode);
                        if (enumerator.MoveNext())
                        {
                            Append(separator, separatorMode);
                            goto again;
                        }
                    }
                    break;
                }
            }
        }
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat(IFormatProvider? provider, CompositeFormat format, ReadOnlySpan<object?> args, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(provider, format, args);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder AppendFormat(IFormatProvider? provider, CompositeFormat format, params ReadOnlySpan<object?> args) => AppendFormat(provider, format, args, AppendMode.Continuous);

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat<TArg0>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(provider, format, arg0);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat<TArg0, TArg1>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(provider, format, arg0, arg1);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <typeparam name="TArg2">The type of the third object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <param name="arg2">The third object to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat<TArg0, TArg1, TArg2>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1, TArg2 arg2, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(provider, format, arg0, arg1, arg2);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }
#endif

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the string representation of a corresponding argument in a parameter array.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, ReadOnlySpan<object?> args, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
#if NET9_0_OR_GREATER
        builder.AppendFormat(format, args);
#else
        switch (args.Length)
        {
            case 1:
                builder.AppendFormat(format, args[0]);
                break;
            case 2:
                builder.AppendFormat(format, args[0], args[1]);
                break;
            case 3:
                builder.AppendFormat(format, args[0], args[1], args[2]);
                break;
            default:
                builder.AppendFormat(format, args.ToArray());
                break;
        }
#endif
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the string representation of a corresponding argument in a parameter array.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params ReadOnlySpan<object?> args) => AppendFormat(format, args, AppendMode.Continuous);

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of a corresponding argument in a parameter array.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object?[] args, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(format, args);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of a corresponding argument in a parameter array using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, ReadOnlySpan<object?> args, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
#if NET9_0_OR_GREATER
        builder.AppendFormat(provider, format, args);
#else
        switch (args.Length)
        {
            case 1:
                builder.AppendFormat(provider, format, args[0]);
                break;
            case 2:
                builder.AppendFormat(provider, format, args[0], args[1]);
                break;
            case 3:
                builder.AppendFormat(provider, format, args[0], args[1], args[2]);
                break;
            default:
                builder.AppendFormat(provider, format, args.ToArray());
                break;
        }
#endif
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of a corresponding argument in a parameter array using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params ReadOnlySpan<object?> args) => AppendFormat(provider, format, args, AppendMode.Continuous);

    /// <summary>
    /// Appends the <see cref="string"/> returned by processing a composite format <see cref="string"/>, which contains zero or more format items, to this instance. Each format item is replaced by the <see cref="string"/> representation of a corresponding argument in a parameter array using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <param name="mode">Determines how the value is appended.</param>
    /// <returns>A reference to this instance after the formatted append operation has completed.</returns>
    public IndentedSourceBuilder AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object?[] args, AppendMode mode = AppendMode.Continuous)
    {
        StringBuilder builder = this.builder;
        int oldLength = builder.Length;
        EnsureIndentation();
        int length = builder.Length;
        builder.AppendFormat(provider, format, args);
        int newLength = builder.Length;
        if (newLength == length)
        {
            if (oldLength != length)
            {
                isAtNewLine = true;
                builder.Length = oldLength;
            }
            return CheckEndNewline(mode);
        }
        else
            return AddIndentationFrom(length, mode);
    }

    /// <summary>
    /// Removes all characters from the current <see cref="IndentedSourceBuilder"/> instance.
    /// </summary>
    /// <returns>An object whose <see cref="Length"/> is 0 (zero).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder Clear()
    {
        builder.Clear();
        IndentCount = 0;
        isAtNewLine = false;
        return this;
    }

    /// <summary>
    /// Copies the characters from a specified segment of this instance to a specified segment of a destination <see cref="char"/> array.
    /// </summary>
    /// <param name="sourceIndex">The starting position in this instance where characters will be copied from. The index is zero-based.</param>
    /// <param name="destination">The array where characters will be copied.</param>
    /// <param name="destinationIndex">The starting position in <paramref name="destination"/> where characters will be copied. The index is zero-based.</param>
    /// <param name="count">The number of characters to be copied.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) => builder.CopyTo(sourceIndex, destination, destinationIndex, count);

#if NET5_0_OR_GREATER
    /// <summary>
    /// Copies the characters from a specified segment of this instance to a specified segment of a destination <see cref="char"/> span.
    /// </summary>
    /// <param name="sourceIndex">The starting position in this instance where characters will be copied from. The index is zero-based.</param>
    /// <param name="destination">The array where characters will be copied.</param>
    /// <param name="count">The number of characters to be copied.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int sourceIndex, Span<char> destination, int count) => builder.CopyTo(sourceIndex, destination, count);
#endif

    /// <summary>
    /// Ensures that the capacity of this instance of <see cref="IndentedSourceBuilder"/> is at least the specified value.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnsureCapacity(int capacity) => builder.EnsureCapacity(capacity);

    /// <summary>
    /// Ensures that the capacity of this instance of <see cref="IndentedSourceBuilder"/> is at least the specified value plus current <see cref="IndentedSourceBuilder.Length"/>.
    /// </summary>
    /// <param name="capacity">The minimum remaining capacity to ensure.</param>
    /// <returns>The new remaining capacity of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnsureRemainingCapacity(int capacity)
    {
        StringBuilder builder = this.builder;
        int length = builder.Length;
        return builder.EnsureCapacity(capacity + length) - length;
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// Returns a value indicating whether the characters in this instance are equal to the characters in a specified read-only character span.
    /// </summary>
    /// <param name="span">The character span to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the characters in this instance and span are the same; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ReadOnlySpan<char> span) => builder.Equals(span);

    /// <summary>
    /// Returns a value indicating whether the characters in this instance are equal to the characters in a specified read-only character span.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the characters in this instance and <see cref="string"/> are the same; otherwise, <see langword="false"/>.</returns>
    public bool Equals(string? value) => builder.Equals(value.AsSpan());
#endif

    /// <summary>
    /// Returns a value indicating whether this instance is equal to a specified object.
    /// </summary>
    /// <param name="sb">An object to compare with this instance, or null.</param>
    /// <returns><see langword="true"/> if this instance and <paramref name="sb"/> have equal <see cref="string"/>, <see cref="Capacity"/>, and <see cref="MaxCapacity"/> values; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringBuilder sb) => builder.Equals(sb);

    /// <summary>
    /// Returns a value indicating whether this instance is equal to a specified object.
    /// </summary>
    /// <param name="sb">An object to compare with this instance, or null.</param>
    /// <returns><see langword="true"/> if this instance and <paramref name="sb"/> have equal <see cref="string"/>, <see cref="Capacity"/>, and <see cref="MaxCapacity"/> values; otherwise, <see langword="false"/>.</returns>
    public bool Equals(IndentedSourceBuilder sb) => builder.Equals(sb.builder);

    /// <summary>
    /// Appends a <c>'{'</c> to this instance, add new line and increases the Indentation.
    /// </summary>
    /// <returns>A reference to this instance after the brace operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder OpenBrace()
    {
        Append('{', AppendMode.SingleLine);
        IndentCount++;
        return this;
    }

    /// <summary>
    /// Appends a <c>'}'</c> to this instance, add new line and decreases the Indentation.
    /// </summary>
    /// <returns>A reference to this instance after the unbrace operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder CloseBrace()
    {
        IndentCount--;
        return Append('}', AppendMode.SingleLine);
    }

    /// <summary>
    /// Increases the indentation by <paramref name="depth"/>.
    /// </summary>
    /// <param name="depth">Indentation levels to increase</param>
    /// <returns>A reference to this instance after the ident operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder IncrementIndent(int depth = 1)
    {
        IndentCount += depth;
        return this;
    }

    /// <summary>
    /// Decreases the indentation by <paramref name="depth"/>.
    /// </summary>
    /// <param name="depth">Indentation levels to increase</param>
    /// <returns>A reference to this instance after the unident operation has completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndentedSourceBuilder DecrementIndent(int depth = 1)
    {
        IndentCount -= depth;
        return this;
    }

    /// <summary>
    /// Converts the value of this instance to a <see cref="string"/>.
    /// </summary>
    /// <returns>A <see cref="string"/> whose value is the same as this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => builder.ToString();

    /// <summary>
    /// Converts the value of a substring of this instance to a <see cref="string"/>.
    /// </summary>
    /// <param name="startIndex">The starting position of the substring in this instance.</param>
    /// <param name="length">The length of the substring.</param>
    /// <returns>A <see cref="string"/> whose value is the same as the specified substring of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(int startIndex, int length) => builder.ToString(startIndex, length);

    private void AppendIdent(ReadOnlySpan<char> value)
    {
        Debug.Assert(value.Length > 0);
        int i = value.IndexOfAny('\r', '\n');
        if (i >= 0)
        {
            if (i != 0)
                EnsureIndentation();
            AppendIdent(value, i);
        }
        else
        {
            StringBuilder builder = EnsureIndentation();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            builder.Append(value);
#else
            unsafe
            {
                fixed (char* ptr = value)
                {
                    builder.Append(ptr, value.Length);
                }
            }
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendIdent(ReadOnlySpan<char> value, int index)
    {
        StringBuilder builder = this.builder;

        if (index > 0)
        {
            ReadOnlySpan<char> span = value.Slice(0, index);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            builder.Append(span);
#else
            unsafe
            {
                fixed (char* ptr = span)
                {
                    builder.Append(ptr, span.Length);
                }
            }
#endif
        }

        AppendLine();

        if (value[index] == '\r' && value.Length > index + 1 && value[index + 1] == '\n')
            index += 2;
        else
            index++;

        if (value.Length > index)
            AppendIdent(value.Slice(index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private StringBuilder EnsureIndentation()
    {
        StringBuilder builder = this.builder;
        if (isAtNewLine && IndentCount > 0)
            return ForceIndentation();
        return builder;
    }

    private StringBuilder ForceIndentation()
    {
        isAtNewLine = false;
        builder.Append(INDENTATION_SEPARATOR, IndentCount * INDENTATION_COUNT);
        return builder;
    }

    private IndentedSourceBuilder AddIndentationFrom(int startIndex, AppendMode mode)
    {
        if (!HandleMultilines(mode))
            goto return_;

        StringBuilder builder = this.builder;

#if NET5_0_OR_GREATER
        StringBuilder.ChunkEnumerator chunks = builder.GetChunks();
        int i = 0;
        while (chunks.MoveNext())
        {
            ReadOnlyMemory<char> current = chunks.Current;
            if (i + current.Length < startIndex)
                i += current.Length;
            else
            {
                current = current.Slice(startIndex - i);
            again:
                int index = current.Span.IndexOfAny('\n', '\r');
                if (index != -1)
                {
                    startIndex = i + index;
                    goto has;
                }
                else if (chunks.MoveNext())
                {
                    i += current.Length;
                    current = chunks.Current;
                    goto again;
                }
                else
                    break;
            }
        }
#else
        for (int i = 0; i < builder.Length; i++)
        {
            if (builder[i] is '\n' or '\r')
            {
                startIndex = i;
                goto has;
            }
        }
#endif

        goto return_;

    has:
        builder.Replace("\r\n", "\n", startIndex, builder.Length - startIndex);
        builder.Replace('\r', '\n', startIndex, builder.Length - startIndex);
        if (Environment.NewLine != "\n")
            builder.Replace("\n", Environment.NewLine, startIndex, builder.Length - startIndex);
        builder.Replace(Environment.NewLine, Helpers.ReplaceIndentationFor(IndentCount), startIndex, builder.Length - startIndex);

    return_:
        return CheckEndNewline(mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HandleMultilines(AppendMode mode) => ((int)mode & SUPPORT_MULTILINE) == SUPPORT_MULTILINE;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EndNewline(AppendMode mode) => ((int)mode & END_WITH_NEW_LINE) == END_WITH_NEW_LINE;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IndentedSourceBuilder CheckEndNewline(AppendMode mode) => EndNewline(mode) ? AppendLine() : this;

    [DoesNotReturn]
    private static void ThrowArgumentNullException_Value()
         => throw new ArgumentNullException("value");

    private static void ThrowArgumentOutOfRangeException_StartIndexIsNegative()
        => throw new ArgumentOutOfRangeException("startIndex", "The start index can't be negative.");

    private static void ThrowArgumentOutOfRangeException_CountIsNegative()
        => throw new ArgumentOutOfRangeException("count", "The start index can't be negative.");

    private static void ThrowArgumentOutOfRangeException_StartIndexPlusCountIsLessThanLength()
        => throw new ArgumentOutOfRangeException("startIndex", "The sum of startIndex and count must be lower than value's length.");
}