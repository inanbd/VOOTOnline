# Deviations from the original CodeSmith templates

The generator is a port of the eleven `.cst` templates in `CodeSmithTemplate.zip`. It
reproduces their output shape, folder layout and file names. This file records every place
the port deliberately differs, and why.

## Defects in the originals that are fixed here

### 1. Stored procedure names did not match the names the data access layer called

`MainDataAccess.cst` built its procedure-name constants with `GetClassName()`, which strips
`TablePrefix`:

```
private const string INSERTUSER = "InsertUser";
```

`SP.cst` built the actual procedure with `GetEntityName()`, whose prefix-stripping was
commented out:

```
CREATE PROCEDURE Inserttbl_User
```

With the configured `TablePrefix` of `tbl_`, every generated data access call targeted a
procedure that did not exist. Both sides now derive names from the same `NameResolver`, so
they cannot drift apart. Covered by
`CodeGeneratorTests.Stored_procedure_names_match_the_constants_the_data_access_layer_calls`.

### 2. `ProcedurePrefix` was declared but never used

`SP.cst` exposed a `ProcedurePrefix` property and ignored it. It is now applied to every
procedure name, on both the T-SQL and the C# side. It defaults to empty, so output is
unchanged unless it is set.

### 3. Every table was classified as a junction table

`Voot.cst` decided a table was a mapping table with:

```csharp
if (table.Name.Split('_').Length > 1) return true;
```

With `TablePrefix = "tbl_"` that matched every table, so all of them would have been emitted
against `BaseRelationData` and none would have received a business manager. The prefix is now
stripped before the test, which is what the rule was clearly meant to do.

### 4. The business manager overwrote the identity key with a row count

`MainBusinessManager.cst` emitted:

```csharp
Int32 _UserId = data.Insert(userObject);   // returns rows affected, not the key
if (_UserId > 0) { userObject.UserId = _UserId; }
```

`Insert` returns the number of rows affected, so a successful single-row insert set the
entity's key to `1`, discarding the identity value the data access layer had just read out of
the procedure's output parameter. The generated manager now leaves the key alone.

### 5. Two generated type expressions did not compile

`GetCSharpVariableType` produced `Nullable<Char[]>` for a nullable `nchar` column and
`Nullable<Object>` for `sql_variant`. Neither is legal C#: `Nullable<T>` requires a struct.
Both now map to the plain reference type, which is already nullable.

### 6. Unmapped column types produced `__UNKNOWN__`

Columns typed `date`, `datetime2`, `smalldatetime`, `xml` or `sysname` fell through the type
switches and emitted `__UNKNOWN__date` and similar into the source. These types are now
mapped. Anything still unrecognised skips the table and raises a visible error diagnostic
instead of emitting text that cannot compile.

`date` in particular is now carried end to end: `DateTime` in the entity, `pDateTime` for the
parameter, `reader.GetDateTime` for the read, and `date` — not a widened `datetime` — in the
procedure's parameter list.

### 6a. Nullable `uniqueidentifier`

A nullable `uniqueidentifier` maps to `Nullable<Guid>` (Legacy) or `Guid?` (Modern), is read
behind an `IsDBNull` guard, and is passed with `pGuid`. The cases that broke in the originals
were the ones around it rather than the mapping itself:

- as a **nullable foreign key**, the generated manager handed `Nullable<Guid>` straight to a
  `Get(Guid, bool)` overload, which does not compile — see defect 12;
- as a **primary key**, the maximum-key procedure emitted `ISNULL(MAX(key), 0)` against a
  `uniqueidentifier`, an operand type clash, and `GetRowCount` returned the key's type — see
  defect 11;
- the out-parameter helper for a GUID key was emitted with a value argument that the integer
  case did not take.

All three are fixed, and both types are covered by tests and verified against SQL Server.

### 7. SELECT order was not guaranteed to match the reader offsets

Generated `FillObject` walks columns in table order and then calls
`FillBaseObject(obj, reader, start + N)` for the framework-owned audit columns. The generated
`SELECT` listed the primary key first and then the remaining columns in table order. The two
orders agree only when the key is physically first and the audit columns physically last.
Both sides now use one explicit projection order — key, then non-audit columns in table
order, then audit columns — so the offsets line up whatever the physical layout.

### 8. `Equals` threw on null

`Entity.cst` emitted `obj.GetType() != typeof(X)`, which throws a `NullReferenceException`
when passed null. The generated override now uses a pattern match.

### 9. Paging used `@@IDENTITY` and unvalidated sort input

The generated insert used `@@IDENTITY`, which returns the last identity inserted in the
session — including one inserted by a trigger on another table. It now uses
`SCOPE_IDENTITY()`.

`GetPaged` concatenated `@SortColumn` and `@SortOrder` straight into dynamic SQL. The sort
column is now checked against `sys.columns` for the target table and the direction against a
fixed pair of literals, so neither can carry an injection. See the note below about the
`WHERE` fragment, which cannot be secured the same way.

### 10. `GetPaged` passed an expression to `sp_executesql`

The paging procedure computed its offset inline:

```sql
EXEC sp_executesql @Sql, N'@Skip int, @Take int',
    @Skip = @PageIndex * @RowPerPage, @Take = @RowPerPage;
```

`sp_executesql` takes variables, not expressions, so every paging procedure failed to create
with `Msg 102, incorrect syntax`. The offset is now computed into a variable first. Found by
executing the generated scripts against SQL Server.

### 11. A non-numeric key produced a procedure that would not compile

The maximum-key procedure emitted `SELECT ISNULL(MAX([TagId]), 0)`. Against a
`uniqueidentifier` key that is `Msg 206, operand type clash`, and a GUID has no meaningful
maximum anyway. The procedure and the matching data access method are now emitted only for
numeric keys.

Relatedly, `GetRowCount` returned the *primary key's* type, which is wrong for any table
whose key is not numeric: `COUNT(*)` is an `int` whatever the key is. It now returns `int`.

### 12. A nullable foreign key was passed to a non-nullable parameter

The generated manager loaded related entities with:

```csharp
customerObject.CountryIdObject = countryManager.Get(customerObject.CountryId, fillChilds);
```

When the foreign key column is nullable, `CountryId` is `Nullable<Int32>` and `Get` takes
`Int32`, so the generated code did not compile. The call is now guarded on `HasValue` and
passes `.Value`, leaving the navigation property null when the key is unset. Found by
compiling the generated output against the framework contract.

## Intentional additions

- **Output style.** Each project selects Legacy (faithful to the originals:
  `System.Data.SqlClient`, WCF `[DataContract]`, block namespaces, tabs) or Modern
  (`Microsoft.Data.SqlClient`, file-scoped namespaces, nullable reference types, C# keyword
  type names, no WCF attributes). The class and method surface is identical either way, so
  both compile against the same framework assembly.
- **Generated-file header.** Files under a `Bases` folder carry an `<auto-generated>` banner
  warning that edits are lost. The hand-editable partials deliberately do not.
- **`README.txt` in the archive root.** Lists the layout and the framework types the
  generated code expects, since the archive ships generated files only.
- **Diagnostics.** A table that fails generation is reported and skipped instead of aborting
  the run, so one bad table cannot cost the whole archive.

### Extra data access constructors

The hand-editable half of each data access class (`{Root}/{DataAccess}/{Entity}DataAccess.cs`,
not the regenerated file under `Bases`) now also carries:

```csharp
public {Entity}DataAccess() { }
public {Entity}DataAccess(string ConnectionStr) : base(ConnectionStr) { }
```

They live in the editable half so the constructor set stays yours to change. Both require the
framework's `BaseDataAccess` and `BaseRelationData` to provide a parameterless constructor and
one taking a connection string; each archive's README lists that requirement.

## Known limitations carried over from the originals

- **Single-column primary keys.** The data access and manager templates assume one key
  column. Tables with a composite key generate against the first column only.
- **`GetByQuery` takes a raw SQL fragment.** It is concatenated into dynamic SQL inside the
  procedure. The sort inputs to `GetPaged` are validated; a caller-supplied `WHERE` fragment
  cannot be, by construction. Both are trusted-caller features: never pass a value derived
  from end-user input. The generated XML docs and the archive README both say so.
- **Partial classes are emitted on every run.** They are scaffolding. Take them on the first
  generation only, or hand-written code is overwritten. The README in each archive repeats
  this.
