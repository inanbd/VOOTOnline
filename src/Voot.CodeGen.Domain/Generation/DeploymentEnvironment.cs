namespace Voot.CodeGen.Domain.Generation;

/// <summary>The environments a schema change is tracked against after it is applied.</summary>
public enum DeploymentEnvironment
{
    Development = 0,
    Production = 1
}

/// <summary>Whether a mark recorded a change moving into an environment, or being taken back out.</summary>
public enum DeploymentAction
{
    Marked = 0,
    Unmarked = 1
}
