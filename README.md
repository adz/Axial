# Axial

> [!WARNING]
> Axial 0.7.0 is the first planned release under the Axial name as split packages, renamed from monolithic FsFlow. The new package line continues in `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Schema`, `Axial.Codec`, `Axial.Validation`, `Axial.Validation.Schema`, and the umbrella `Axial` package. The direction is designed to keep parts usable independently and reduce cognitive load.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/content/img/axial-readme-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/content/img/axial-readme-light.svg">
  <img alt="Axial" src="docs/content/img/axial-readme-light.svg" width="160">
</picture>

Checking a model field by field falls apart once there's more than one field: stopping at the first failure ("fail-fast")
hides every other broken field from the caller, hand-rolled accumulation loses which field each error belongs to, and
either way the record gets built before the checks finish. Add async work on top and a second problem shows up —
dependencies and cancellation get threaded through every call by hand. Axial is three areas that share one vocabulary
and can be used independently:

**Error handling.** For simple code, plain F# `Result<'value, 'error>` with your own error union is the whole story,
no Axial types in your signatures. `Validation` accumulates several of those errors instead of stopping at the first.
`Check` is a library of reusable, ready-made checks (blank strings, ranges, and the like) with its own built-in error
type, for when you don't want to define a check yourself.

**Schema.** For a whole domain model, declare a `Schema` once: parsing raw input (HTTP form-like, CLI, JSON-like,
configuration), validation, redisplay with path-aware field errors, contextual rules, and metadata interpreters (JSON
Schema, docs, UI) all fall out of that one declaration. An invalid model is never constructed. `Refined` types carry
this same idea down to a single field: the validation stays part of the type (e.g. `NonBlankString`) instead of being
thrown away after a boolean check, so a value that type-checks is already known-valid everywhere it's passed around.

**Flow.** An environment-aware Reader-Async-Result workflow model in the ZIO tradition. "Cold" means a `Flow` value is
just a description of work — building one runs nothing; it only executes when you actually run it, unlike a `Task`,
which starts as soon as it's created. On top of that: explicit dependencies in `'env`, direct `Task`/`ValueTask`/`Async`
interop, cancellation, layers, scoped resources, and fibers for structured concurrency. `Policy` and `Flow.verify` are
where Schema and Flow meet — a parsed model enters a workflow with the environment injected. Flow is optional; error
handling and Schema work without it.

Everything is zero-reflection, AOT- and trimming-safe, and Fable-compatible.

[![ci](https://github.com/adz/Axial/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/Axial/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Axial.svg)](https://www.nuget.org/packages/Axial)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Application Architecture

The same vocabulary carries from pure checks into effectful workflows.

- **Composition**: `flow {}` binds `Result`, `Option`, `Async`, `Task`, and `ColdTask` directly. `ColdTask` is just a
  delayed `Task`: a `CancellationToken -> Task<'value>` that hasn't started running yet, so `flow` can pass its own
  cancellation token in and rerun it if needed.
- **Explicit dependencies**: Keep dependencies visible in `'env`, with `IServiceProvider` integration at the host boundary.
- **Execution outcomes**: Keep typed domain failures, cancellations, and unhandled defects separate.

## Example

Start with a reusable check and a fail-fast result:

```fsharp
open Axial.Flow
open Axial.ErrorHandling

type RegistrationError =
    | EmailMissing
    | SaveFailed of string

let validateEmail (email: string) : Result<string, RegistrationError> =
    email
    |> Result.notBlank
    |> Result.mapError (fun _ -> EmailMissing)
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
- `Schema` + `Input.parse` turn raw boundary input into trusted models or path-aware diagnostics — invalid models are never constructed
- `Json.compile` (`Axial.Codec`) compiles the same schema into a reflection-free JSON codec for trusted payloads, and `JsonSchema.generate` publishes the matching contract

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
        do! File.Exists path |> Result.checkOr () |> Bind.error (NotFound path)

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
