using System.Runtime.CompilerServices;

namespace API.Interfaces.Steps;

/// <summary>
/// What decides whether a step's text is formatted: a <see cref="StepRecorder{TPartial}"/>, or a
/// <see cref="SearchTree{TState, TPartial}"/> on one. The text handlers below ask it before formatting anything.
/// </summary>
interface IStepGate {
    /// <summary>Whether the recorder is recording. An Accept and a Done are built whenever it is, even past the cap.</summary>
    bool Recording { get; }

    /// <summary>Whether the next Try, Reject or Backtrack will be kept: recording, and the cap not yet reached.</summary>
    bool ShowsNextStep { get; }
}

/// <summary>What decides whether <see cref="SearchTree{TState, TPartial}.Expand"/> records a Backtrack to a state.</summary>
interface IBacktrackGate {
    /// <summary>Whether expanding <paramref name="state"/> records a Backtrack that will be kept.</summary>
    bool ShowsBacktrackTo(object state);
}

/// <summary>
/// The text of a Try, Reject or Backtrack (its caption, or a search-tree bound). Write it as an interpolated string,
/// <c>$"Skip {city}: ..."</c>: nothing in the braces is evaluated, and nothing is formatted, unless the recorder will keep
/// the step (it is recording and the cap has not been reached), like the lambdas of the older overloads. A plain string
/// converts too, for text that is already at hand.
/// </summary>
[InterpolatedStringHandler]
ref struct ShownText {
    private DefaultInterpolatedStringHandler _text;
    private readonly string? _plain;
    private readonly bool _on;

    public ShownText(int literalLength, int formattedCount, IStepGate gate, out bool shouldAppend) {
        _on = shouldAppend = gate.ShowsNextStep;
        _text = _on ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
        _plain = null;
    }

    // For SearchTree.Expand: formatted only when the expansion is a kept Backtrack.
    public ShownText(int literalLength, int formattedCount, IBacktrackGate gate, object state, out bool shouldAppend) {
        _on = shouldAppend = gate.ShowsBacktrackTo(state);
        _text = _on ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
        _plain = null;
    }

    private ShownText(string? plain) {
        _on = true;
        _plain = plain;
        _text = default;
    }

    public static implicit operator ShownText(string? plain) => new(plain);

    public void AppendLiteral(string value) => _text.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _text.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _text.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => _text.AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => _text.AppendFormatted(value, alignment, format);
    public void AppendFormatted(string? value) => _text.AppendFormatted(value);
    public void AppendFormatted(ReadOnlySpan<char> value) => _text.AppendFormatted(value);

    /// <summary>The text; null when it was not formatted (the step is not kept) or no text was given.</summary>
    internal string? ToStringAndClear() => !_on ? null : _plain ?? _text.ToStringAndClear();
}

/// <summary>
/// The text of an Accept or a Done, or a search-tree node's label: like <see cref="ShownText"/>, but formatted whenever the
/// recorder is recording, past the cap too. The recorder keeps the last Accept past the cap, so it builds every one (see
/// <see cref="StepRecorder{TPartial}"/>), and a node's label is needed by any later step that names the node.
/// </summary>
[InterpolatedStringHandler]
ref struct KeptText {
    private DefaultInterpolatedStringHandler _text;
    private readonly string? _plain;
    private readonly bool _on;

    public KeptText(int literalLength, int formattedCount, IStepGate gate, out bool shouldAppend) {
        _on = shouldAppend = gate.Recording;
        _text = _on ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
        _plain = null;
    }

    private KeptText(string? plain) {
        _on = true;
        _plain = plain;
        _text = default;
    }

    public static implicit operator KeptText(string? plain) => new(plain);

    public void AppendLiteral(string value) => _text.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _text.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _text.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => _text.AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => _text.AppendFormatted(value, alignment, format);
    public void AppendFormatted(string? value) => _text.AppendFormatted(value);
    public void AppendFormatted(ReadOnlySpan<char> value) => _text.AppendFormatted(value);

    /// <summary>The text; null when it was not formatted (the recorder is off) or no text was given.</summary>
    internal string? ToStringAndClear() => !_on ? null : _plain ?? _text.ToStringAndClear();
}
