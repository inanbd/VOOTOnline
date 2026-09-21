using System.Text;

namespace Voot.CodeGen.Generation;

/// <summary>
/// Indentation-aware text builder used by every emitter. Keeps emitters free of manual
/// tab counting, which is what made the original .cst templates hard to follow.
/// </summary>
public sealed class CodeWriter(string indentUnit = "\t")
{
    private readonly StringBuilder _builder = new();
    private int _level;
    private bool _lastWasBlank = true;

    /// <summary>Writes a line at the current indentation. An empty string writes a bare newline.</summary>
    public CodeWriter Line(string text = "")
    {
        if (text.Length == 0)
        {
            _builder.Append('\n');
            _lastWasBlank = true;
            return this;
        }

        for (var i = 0; i < _level; i++)
        {
            _builder.Append(indentUnit);
        }

        _builder.Append(text).Append('\n');
        _lastWasBlank = false;
        return this;
    }

    /// <summary>Writes each line of a multi-line string at the current indentation.</summary>
    public CodeWriter Lines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Line(line);
        }

        return this;
    }

    public CodeWriter LineIf(bool condition, string text) => condition ? Line(text) : this;

    /// <summary>Writes a blank line unless the previous write already produced one.</summary>
    public CodeWriter Blank()
    {
        if (!_lastWasBlank)
        {
            Line();
        }

        return this;
    }

    /// <summary>Writes <paramref name="header"/> followed by a braced block; dispose closes it.</summary>
    public Scope Block(string header)
    {
        Line(header);
        Line("{");
        _level++;
        return new Scope(this, "}");
    }

    /// <summary>Writes a <c>#region</c> / <c>#endregion</c> pair; dispose closes it.</summary>
    public Scope Region(string name)
    {
        Line($"#region {name}");
        return new Scope(this, "#endregion", indents: false);
    }

    public CodeWriter Indent()
    {
        _level++;
        return this;
    }

    public CodeWriter Outdent()
    {
        if (_level > 0)
        {
            _level--;
        }

        return this;
    }

    public override string ToString() => _builder.ToString().TrimEnd('\n') + "\n";

    /// <summary>Closes a block or region when disposed.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly CodeWriter _writer;
        private readonly string _closing;
        private readonly bool _indents;

        internal Scope(CodeWriter writer, string closing, bool indents = true)
        {
            _writer = writer;
            _closing = closing;
            _indents = indents;
        }

        public void Dispose()
        {
            if (_indents)
            {
                _writer.Outdent();
            }

            _writer.Line(_closing);
        }
    }
}
