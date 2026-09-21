namespace Voot.CodeGen.Application.Abstractions;

/// <summary>Wraps the system clock so run timings can be asserted in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
