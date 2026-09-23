using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation;

/// <summary>Turns a schema snapshot into the set of files that go into the archive.</summary>
public interface ICodeGenerator
{
    /// <param name="selection">
    /// Limits the pass to some tables, keeping the whole schema as context; null generates every table.
    /// </param>
    GenerationResult Generate(DatabaseModel database, GenerationSettings settings, TableSelection? selection = null);
}
