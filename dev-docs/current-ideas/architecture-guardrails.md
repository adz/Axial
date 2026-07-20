# Architecture Guardrails

Axial should help applications compress intent, domain knowledge, and invariants into human-reviewable interfaces that
also constrain generated code. Schema establishes trust at data boundaries; private domain types preserve it; named
domain transitions evolve it; Flow makes operational dependencies and failures explicit.

This note records the remaining recommendations from the 2026-07 architecture review. Contract generation freshness is
not listed as a separate CI command because `Axial.Schema.Contracts.Build` now makes generation part of the ordinary
build.

## Terms

- **Domain**: the business concepts and rules used by application logic, independent of transport and storage formats.
- **Aggregate**: related values that are created and updated as one thing.
- **Invariant**: a rule that must hold for every valid value of a domain type.
- **Schema**: Axial's typed description of input shape, field constraints, and construction.
- **Wire model**: a record shaped for serialized or stored data rather than business operations.
- **Transition**: a named operation that changes one valid domain state into another or returns a typed refusal.
- **Guardrail**: a compiler, build, interface, or test mechanism that prevents or reports a specific class of mistake.

## Guarantee Ladder

Teach four distinct strengths of guarantee:

1. A raw, wire, or draft value is freely constructible and carries no trust guarantee.
2. A successful `Schema.parse` or `Schema.check` result passed that operation's admission gates.
3. A private refined field type durably preserves a field-local invariant.
4. A private aggregate representation, exposed only through complete constructors and invariant-preserving
   transitions, durably preserves relational and state invariants.

Top-level documentation should say that Schema never *returns* a value which failed its declared gates. Avoid the
broader claim that an invalid value of a publicly constructible type can never be created.

## Domain Types And Project Structure

- Parse at every external boundary, then pass domain types inward rather than `Data`, DTOs, drafts, or wire records.
- Prefer private refined fields as the first resort because they preserve ordinary record syntax and make illegal field
  values unrepresentable.
- Use private aggregate representations for cross-field invariants that must hold for every value.
- Put domain representations in a small module or assembly so changing constructor visibility is a conspicuous review
  event.
- Consider a one-way project graph such as Contracts/Boundary → Domain → Application, with Infrastructure satisfying
  explicit application seams and Host assembling the graph.
- Serialize through named domain-to-wire functions. Reparse values read from persistence, plugins, queues, configuration,
  or other systems whose construction history is not controlled by the current process.

## Legal Transitions

Construction invariants cover which values may exist. Large applications must also encode which state transitions are
legal.

- Expose named domain operations over trusted types rather than arbitrary record-copy edits followed by `Schema.check`.
- Return `Result` when a transition may violate an invariant or be refused by the current state.
- Expose total transitions only when their implementation proves they preserve the invariant.
- Keep transition errors as finite domain discriminated unions rather than strings.
- Use drafts for multi-field editing, then re-admit through the aggregate's authoritative constructor.

## Architecture Fitness Checks

Provide reusable tests, templates, or tooling for adopter repositories:

- Check the allowed project-reference graph.
- Ban ambient clock, GUID, random, environment, filesystem, console, and network access in designated domain and
  application projects.
- Restrict `IServiceProvider` resolution to host-edge projects.
- Reject `Data`, generated wire records, and draft types in domain or application public interfaces.
- Check that domain aggregates do not expose public unchecked construction or mutation.
- Check that expected domain failures use typed error channels rather than exceptions or strings.
- Make generated contract/schema output an incremental, deterministic dependency of normal MSBuild compilation.

These rules should come from a small checked-in architecture manifest where practical, so documentation and executable
checks share one declaration.

## Analyzer Candidates

Only promote high-signal rules into analyzers. Candidates include:

- A public record schema uses `constructResult` for a claimed durable aggregate invariant.
- A `Schema.check` result is discarded.
- A Flow-producing application function calls an ambient operational effect.
- `Service.resolve` appears outside a designated host-edge assembly.
- A boundary representation crosses into a domain or application interface.
- A generated wire type is used as a business-domain type.

Diagnostics should explain the safe alternative and allow an explicit local suppression for deliberate exceptions.

## Compile-Negative Proofs

Add compile-negative tests or templates for important guarantees:

- Private refined and aggregate constructors cannot be called from application code.
- Incomplete object shapes and constructors whose parameters do not match field order do not compile.
- Forbidden package references fail the architecture test.
- A workflow cannot access a dependency missing from its environment interface.
- A new wire version makes contract construction fail until its typed migration is supplied.

Compile-negative examples are especially useful guardrails for generated code because an invalid shape fails
immediately rather than relying on a reviewer to remember a convention.

## Schema And Constructor Laws

Portable schema constraints may repeat facts enforced by an authoritative smart constructor. Keep failure safe and make
drift observable with reusable laws:

- Every successfully parsed value passes `Schema.check`.
- Every generated valid model passes `Schema.check`.
- Codec round trips preserve checked values.
- A refined smart constructor and its schema accept and reject the same generated raw values where the schema claims to
  describe the constructor's full rule.
- Every migration result passes the current schema.
- Every successful domain transition preserves intrinsic schema checks.

`Axial.Schema.Testing` is the natural place for FsCheck adapters implementing these laws without adding a property-test
dependency to public runtime packages.

## Schema Knowledge For Tools

Develop stable schema-as-data only around concrete consumers. Useful consumers include:

- Semantic contract and version diffs.
- LSP diagnostics and completion.
- Dynamic editors and forms.
- Compact, authoritative LLM context manifests.
- Documentation and compatibility reports.

The serialized form should preserve stable constraint codes and typed arguments without making boxed `obj` values the
wire contract. It should remain an interpreter over Schema rather than a competing authoring surface.

## Reference Application

Treat the reference application as the canonical architectural proof:

- Assert its project dependency direction.
- Assert wire records do not appear in domain/application public interfaces.
- Demonstrate private refined fields, private aggregates, fallible transitions, and explicit Flow environments together.
- Reparse persisted contracts on read.
- Run contextual policies only at their actual admission points.
- Include examples of both a fallible transition and a total invariant-preserving transition.

## Trusted Kernel Model

The practical goal is not to prove an application correct. It is to reduce the code that must be trusted and reviewed.

Use three kinds of guardrail around a small domain kernel:

```text
build and compiler restrictions
          ↓
small domain modules
  - authoritative constructors
  - legal transitions
  - explicit migrations
          ↓
schema-derived and property tests
```

The build should reject structural violations. Domain modules should contain the business decisions. Tests should
exercise semantic claims the F# compiler cannot prove.

## Guardrail Matrix

| Concern | Guarded fact | Strongest practical mechanism | Remaining manual check |
| --- | --- | --- | --- |
| Wire generation | Generated F# matches its declaration | MSBuild generation before `CoreCompile` | Whether the wire declaration models the real format |
| Domain construction | Callers cannot forge an aggregate | Private representation or opaque `.fsi` type | Whether the constructor enforces the complete invariant |
| Field invariants | Invalid field values cannot enter records | Private refined value type | Whether the refinement rule matches the domain term |
| Legal updates | Callers use named transitions | Opaque aggregate interface | Whether each transition should exist |
| Lifecycle states | An operation accepts only legal states | Separate or phantom state types | Whether the state split is useful rather than ceremonial |
| Project direction | Domain cannot import boundary or infrastructure code | Project references plus build validation | Whether the chosen project split matches ownership |
| Ambient effects | Selected projects do not call operational APIs | Project references plus compiled-call audit | Whether an exception is legitimate |
| Container lookup | Dynamic resolution stays at hosts | Package/project placement plus compiled-call audit | Host composition choices |
| Boundary leakage | DTO, draft, or `Data` types do not enter core interfaces | Project isolation plus public-surface audit | Classification of unusual boundary types |
| Typed failures | Expected refusal remains visible | Domain DUs and `Result`/Flow error channels | Which failures are expected rather than defects |
| Constraint drift | Schema metadata agrees with constructors | Schema-derived law tests | Custom constraints and missing test distributions |
| Transition preservation | Total transitions keep intrinsic invariants | Opaque module plus property law | Correctness of the property and generator |
| Migration validity | Migrated values satisfy the head schema | Typed chain plus revalidation and law tests | Preservation of business meaning |

## Opaque Domain Interfaces With `.fsi`

F# signature files can hide an implementation record behind an abstract public type:

```fsharp
// Booking.fsi
type Booking

module Booking =
    val create: BookingDraft -> Result<Booking, BookingError>
    val changeEnd: DateOnly -> Booking -> Result<Booking, BookingError>
    val shift: int -> Booking -> Booking
```

The matching `.fs` file can use ordinary records internally. Callers cannot construct, pattern-match, copy-update, or
read fields not listed in the signature.

This guards the aggregate seam rather than relying on every record member having the right accessibility modifier. The
signature also gives reviewers and coding agents a compact statement of legal operations.

Documentation should teach `.fsi` as the strongest option for important domain modules. Templates may generate an
initial signature, but the user should own it because choosing the public domain vocabulary is not mechanical work.

## Project Roles In MSBuild

An optional future build package could classify projects:

```xml
<PropertyGroup>
  <AxialArchitectureRole>Domain</AxialArchitectureRole>
</PropertyGroup>
```

Possible roles are `Contracts`, `Domain`, `Application`, `Infrastructure`, `Host`, and `Tests`.

A target can inspect direct `ProjectReference` items and reject forbidden edges before compilation. This guards which
types a project can name at all.

Example defaults:

```text
Contracts -> Schema
Domain -> ErrorHandling, optionally Schema
Application -> Domain, optionally Flow
Infrastructure -> Application
Host -> all application projects
Tests -> the projects under test
```

The role should live in the project file because MSBuild owns the graph. A separate manifest would duplicate project
identity and could drift.

Direct-edge validation is enough for most graphs. Every forbidden transitive path contains a direct edge where the
policy can fail.

## Compiled-Code Audits

Some policies cannot be expressed through references because a project legitimately references a broad framework
assembly. A post-compile task can inspect actual IL member references.

Candidate forbidden calls include:

- `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, and `DateTimeOffset.UtcNow`;
- `Guid.NewGuid` and random-number constructors;
- `Environment.*`, `System.Console.*`, and selected `System.IO.*` members;
- direct `HttpClient` construction in projects that require an injected HTTP seam;
- `IServiceProvider.GetService` and `Service.resolve` outside host projects.

This guards compiled behavior rather than source spelling. It catches generated code, aliases, open namespaces, and
calls hidden inside lambdas.

Policies must vary by project role. Infrastructure may use filesystem and HTTP APIs. Domain normally uses none. A local
suppression should name the exact member and reason rather than disable the rule for an assembly.

An editor analyzer may later provide faster feedback. The compiled audit should remain authoritative because it sees the
same output CI ships.

## Public-Surface Audits

Assembly metadata can check public parameter, return, property, field, base, and generic argument types.

For Domain and Application projects, reject public interfaces containing:

- `Data` or `RetainedParseResult`;
- types from configured Contracts assemblies;
- generated wire types carrying a stable marker;
- draft types carrying an optional draft marker;
- infrastructure implementation types.

This guards the interface consumers compile against. It does not reject local boundary variables inside host mapping
functions.

`[<DeriveSchema>]` already provides a source classification. Generated `.contract` types should receive an equivalent
stable compiled marker if public-surface tooling needs to recognize them without naming conventions.

Do not infer wire or draft meaning from suffixes such as `Dto`, `Wire`, `V1`, or `Draft`. Names are useful to readers but
are too weak for build enforcement.

## Invariant Aggregate Marker

A future opt-in marker could state that a type claims a durable aggregate invariant:

```fsharp
[<InvariantAggregate>]
type Booking = private { ... }
```

Once the author makes that claim, a build task can require:

- no public representation or constructor;
- no public setters or mutable fields;
- a public creation path returning `Result`, when configured;
- an opaque `.fsi` exposure for projects using strict mode.

Do not infer this claim merely because a schema uses `buildResult`. A public draft may legitimately use a fallible schema
constructor while remaining freely constructible.

## Restricting Dynamic Service Resolution

The strongest guard is package placement. If application code needs `Service.get` but can also see `Service.resolve`, a
build rule can reject calls outside host assemblies.

A future package split could place provider-backed resolution in a host integration package. That would remove the
operation from ordinary application discovery.

Use a package split only if repeated misuse justifies another package. Until then, project-role and compiled-call checks
provide a smaller change.

## State Types As Guardrails

Separate state types can remove illegal operations from the interface:

```fsharp
type DraftOrder
type SubmittedOrder
type CancelledOrder

val submit: DraftOrder -> Result<SubmittedOrder, SubmitError>
val cancel: SubmittedOrder -> Result<CancelledOrder, CancelError>
```

This guards which state can call an operation. It does not prove that the transition implementation carries every field
correctly.

Use state types when lifecycle state changes the available operations. Keep a DU field when all states share the same
operations and callers already need to pattern match.

## Schema Law Helpers

`Axial.Schema.Testing` can grow reusable FsCheck properties over its existing generators:

```fsharp
SchemaLaws.parseThenCheck schema
SchemaLaws.generatedModelsCheck schema
SchemaLaws.codecRoundTrip schema codec
SchemaLaws.transitionPreserves schema transition
SchemaLaws.refinementAgrees schema construct rawGenerator
SchemaLaws.migrationProducesCurrent contract oldGenerator
```

Each law should state exactly what it guards:

- `parseThenCheck` guards interpreter disagreement immediately after construction.
- `generatedModelsCheck` guards the generated accepted-value path.
- `codecRoundTrip` guards loss or mismatch in trusted serialization.
- `transitionPreserves` guards a total transition's claim to keep intrinsic invariants.
- `refinementAgrees` guards duplicated constructor and portable constraint declarations.
- `migrationProducesCurrent` guards schema validity after typed migration.

Schema-derived generators mostly explore accepted inputs. Constructor-agreement laws also need broad raw generators and
values around constraint limits. Custom constraints must require caller-owned distributions rather than guessed input.

These are semantic tests, not proofs. Their value is that a developer writes one property instead of rebuilding the
same plumbing for each domain module.

## Compile-Negative Harness

Keep small fixture projects under a compile-fail test tree:

```text
tests/compile-fail/private-aggregate-construction/
tests/compile-fail/incomplete-schema-builder/
tests/compile-fail/missing-flow-environment/
tests/compile-fail/missing-contract-migration/
```

Each fixture declares an expected compiler code or stable diagnostic fragment. A runner builds every fixture and
requires failure for the expected reason.

This guards claims such as:

- callers cannot construct an opaque aggregate;
- incomplete object shapes cannot become `Schema<'model>` values;
- a workflow cannot access an absent environment member;
- a new contract version requires a typed migration.

Normalize paths and volatile compiler wording before comparing diagnostics. Reject unexpected additional errors so a
broken fixture cannot pass for the wrong reason.

These fixtures belong in Axial's conformance suite and example templates. Do not add them to every consumer build.

## Generated Architecture Manifest

A build task can generate a compact description from compiled truth and explicit classifications:

```json
{
  "assembly": "MyApp.Domain",
  "role": "Domain",
  "references": ["Axial.ErrorHandling"],
  "opaqueTypes": ["Booking"],
  "forbiddenCalls": [],
  "publicBoundaryTypes": []
}
```

Inputs should be:

- MSBuild project roles;
- assembly references and public metadata;
- explicit markers such as `DeriveSchema`, `WireContract`, or `InvariantAggregate`;
- results from compiled-call and public-surface audits.

Consumers may include CI reports, LSP hints, documentation, semantic diffs, and compact LLM context.

Generate the manifest rather than maintain it by hand. It should report facts and explicit classifications, not infer
business meaning from names.

## Proposed Tooling Order

Build the smallest high-confidence mechanisms first:

1. Document opaque `.fsi` domain modules and project separation using current F# features.
2. Add schema law helpers to `Axial.Schema.Testing` after their call shapes are proven in repository tests.
3. Add a compile-negative harness for Axial's own compiler guarantees and contract MSBuild integration.
4. Prototype project-role validation and compiled-call/public-surface audits in a repository script or test project.
5. Package proven checks as an optional `Axial.Architecture.Build` package.
6. Add editor analyzers only for repeated, high-signal violations where faster feedback matters.

The contracts build package should remain focused on contract generation. Architecture policy can reuse its MSBuild
packaging approach without coupling the two concerns.

## Limits Of Automation

Automation cannot reliably decide:

- whether a rule is intrinsic or contextual;
- whether two fields belong in one aggregate;
- whether a default is legitimate business behavior;
- whether a transition should exist;
- whether a migration preserves meaning rather than only shape;
- whether an invariant is complete;
- whether a domain name matches the language used by the team.

Guardrails should concentrate those decisions in constructors, `.fsi` interfaces, transitions, policies, and migrations.
Reviewers can then discuss the business decision instead of checking repeated plumbing across the codebase.

## User Emphasis

User-facing material should repeat these points:

- Axial is not primarily a validator.
- Schema converts untrusted representations into admitted values or diagnostics.
- The type system, not schema metadata, carries durable invariants through the application.
- Application functions decide whether an already trusted value is acceptable for one operation.
- Flow makes effect dependencies and expected failures visible in interfaces.
- Project references, analyzers, and CI turn architectural intent into guardrails.

The intended application shape is: parse once, enter strong domain types, perform named legal transitions, and keep the
majority of business code free of repeated validation branches.
