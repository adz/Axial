# Axial

> [!WARNING]
> Axial 0.7.0 is the first planned release under the Axial name as split packages, renamed from monolithic FsFlow. The new package line continues in `Axial.Flow`, `Axial.Result`, `Axial.Validation`, and the umbrella `Axial` package. The direction is designed to keep parts usable independently and reduce cognitive load.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/content/img/axial-readme-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/content/img/axial-readme-light.svg">
  <img alt="Axial" src="docs/content/img/axial-readme-light.svg" width="160">
</picture>

Axial provides **structured composition over normal F#/.NET code**. It is an application architecture model for F# on .NET.

Write small predicate checks with `Axial.Result.Check`, keep fail-fast logic in standard `Result`, accumulate sibling
validation with `Axial.Validation.Validation` and `validate {}`, then lift the same logic into `Axial.Flow.Flow`
when the boundary needs environment access, async work, task interop, or runtime policy.

[![ci](https://github.com/adz/Axial/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/Axial/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Axial.svg)](https://www.nuget.org/packages/Axial)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Application Architecture

Axial is built around one progression:

```text
Axial.Result.Check -> Result -> Axial.Validation.Validation -> Axial.Flow.Flow
```

The same vocabulary carries from pure checks into effectful workflows.

- **Composition**: `flow {}` binds `Result`, `Option`, `Async`, `Task`, and `ColdTask` directly.
- **Explicit dependencies**: Keep dependencies visible in `'env`, with `IServiceProvider` integration at the host boundary.
- **Execution outcomes**: Keep typed domain failures, cancellations, and unhandled defects separate.
- **State**: Software Transactional Memory (STM) coordinates updates across multiple variables.

## Example

Start with a reusable check and a fail-fast result:

```fsharp
open Axial.Flow
open Axial.Result

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

Axial stays close to standard F# and .NET:

- `flow { ... }` binds to `Result` and `Option`
- `flow { ... }` also binds to `Async`, `Async<Option<_>>`, `Async<ValueOption<_>>`, and `Async<Result<_,_>>`
- On .Net, `flow { ... }` also binds to `Task`, `ValueTask`, `Task<_>`, `ValueTask<_>`, and `ColdTask`
- `result {}` keeps fail-fast pure code readable
- `validate {}` keeps sibling validation accumulation explicit

Because tasks are hot, Axial includes `ColdTask`: a small wrapper around `CancellationToken -> Task`.
`flow` handles token passing for you and keeps reruns explicit.

## A full example

The full runnable example is in [`examples/Axial.ReadmeExample/Program.fs`](./examples/Axial.ReadmeExample/Program.fs).

```bash
dotnet run --project examples/Axial.ReadmeExample/Axial.ReadmeExample.fsproj
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
