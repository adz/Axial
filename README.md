# FsFlow

> [!WARNING]
> API Still stabilising - wait for 1.0 to avoid breaking changes

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
- **Architectural Honesty**: Distinguishes between your **Explicit Environment** (business dependencies like repositories) and the **Ambient Runtime** (operational services like clock and logging).
- **ZIO-Style Execution**: Preserves the critical distinction between typed domain failures, cancellations, and unhandled defects.
- **Composable State**: Built-in Software Transactional Memory (STM) for atomic coordination across multiple variables without manual lock management.

## Install

- `FsFlow` for `Flow` and the supporting validation/runtime helpers

## Example

Start with a reusable check and a fail-fast result:

```fsharp
open System.Threading.Tasks
open FsFlow

type RegistrationError =
    | EmailMissing
    | SaveFailed of string

let validateEmail (email: string) : Result<string, RegistrationError> =
    email
    |> Check.notBlank
    |> Check.orError EmailMissing
```

Use the same validation logic directly inside a task-oriented workflow:

```fsharp
open System.Threading.Tasks
open FsFlow

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

## Semantic Boundary

FsFlow is for short-circuiting, ordered workflows:

- `Check`, `Result`, `Validation`, and `Flow` stop on the first typed failure.
- `Validation` and `validate {}` accumulate sibling failures in a structured diagnostics graph.
- The flow families are for orchestration, dependency access, async or task execution, and runtime concerns.

If you need accumulated validation, use `Validation` and `validate {}` explicitly instead of
trying to hide it inside a flow builder.

## What You Get

FsFlow stays close to standard F# and .NET:

- `flow { ... }` binds to `Result` and `Option`
- `flow { ... }` also binds to `Async`, `Async<Option<_>>`, `Async<ValueOption<_>>`, and `Async<Result<_,_>>`
- `flow { ... }` also binds to `Task`, `ValueTask`, `Task<_>`, `ValueTask<_>`, and `ColdTask`
- `result {}` keeps fail-fast pure code readable
- `validate {}` keeps sibling validation accumulation explicit

Because tasks are hot, FsFlow includes `ColdTask`: a small wrapper around `CancellationToken -> Task`.
`flow` handles token passing for you and keeps reruns explicit.

This is the file-oriented example shape. The full runnable example is in
[`examples/FsFlow.ReadmeExample/Program.fs`](./examples/FsFlow.ReadmeExample/Program.fs).

```bash
dotnet run --project examples/FsFlow.ReadmeExample/FsFlow.ReadmeExample.fsproj --nologo
```

Supporting types in the full example are just:

- `ReadmeEnv = { Root: string }`
- `FileReadError = NotFound`

```fsharp
let readTextFile (path: string) : Flow<ReadmeEnv, FileReadError, string> =
    flow {
        // In production, map access and path exceptions separately at the boundary.
        do! okIf (File.Exists path) |> orElse (NotFound path) // from Validate

        return! ColdTask(fun ct -> File.ReadAllTextAsync(path, ct)) // ColdTask<string>
    }

let program : Flow<ReadmeEnv, FileReadError, string * string> =
    flow {
        let! root = Flow.read _.Root                       // ReadmeEnv.Root -> string
        let settingsFile = Path.Combine(root, "settings.json")
        let featureFlagsFile = Path.Combine(root, "feature-flags.json")

        let! settings = readTextFile settingsFile              // Flow<ReadmeEnv, FileReadError, string>
        let! featureFlags = readTextFile featureFlagsFile      // Flow<ReadmeEnv, FileReadError, string>

        return settings, featureFlags                          // Flow<ReadmeEnv, FileReadError, string * string>
    }
```

It reads `Root` from `'env`, performs two file reads in one `flow {}`, and keeps failure typed at the boundary.

## Getting Started

- [Docs site](https://adz.github.io/FsFlow) for guides and API reference
- [Validation & Results](https://adz.github.io/FsFlow/validation-results/) for the validation-first story
- [Getting Started](https://adz.github.io/FsFlow/start/getting-started/) for the core workflow guide
- [Straightforward Examples](https://adz.github.io/FsFlow/start/basic-examples/) for small runnable snippets
- [Execution and Outcomes](https://adz.github.io/FsFlow/start/execution-and-outcomes/) for running and combining flows
- [`examples/`](examples/) for runnable repo examples
