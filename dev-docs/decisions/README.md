# Durable decisions

This file records only decisions that remain necessary to understand Axial. Implementation history belongs in git; active architecture and work remain in `dev-docs/PLAN.md` and `dev-docs/TASKS.md`.

## 2026-08-07: Axial is the workflow product

- The repository contains the Flow side of the former combined project. Constraint, Refinements, Parse, Result, Data, Schema, codecs, and contracts belong to [Reified](https://github.com/adz/Reified).
- `Axial.Flow` became the `Axial` package and namespace. Add-ons became `Axial.*`; pre-1.0 compatibility packages and namespace aliases were not retained.
- Core `Axial` and its operational packages have no Reified dependency.
- `Axial.Hosting.AspNetCore` and `Axial.Hosting.GenHttp` are explicit optional integrations over Reified HTTP contracts. The retained reference application is temporary integration evidence and should eventually move to a separate examples repository.

## 2026-08-07: Why these two names

Both names are load-bearing and the reasoning is not recoverable from the code.

- **Axial went to the workflow side, not the description side.** It never meant anything for constraints and schema — "along an axis" says nothing about declaring a rule once and deriving a parser, a codec, and a contract from it. It reads better against directed, controlled execution. Nothing had shipped under the name, so the move cost nothing.
- **Reified names the decision the description side rests on.** A `Constraint<'value>` is not a `'value -> bool`; it carries its description, and a violation carries the constraint atom and the offending value as data rather than as rendered prose. Rendering, localization, JSON Schema emission, and derived fixtures are only possible because the rule is an inspectable value. That is reification in the strict sense.
- **`FsFlow` was considered and rejected.** The published 0.6 had no users beyond CI, so there was no continuity to protect, and "Flow" reads as workflow engine — Airflow, Prefect, Camunda — for a library that is not one.
- **This repository kept its history because the workflow side is the trunk.** The initial commit is the effect system; the description packages appear months later. So Reified was extracted out and this repository stayed intact, rather than the reverse.

## 2026-08-07: Do not lead with "effect system"

A positioning rule, not a style preference. It governs the documentation plan in `dev-docs/current-ideas/documentation-plan.md`.

Most .NET developers and many F# developers have never used an effect system, and the phrase produces blank stares. Lead with symptoms — failures that are not visible in a signature, code that cannot be tested without a real database, retry logic copy-pasted at every call site — and let the reader meet the category afterwards. Polly is the precedent: an unfamiliar concept carried by an opaque name and a concrete pitch.

The asymmetry with Reified is deliberate. Reified leads with its concept, because "the rule and its message are the same object, so they cannot drift" lands the moment it is explained. This one does not, so it waits.

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

## Application and hosting boundary

- `App` owns the portable lifetime of one root workflow. `App.run` is the finite entry point; `App.start` returns an owned handle whose stop operation is idempotent and whose completion follows cleanup.
- Host packages translate native lifecycle events at the outer edge. Core workflows do not inherit host-specific base types.
- Node and browser hosting packages are runtime-specific Fable bindings. Browser visibility and unload events are not presented as dependable application shutdown.

## Portability and compatibility

- Core designs remain AOT-, trimming-, and Fable-friendly. Runtime reflection is not the foundation of workflow execution or service access.
- Before 1.0, obsolete APIs are removed directly instead of accumulating aliases. After 1.0, normal semantic-versioning and deprecation rules apply.
- Public packages currently share the version in `Directory.Build.props`; independent versioning can be reconsidered after package boundaries stabilize.
