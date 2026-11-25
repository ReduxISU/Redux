using System.Runtime.CompilerServices;
using System.Text;

namespace API.Interfaces.Steps;

/// <summary>
/// What decides whether a step's text is formatted: a <see cref="StepRecorder{TPartial}"/>, or a
/// <see cref="RunSteps{TState, TPartial}"/> or <see cref="SearchSteps{TState, TChoice, TPartial}"/> on one. The text handlers below ask it before formatting anything.
/// </summary>
interface IStepGate {
    /// <summary>Whether the recorder is recording. An Accept and a Done are built whenever it is, even past the cap.</summary>
    bool Recording { get; }

    /// <summary>Whether the next Try, Reject or Backtrack will be kept: recording, and the cap not yet reached.</summary>
    bool ShowsNextStep { get; }
}

/// <summary>What decides whether <see cref="SearchSteps{TState, TChoice, TPartial}.Expand"/> records a Backtrack to a state.</summary>
interface IBacktrackGate {
    /// <summary>Whether expanding <paramref name="state"/> records a Backtrack that will be kept.</summary>
    bool ShowsBacktrackTo(object state);
}

/// <summary>
/// The text of a Try, Reject or Backtrack (its caption, or a search-tree bound). Write it as an interpolated string,
/// <c>$"Skip {city}: ..."</c>: nothing in the braces is evaluated, and nothing is formatted, unless the recorder will keep
/// the step (it is recording and the cap has not been reached), like the lambdas of the older overloads. Only an interpolated
/// string converts: a plain string or a method call does not compile (CS9205), since it would be built on every call, kept or
/// not. Write fixed text as <c>$"no edge"</c> and computed text as <c>$"{Note(state)}"</c>.
/// </summary>
[InterpolatedStringHandler]
ref struct ShownText {
    // The text being built; null when it is not formatted.
    private readonly StringBuilder? _text;

    public ShownText(int literalLength, int formattedCount, IStepGate gate, out bool shouldAppend) {
        shouldAppend = gate.ShowsNextStep;
        _text = shouldAppend ? StepTextFormat.Builder(literalLength, formattedCount) : null;
    }

    // For SearchSteps.Expand: formatted only when the expansion is a kept Backtrack.
    public ShownText(int literalLength, int formattedCount, IBacktrackGate gate, object state, out bool shouldAppend) {
        shouldAppend = gate.ShowsBacktrackTo(state);
        _text = shouldAppend ? StepTextFormat.Builder(literalLength, formattedCount) : null;
    }


    public void AppendLiteral(string value) => _text!.Append(value);
    public void AppendFormatted<T>(T value) => StepTextFormat.Into(_text!).AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => StepTextFormat.Into(_text!).AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => StepTextFormat.Into(_text!).AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => StepTextFormat.Into(_text!).AppendFormatted(value, alignment, format);
    public void AppendFormatted(string? value) => _text!.Append(value);
    public void AppendFormatted(ReadOnlySpan<char> value) => _text!.Append(value);

    /// <summary>The text; null when it was not formatted (the step is not kept, or no text was given).</summary>
    internal string? ToStringAndClear() => _text?.ToString();
}

/// <summary>
/// The text of an Accept or a Done, or a search-tree node's label: like <see cref="ShownText"/>, but formatted whenever the
/// recorder is recording, past the cap too. The recorder keeps the last Accept past the cap, so it builds every one (see
/// <see cref="StepRecorder{TPartial}"/>), and a node's label is needed by any later step that names the node.
/// </summary>
[InterpolatedStringHandler]
ref struct KeptText {
    // The text being built; null when it is not formatted.
    private readonly StringBuilder? _text;

    public KeptText(int literalLength, int formattedCount, IStepGate gate, out bool shouldAppend) {
        shouldAppend = gate.Recording;
        _text = shouldAppend ? StepTextFormat.Builder(literalLength, formattedCount) : null;
    }


    public void AppendLiteral(string value) => _text!.Append(value);
    public void AppendFormatted<T>(T value) => StepTextFormat.Into(_text!).AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => StepTextFormat.Into(_text!).AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => StepTextFormat.Into(_text!).AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => StepTextFormat.Into(_text!).AppendFormatted(value, alignment, format);
    public void AppendFormatted(string? value) => _text!.Append(value);
    public void AppendFormatted(ReadOnlySpan<char> value) => _text!.Append(value);

    /// <summary>The text; null when it was not formatted (the recorder is off, or no text was given).</summary>
    internal string? ToStringAndClear() => _text?.ToString();
}

/// <summary>
/// How <see cref="ShownText"/> and <see cref="KeptText"/> build their text. They hold only a <see cref="StringBuilder"/>,
/// null when the text is not formatted, so a step whose text is skipped (every step of a run with recording off) passes one
/// null reference rather than a whole string handler. Each hole is formatted by the framework's own
/// <see cref="StringBuilder.AppendInterpolatedStringHandler"/>, so the text reads exactly as <c>$"..."</c> would.
/// </summary>
static class StepTextFormat {
    /// <summary>A builder sized for the literal text and a few characters per hole.</summary>
    public static StringBuilder Builder(int literalLength, int formattedCount) => new(literalLength + formattedCount * 11);

    /// <summary>The framework's handler appending to <paramref name="text"/>, for one hole.</summary>
    public static StringBuilder.AppendInterpolatedStringHandler Into(StringBuilder text) => new(0, 1, text);
}
