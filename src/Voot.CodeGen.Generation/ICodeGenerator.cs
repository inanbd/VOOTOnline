using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation;

/// <summary>Turns a schema snapshot into the set of files that go into the archive.</summary>
public interface ICodeGenerator
{
    GenerationResult Generate(DatabaseModel database, GenerationSettings settings);
}
