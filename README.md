# Voot CodeGen

Voot CodeGen is a .NET 10 MVC web application that generates a complete C# data layer from a
SQL Server database. Point a project at a database, and it produces entities, typed lists, data
access classes, business managers and the stored procedures they call — one set per table —
packaged as a downloadable zip.

It is also a place to manage the database itself: teams apply schema changes through it, see
exactly what each change did to the structure, keep an audit trail of every script, and track
which changes have reached development and production.

## What it does

**Projects and access**

- An administrator creates a **project**: a target SQL Server database (its connection string is
  encrypted at rest) plus the options that shape the generated code — namespaces, folder layout,
  table prefix, which stored procedures to emit, and the output style.
- Administrators reach every project. Other users reach only the projects they are assigned to.
- Administrators manage accounts: create users, grant or revoke the administrator role, disable
  accounts and reset passwords. The last active administrator cannot be removed.

**Schema changes and code generation**

- A user submits a **SQL change** against a project. It runs against that project's database,
  in a single transaction by default, and the script is kept permanently as the audit record.
- When the change succeeds, the application re-reads the schema and **regenerates the code**. A
  table that cannot be generated is reported and skipped rather than failing the whole run.
- Each run generates either **all tables** or **only the tables that changed** since the last
  successful generation. The choice is made per run, pre-set from a project default.
- The result is **zipped, stored and downloadable**, and stays re-downloadable from the
  project's history.
- A project can also be **regenerated** from its current schema without applying any SQL.
- Every run is recorded with its status, a timestamped log, any warnings or errors, and the list
  of files it produced. Failures show the SQL Server error number, line and batch.

**Database structure**

- The **schema browser** shows the current structure of a project's database: columns, declared
  types, nullability, defaults, primary keys, foreign keys and indexes. Tables can be picked one
  at a time, filtered, or all selected at once, and foreign keys link to the tables they
  reference.
- Each table shows the class name the generator derives for it, and flags tables it treats
  specially — junction tables, and tables skipped for having no primary key.
- SQL can be run **directly from the schema page**. The page then shows a structural diff of
  what changed: tables added or dropped, and columns added, dropped or altered, with each
  column's declaration before and after. Running SQL here does not regenerate code; that stays
  an explicit step, linked from the result.
- Each table offers a **sample `INSERT`** with placeholder data — for one table or the whole
  selection — in a dialog with a copy button.

**Tracking changes into dev and production**

- Every applied change has a **Dev** and a **Production** checkbox recording whether it has
  reached that environment, along with who marked it and when.
- The page shows how many changes are still outstanding for each environment, and combines them
  into **one replayable script**, oldest first, in a dialog with a copy button.
- Everything outstanding can be **marked in one click**.
- Every move — into an environment or back out — is logged.
- Changes and deployment moves are shown together as a **timeline**, so a change and its journey
  to production read as one story.

## How it works

### The generation pipeline

Submitting a change records it and queues a run, then returns immediately; a background worker
does the work, so a large database cannot time out a request. The run page polls for progress
and updates itself when the run finishes.

For each run the worker:

1. **Applies the SQL.** The script is split on `GO` separators — correctly ignoring any `GO`
   inside strings, comments or bracketed identifiers — and each batch runs in turn, inside one
   transaction unless the project turns that off. Any failure rolls everything back and ends the
   run with the server's error.
2. **Reads the schema** from SQL Server's catalog views: tables, columns and their types,
   primary and foreign keys, and indexes.
3. **Generates the code.** A set of emitters runs over every table. Naming rules (class names,
   procedure names, parameter helpers) live in one place, so the procedure names the data access
   layer calls always match the procedures the scripts create.
4. **Packages the archive**, writes it to storage, and records a SHA-256 hash and the list of
   files it contains.
5. **Saves a snapshot of the schema** it generated from, as the baseline for the next
   changed-tables run.

### Generating only changed tables

A changed-tables run compares the schema it just read with the snapshot saved by the last
successful run. It generates a table if the table is new, if its columns or primary key changed
(type, size, nullability, identity, default), or if its foreign keys changed. Renaming a
constraint does not count. A table that failed to generate last time is included again.
Because the comparison is against the database itself, changes made from the schema page count
too, not only submitted changes.

The archive then holds only those tables. Its `README.txt` lists them, along with any tables
dropped since the baseline, whose files should be deleted. The rest of the schema is still read,
so relationships to tables outside the archive come out the same as in a full run.

The run falls back to every table, and says why on the run page and in its log, when:

- there is no earlier successful run to compare against;
- the project's generation settings changed since then, since that can change every file; or
- no table changed.

If the application restarts mid-run, queued runs are picked up again. Runs that were already
executing are marked failed rather than replayed, because their SQL may already have been
applied.

### The schema browser and diff

The browser reads the database live on every view, so it always reflects the database as it is
now. When SQL is run from that page, the schema is read before and after, and the two snapshots
are compared — so the diff reflects what the server actually did, including anything the script
triggered indirectly, rather than a guess from parsing the script.

### Architecture

The solution follows clean architecture, with dependencies pointing inward:

```
src/
  Voot.CodeGen.Domain           entities and the schema model; no dependencies
  Voot.CodeGen.Generation       the code emitters; depends only on Domain
  Voot.CodeGen.Application      use cases and the ports they depend on
  Voot.CodeGen.Infrastructure   SQL Server, Dapper, Identity stores, storage, background worker
  Voot.CodeGen.Web              MVC controllers and views
tests/
  Voot.CodeGen.Generation.Tests   the generator, over a synthetic schema
  Voot.CodeGen.Application.Tests  SQL handling, schema diffing, sample inserts, trackers
```

`Web → Infrastructure → Application → Domain`, with `Generation` depending only on `Domain`.

### Data access and identity, without Entity Framework

There is no Entity Framework anywhere in the solution. All data access uses Dapper over
`Microsoft.Data.SqlClient`.

Sign-in uses ASP.NET Core Identity, whose stores are implemented directly over Dapper —
users, passwords, email, security stamps, lockout, roles, claims, phone numbers and two-factor
settings. Password hashing, lockout after repeated failures, and session invalidation when an
account is disabled all work as standard.

The application's own tables are created by SQL scripts embedded in the build. They run at
startup, are written to be safe to run repeatedly, and each is recorded in `__SchemaVersions`
once applied. The application also creates its own database on first start if it does not
exist.

## Generated output

For each table with a primary key, the archive contains:

| File | Contents |
| --- | --- |
| `Entities/Bases/{Entity}Base.cs` | Column constants, backing fields, change-tracked properties, cloning |
| `Entities/Bases/{Entity}.cs` | A navigation property for each foreign key; equality on the key |
| `Entities/List/{Entity}List.cs` | A typed collection |
| `DataAccess/Bases/{Entity}DataAccess.cs` | Insert, update, delete, get by key, get all, lookups by foreign key, paging, and reader mapping |
| `BusinessLogic/Bases/{Entity}Manager.cs` | Persistence driven by row state, retrieval, and loading of related entities |
| `StoreProcedures/{table}_Procedures.sql` | Every procedure the data access layer calls |
| `Entities/{Entity}.cs`, `DataAccess/{Entity}DataAccess.cs`, `BusinessLogic/{Entity}Manager.cs` | Partial classes for hand-written code |

Junction tables get data access built on a relation base class and no business manager. Tables
without a primary key are skipped, with a warning in the run log.

Files under a `Bases` folder are regenerated on every run and should not be edited. The partial
classes beside them are for hand-written code; they are emitted on every run too, so take them
on the first generation only.

Each project chooses an output style:

- **Legacy** — `System.Data.SqlClient`, WCF `[DataContract]` attributes, block-scoped
  namespaces.
- **Modern** — `Microsoft.Data.SqlClient`, file-scoped namespaces, nullable reference types, no
  WCF attributes.

Both styles produce the same classes, methods and file set.

The archive contains generated files only. They compile against your own framework assembly,
which provides the base classes (`BaseBusinessEntity`, `BaseCollection<T>`, `BaseDataAccess`,
`BaseRelationData`, `BaseManager`), the parameter helpers, `PagedRequest`, the context type and
the exception types. Each archive's `README.txt` lists the exact surface required.

## Running it

Requires .NET 10 and a SQL Server instance.

```bash
export ConnectionStrings__Application="Server=localhost;Database=VootCodeGen;User ID=sa;Password=...;TrustServerCertificate=True"
export SeedAdministrator__UserName="admin"
export SeedAdministrator__Password="<a strong password>"

dotnet run --project src/Voot.CodeGen.Web
```

On first start the application creates its database, applies its schema, and creates the seed
administrator. The seed account is only created when no administrator exists yet, and an existing
account's password is never reset. Supply the password through an environment variable or user
secrets, never a committed settings file.

### Configuration

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Application` | The database the application stores its own data in |
| `ArtifactStorage:RootPath` | Where generated archives are written (default `App_Data/artifacts`) |
| `ArtifactStorage:RetentionDays` | Days to keep archive files; `0` keeps them forever |
| `SeedAdministrator:UserName` / `:Password` | The first administrator, created only if none exists |

### Tests

```bash
dotnet test
```

## Security notes

- **Running submitted SQL is the purpose of the application.** Its reach is limited to the
  project's own database, whose connection string only an administrator can set, and every
  script is recorded against the user who submitted it before it runs. Point projects at
  development or staging databases.
- **Connection strings are encrypted** with ASP.NET Core Data Protection, so a backup of the
  application database alone does not reveal them. Pages only ever show a redacted summary.
- **Access is checked on every project action.** A user who is not assigned to a project cannot
  view it, run SQL against it, download from it, or change its trackers.
- **Generated `GetByQuery` and `GetPaged` accept a raw `WHERE` fragment**, which the stored
  procedure concatenates into dynamic SQL. Sort column and direction are validated against the
  real table, but a `WHERE` fragment cannot be. Never pass a value built from end-user input.
  The generated code comments and each archive's `README.txt` say the same.
