# Durable decisions

This file records only decisions that remain necessary to understand Axial. Implementation history belongs in git; active architecture and work remain in `dev-docs/PLAN.md` and `dev-docs/TASKS.md`.

## 2026-08-07: Axial is the workflow product

- This project is workflows and their operational surface. Constraints, refinements, parsing, result composition, structured data, schema, codecs, and contracts were removed to a separate project and are out of scope here.
- `Axial.Flow` became the `Axial` package and namespace. Add-ons became `Axial.*`; pre-1.0 compatibility packages and namespace aliases were not retained.
- Core `Axial` and its operational packages take no dependency outside this project.
- `Axial.Hosting.AspNetCore` and `Axial.Hosting.GenHttp` are the sole exception: explicit optional integrations that serve externally declared HTTP contracts, consumed as released packages. The retained reference application is temporary integration evidence and should eventually move to a separate examples repository.

## 2026-08-07: Why the name is Axial

The reasoning is not recoverable from the code.

- **The name belongs on the workflow side.** "Along an axis" reads as directed, controlled execution. It said nothing useful about the value and model work that used to share this repository, which is part of why that work is no longer here.
- **`FsFlow` was considered and rejected.** The published 0.6 had no users beyond CI, so there was no continuity to protect, and "Flow" reads as workflow engine — Airflow, Prefect, Camunda — for a library that is not one.
- **This repository kept its history because the workflow side is the trunk.** The initial commit is the effect system; everything else arrived months later. So the rest was extracted out and this repository stayed intact, rather than the reverse.

## 2026-08-07: Do not lead with "effect system"

A positioning rule, not a style preference. It governs how the documentation is structured.

Most .NET developers and many F# developers have never used an effect system, and the phrase produces blank stares. Lead with symptoms — failures that are not visible in a signature, code that cannot be tested without a real database, retry logic copy-pasted at every call site — and let the reader meet the category afterwards. Polly is the precedent: an unfamiliar concept carried by an opaque name and a concrete pitch.

## Public workflow model

- `Flow<'env,'error,'value>` is the one public workflow model. Carrier-specific workflow concepts such as `Effect`, `AsyncFlow`, and `TaskFlow` are not public alternatives.
- Short aliases describe common channel combinations without introducing different execution models.
- Workflows are cold descriptions. Execution happens only through explicit runners and application roots.
- Expected failures use `'error`; defects, interruption, and cancellation remain distinct in `Cause` and `Exit`.

## Explicit dependencies and runtime boundary

- Application and operational dependencies live in `'env`. Plain records plus `Flow.read` are the default local application style; nominal `IHas<'service>` contracts are available when their static contract is worth the ceremony.
- The ambient runtime is closed and contains executor mechanics only: cancellation, scopes, scheduling, interruption, fiber bookkeeping, annotations, and tracing mechanics.
- Clock, randomness, GUID generation, environment variables, filesystem, console, HTTP, process execution, and other operational effects are explicit mockable services.
- A service implementation may perform only the effect named by its core service type unless every additional effect is visible as another explicit dependency.

## Resources and concurrency

- Structured child fibers are owned by their scope. `forkDetached` is the explicit declaration of intentional fire-and-forget work.
- Resource lifetime uses scopes and finalizers. Acquisition APIs must make ownership and release behavior visible rather than relying on ambient disposal.
- `FlowStream` reuses Flow execution, scope, cancellation, and failure semantics; it must not become a second runtime.
- Mutable state lives under `Axial.State`: `Ref` handles one independently atomic value, while `TRef` and `STM`
  compose transactions across values. Coordination primitives such as `Deferred` and `FlowSemaphore` remain in
  `Axial`; STM journal, context, and commit machinery are internal implementation details.

## Application and hosting boundary

- `App` owns the portable lifetime of one root workflow. `App.run` is the finite entry point; `App.start` returns an owned handle whose stop operation is idempotent and whose completion follows cleanup.
- Host packages translate native lifecycle events at the outer edge. Core workflows do not inherit host-specific base types.
- Node and browser hosting packages are runtime-specific Fable bindings. Browser visibility and unload events are not presented as dependable application shutdown.

## Portability and compatibility

- Core designs remain AOT-, trimming-, and Fable-friendly. Runtime reflection is not the foundation of workflow execution or service access.
- Before 1.0, obsolete APIs are removed directly instead of accumulating aliases. After 1.0, normal semantic-versioning and deprecation rules apply.
- Public packages currently share the version in `Directory.Build.props`; independent versioning can be reconsidered after package boundaries stabilize.

## 2026-08-10: Environment contracts are one non-generic interface per service

`IHas<'service>` is being replaced by one ordinary interface per service — `IHasClock` exposing
`Clock`, `IHasFileSystem` exposing `FileSystem`, and so on.

- **Why.** F# rejects a type parameter constrained by two instantiations of the same generic
  interface (`FS0193`). With `IHas<'service>`, a workflow using two services could not be expressed
  without naming a concrete environment *inside* the `flow { }` block, because the annotation on the
  binding applies to the block's result after the body has already been checked. Distinct interfaces
  carry no such restriction, so the constraints merge on their own. Adding a generic type anywhere in
  a contract's inheritance chain reintroduces the failure, so contracts must not inherit one.
- **Naming rule for producers.** A contract named `IHasFoo` exposes exactly one member named `Foo`.
  This is what makes `Flow.read _.Foo` predictable and keeps composition roots readable.
- **`member this.Foo = this.Foo` is not recursive.** F# interface implementations are always
  explicit, so the interface member is not in scope on the concrete type; `this.Foo` on the
  right-hand side resolves to the record field. A type with no such field fails to compile rather
  than recursing.
- **Each package ships one accessor**, `Foo.service`, bound at module level. `Flow.read _.Foo` cannot
  resolve inside a `flow { }` block, where the lambda's parameter type is not yet known; binding it
  at module level puts the annotation next to the expression that needs it, and every caller then
  binds it with no annotation.

## 2026-08-10: Module-level values in a test project's last file are never initialised

F# compiles the **last compiled file's** top-level initialisation into `main@`, the assembly's entry
point. Test assemblies are built with an entry point, and the test host invokes test methods
reflectively without ever calling it, so those values stay `null`. The access site is an unguarded
`ldsfld`, and the file's startup class has no static constructor, so nothing forces initialisation.

Verified: `Axial.PlatformService.Tests.dll` reports its entry point as
`<StartupCode$Axial-PlatformService-Tests>.$ServiceRuntimePatternTests::main@` — the last file.
`Axial.dll` has no entry point, so every file including `Builders.fs` gets a static constructor and
`flow` and `layer` initialise normally. Moving the file out of last position also fixes it, because
`main@` moves with the position.

`<OutputType>Library</OutputType>` does not prevent this; the test SDK arranges the entry point
regardless, and `GenerateProgramFile=false` only suppresses the generated `Program.fs`.

Bind such values locally inside the test, or make them functions. Do not fix it by reordering files
— the next file added moves the problem.
