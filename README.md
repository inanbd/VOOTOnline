# Voot CodeGen

A .NET 10 MVC application that regenerates a CodeSmith-style data layer from a SQL Server
schema. Users submit SQL table changes against a project; the application applies them, re-reads
the schema, regenerates the entities, data access, business managers and stored procedures, and
hands back a zip.

It replaces the CodeSmith template suite in `CodeSmithTemplate.zip`: the eleven `.cst`
templates are ported to C# emitters, and the SchemaExplorer provider is replaced by a reader
over SQL Server's catalog views. There is no CodeSmith dependency and no Entity Framework.

## What it does

- **Projects.** An administrator creates a project: a target SQL Server connection string
  (encrypted at rest) plus the generator settings the original `.csp` property set carried.
- **Access.** Administrators reach every project. Everyone else reaches only the projects they
  are assigned to.
- **Structure.** Anyone on a project can browse its database's current table structures —
  columns, types, defaults, keys, foreign keys and indexes — picking tables individually or
  all at once. The view is read live, so it always reflects the database as it is now.
- **Ad-hoc SQL.** The same page runs SQL against the project's database and shows the
  structural diff it produced: tables added or dropped, columns added, dropped or altered.
  Every script is recorded in the project's change log with its outcome and a summary of what
  it changed. Running SQL here does not regenerate the code; that stays an explicit step, and
  the page links to it.
- **Dev and production trackers.** Each applied change carries a checkbox per environment.
  The page shows how many changes are still outstanding for each, combines them into one
  replayable script in a copyable dialog, and can mark everything remaining in one click. Every
  move — in or back out — is recorded with who did it and when.
- **Timeline.** Changes and deployment moves are shown as one chronological timeline rather
  than a flat table, so a change and its journey to production read together.
- **Sample inserts.** Each table offers a ready-to-run `INSERT` with placeholder data, for one
  table or the whole selection, in a dialog with a copy button. Identity, computed and
  rowversion columns are left out because the server assigns them.
- **Changes.** An assigned user submits SQL. It runs against that project's database, in one
  transaction by default, and the script is kept permanently as the audit record.
- **Generation.** On success the schema is re-read and every table is regenerated. A table that
  fails is reported and skipped rather than losing the whole batch.
- **Download.** The output is zipped, stored, and re-downloadable from the project's history.
- **History.** Every change and run is recorded with its log, its diagnostics, and the list of
  files it produced.

## Layout

```
src/
  Voot.CodeGen.Domain           entities, the schema model, no dependencies
  Voot.CodeGen.Generation       the eleven emitters; depends only on Domain
  Voot.CodeGen.Application      ports and use cases
  Voot.CodeGen.Infrastructure   Dapper, SQL Server, Identity stores, storage, the run worker
  Voot.CodeGen.Web              MVC controllers and views
tests/
  Voot.CodeGen.Generation.Tests   the generator, over a synthetic schema
  Voot.CodeGen.Application.Tests  the GO splitter, archive paths, download naming
```

Dependencies point inward: `Web -> Infrastructure -> Application -> Domain`, with `Generation`
depending only on `Domain`.

## Running it

Needs .NET 10 and a SQL Server instance. The application creates its own database and applies
its schema on first start.

```bash
export ConnectionStrings__Application="Server=localhost;Database=VootCodeGen;User ID=sa;Password=...;TrustServerCertificate=True"
export SeedAdministrator__UserName="admin"
export SeedAdministrator__Password="<a strong password>"

dotnet run --project src/Voot.CodeGen.Web
```

The seed administrator is created only when no administrator exists yet, and an existing
account's password is never reset. Supply the password through an environment variable or user
secrets — never a committed `appsettings` file.

### Configuration

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Application` | The database this application stores its own data in |
| `ArtifactStorage:RootPath` | Where generated archives are written (default `App_Data/artifacts`) |
| `ArtifactStorage:RetentionDays` | Days to keep archive bytes; `0` keeps them forever |
| `SeedAdministrator:UserName` / `:Password` | First administrator, created only if none exists |

## Identity without Entity Framework

ASP.NET Core Identity normally persists through EF Core. Here the store contracts
(`IUserStore`, `IUserPasswordStore`, `IUserEmailStore`, `IUserSecurityStampStore`,
`IUserLockoutStore`, `IUserRoleStore`, `IUserClaimStore`, `IUserPhoneNumberStore`,
`IUserTwoFactorStore` and `IRoleStore`) are implemented directly over Dapper, so password
hashing, lockout, security stamps, roles and claims all work with no EF anywhere in the
solution. The application's own tables come from idempotent SQL scripts applied at startup and
recorded in `__SchemaVersions`.

## Generated output

Each archive contains generated files only — it compiles against your own framework assembly,
whose required surface the archive's `README.txt` spells out. Each project chooses a style:

- **Legacy** matches the original templates: `System.Data.SqlClient`, WCF
  `[DataContract]`/`[CollectionDataContract]`, block namespaces, tabs.
- **Modern** targets current .NET: `Microsoft.Data.SqlClient`, file-scoped namespaces, nullable
  reference types, no WCF attributes.

Both produce the same class and method surface and the same file set, so they compile against
the same framework assembly.

Files under a `Bases` folder are regenerated on every run. The partial classes beside them are
scaffolding for hand-written code, and are also emitted every run — take those on the first
generation only.

`docs/TEMPLATE-DEVIATIONS.md` records every deliberate difference from the original templates,
including twelve defects in them that are fixed here.

## Security notes

- Executing submitted DDL is the feature. The blast radius is the project's connection string,
  which only an administrator sets, and every script is recorded against its submitter before it
  runs. Point projects at development or staging databases.
- Project connection strings are encrypted with ASP.NET Core Data Protection, so a database
  backup alone does not disclose them. Only a redacted summary is ever rendered.
- The generated `GetByQuery`, and the `WHERE` fragment of `GetPaged`, concatenate a
  caller-supplied fragment into dynamic SQL inside the procedure. Sort column and direction are
  validated against the real table; a `WHERE` fragment cannot be. Never pass a value derived
  from end-user input. The generated XML docs and each archive's README say so too.
