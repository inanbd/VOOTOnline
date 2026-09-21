using Voot.CodeGen.Application.Abstractions;

namespace Voot.CodeGen.Infrastructure.Time;

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
