# Architecture Guardrails

Axial should help applications compress intent, domain knowledge, and invariants into human-reviewable interfaces that
also constrain generated code. Schema establishes trust at data boundaries; private domain types preserve it; named
domain transitions evolve it; Flow makes operational dependencies and failures explicit.

This note records the remaining recommendations from the 2026-07 architecture review. Contract generation freshness is
not listed as a separate CI command because the planned `Grpc.Tools`-style MSBuild targets make generation part of the
ordinary build.

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

- Parse at every external boundary, then pass domain types inward rather than `RawInput`, DTOs, drafts, or wire records.
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
- Reject `RawInput`, generated wire records, and draft types in domain or application public interfaces.
- Check that domain aggregates do not expose public unchecked construction or mutation.
- Check that expected domain failures use typed error channels rather than exceptions or strings.
- Make generated contract/schema output an incremental, deterministic dependency of normal MSBuild compilation.

These rules should come from a small checked-in architecture manifest where practical, so documentation and executable
checks share one declaration.

## Analyzer Candidates

Only promote high-signal rules into analyzers. Candidates include:

- A public record schema uses `Schema.buildResult` for a claimed durable aggregate invariant.
- A `Schema.check` result is discarded.
- A Flow-producing application function calls an ambient operational effect.
- `Service.resolve` appears outside a designated host-edge assembly.
- A boundary representation crosses into a domain or application interface.
- A generated wire type is used as a business-domain type.

Diagnostics should explain the safe alternative and allow an explicit local suppression for deliberate exceptions.

## Compile-Negative Proofs

Add compile-negative tests or templates for important guarantees:

- Private refined and aggregate constructors cannot be called from application code.
- Incomplete or misordered progressive schema builders do not compile.
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

## User Emphasis

User-facing material should repeat these points:

- Axial is not primarily a validator.
- Schema converts untrusted representations into admitted values or diagnostics.
- The type system, not schema metadata, carries durable invariants through the application.
- Contextual rules decide whether an already trusted value is acceptable for one operation.
- Flow makes effect dependencies and expected failures visible in interfaces.
- Project references, analyzers, and CI turn architectural intent into guardrails.

The intended application shape is: parse once, enter strong domain types, perform named legal transitions, and keep the
majority of business code free of repeated validation branches.
