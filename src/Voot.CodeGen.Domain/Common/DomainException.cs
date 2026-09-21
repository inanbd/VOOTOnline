namespace Voot.CodeGen.Domain.Common;

/// <summary>
/// A rule violation that should be shown to the user rather than logged as a fault,
/// e.g. "a project with that name already exists".
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The requested entity does not exist, or the caller may not see it.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string what) : base($"{what} was not found.") { }
}

/// <summary>The caller is authenticated but not permitted to act on this resource.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message) { }
}
