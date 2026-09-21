namespace Voot.CodeGen.Domain.Identity;

/// <summary>Identity role row, persisted by a Dapper-backed role store.</summary>
public sealed class ApplicationRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? Name { get; set; }

    public string? NormalizedName { get; set; }

    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
