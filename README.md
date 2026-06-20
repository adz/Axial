# FsFlow

> [!WARNING]
> FsFlow 0.7.0 is the last planned release under the FsFlow name before the split. Future development continues in `Axial.Result`, `Axial.Validation`, and `Axial`.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/content/img/fsflow-readme-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/content/img/fsflow-readme-light.svg">
  <img alt="FsFlow" src="docs/content/img/fsflow-readme-light.svg" width="160">
</picture>

FsFlow provides **structured composition over normal F#/.NET code**. It is a coherent application architecture model for F# on .NET, centered on a unified effect system.

Write small predicate checks with `Check`, keep fail-fast logic in standard `Result`, accumulate sibling
validation with `Validation` and `validate {}`, then lift the same logic into `Flow`
when the boundary needs environment access, async work, task interop, or runtime policy.

[![ci](https://github.com/adz/FsFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/FsFlow/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/FsFlow.svg)](https://www.nuget.org/packages/FsFlow)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Coherent Application Architecture

FsFlow is built around one progression:

```text
Check -> Result -> Validation -> Flow
```

The same vocabulary stays the same while the execution context grows.

- **Structured Composition**: A single `flow {}` builder that binds `Result`, `Option`, `Async`, `Task`, and `ColdTask` directly, eliminating the "adapter tax" of switching helper families at every boundary.
- **Architectural Honesty**: Keep dependencies explicit in 'env - but allow integration with IServiceProvider at the boundary.
- **ZIO-Style Execution**: Preserves the critical distinction between typed domain failures, cancellations, and unhandled defects.
- **Composable State**: Built-in Software Transactional Memory (STM) for atomic coordination across multiple variables without manual lock management.

## Example

Lets start by showing a reusable check and a fail-fast result:

```fsharp
type RegistrationError =
    | EmailMissing
    | SaveFailed of string

let validateEmail (email: string) : Result<string, RegistrationError> =
    email
    |> Check.whenNotBlank
    |> Check.withError EmailMissing
```

Use the same validation logic directly inside a task-oriented workflow:

```fsharp
type User =
    { Email: string }

type RegistrationEnv =
    { LoadUser: int -> Task<Result<User, RegistrationError>>
      SaveUser: User -> Task<Result<unit, RegistrationError>> }

let registerUser userId : Flow<RegistrationEnv, RegistrationError, unit> =
    flow {
        let! loadUser = Flow.read _.LoadUser
        let! saveUser = Flow.read _.SaveUser

        let! user = loadUser userId
        do! validateEmail user.Email

        return! saveUser user
    }
```

`validateEmail` is just a plain `Result<string, RegistrationError>`.
`flow` lifts it directly with `do!`.
The same builder also binds `Async`, `Task`, `ValueTask`, and `ColdTask` directly.

## What You Get

FsFlow stays close to standard F# and .NET:

- `flow { ... }` binds to `Result` and `Option`
- `flow { ... }` also binds to `Async`, `Async<Option<_>>`, `Async<ValueOption<_>>`, and `Async<Result<_,_>>`
- On .Net, `flow { ... }` also binds to `Task`, `ValueTask`, `Task<_>`, `ValueTask<_>`, and `ColdTask`
- `result {}` keeps fail-fast pure code readable
- `validate {}` keeps sibling validation accumulation explicit

Because tasks are hot, FsFlow includes `ColdTask`: a small wrapper around `CancellationToken -> Task`.
`flow` handles token passing for you and keeps reruns explicit.

## A full example

The full runnable example is in [`examples/FsFlow.ReadmeExample/Program.fs`](./examples/FsFlow.ReadmeExample/Program.fs).

```bash
dotnet run --project examples/FsFlow.ReadmeExample/FsFlow.ReadmeExample.fsproj
```

```fsharp
// ReadmeEnv = { Root: string }
// FileReadError = NotFound

let readTextFile (path: string) : Flow<ReadmeEnv, FileReadError, string> =
    flow {
        // In production, map access and path exceptions separately at the boundary.
        do! File.Exists path |> Check.isTrue |> BindError.withError (NotFound path)

        // Wrap in ColdTask for later exeuction
        return! ColdTask(fun ct -> File.ReadAllTextAsync(path, ct))
    }

let program : Flow<ReadmeEnv, FileReadError, string * string> =
    flow {
        let! root = Flow.read _.Root                       // ReadmeEnv.Root -> string
        let settingsFile = Path.Combine(root, "settings.json")
        let featureFlagsFile = Path.Combine(root, "feature-flags.json")

        let! settings = readTextFile settingsFile          // Flow<ReadmeEnv, FileReadError, string>
        let! featureFlags = readTextFile featureFlagsFile  // Flow<ReadmeEnv, FileReadError, string>

        return settings, featureFlags                      // Flow<ReadmeEnv, FileReadError, string * string>
    }
```

It reads `Root` from `'env`, performs two file reads in one `flow {}`, and keeps failure typed at the boundary.

## Getting Started

- Preview the docs locally with `bash scripts/preview-docs.sh`
- Build and validate the docs with `bash scripts/validate-docs.sh`
- [`examples/`](examples/) for runnable repo examples
