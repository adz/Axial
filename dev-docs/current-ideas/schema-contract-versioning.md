# Schema Contract Versioning Sketch

Status: pre-idea (2026-07-07). Not accepted architecture. This sketches a **Contract** concept: a named, versioned
family of frozen schemas with manually written, typed migrations. Motivating boundaries: config files, messages,
event-sourced events, and (later) database records.

## The Idea

A schema stops being "the description of my current type" and becomes a version chain:

```fsharp
module SignupContract =
    module V1 =
        type Signup = { Email: string }
        let schema = ...   // explicit pipeline, frozen forever

    module V2 =
        type Signup = { Email: string; Age: int }
        let schema = ...   // current

    let contract =
        Contract.create "signup" V2.schema
        |> Contract.supersedes V1.schema (fun (v1: V1.Signup) ->
            Ok { Email = v1.Email; Age = 18 })
```

- Raw input is parsed against the schema of the version it was written under, so it gets that version's validation
  exactly as authored. Migration then runs between **parsed** types — total, typed, unit-testable ordinary F# — never
  between raw representations.
- Migrations are n-1 → n only; older versions compose through the chain. Writes always emit the newest version.
- Superseded versions are frozen checked-in code: the version record and its schema never change again. This is the
  event-sourcing upcaster pattern generalized to config and messages.

## Design Decisions To Settle

1. **Version detection.** Prefer an explicit version field owned by the contract (config `version: 2`, event
   metadata, message header) over structural sniffing, which breaks when two versions are shape-compatible. At most
   one designated "unversioned input means V1" legacy rule.
2. **Constraint drift.** V1-valid data can violate V2 constraints. After migrating, re-validate the result against the
   current schema so "valid instance" always means valid under the *current* contract. Migration signature is
   therefore fallible — `'prev -> Result<'next, MigrationError>` — with an infallible convenience overload.
3. **Lifecycle is not core.** Events migrate on read forever (stored events are immutable, so old versions stay
   permanently load-bearing); databases eventually migrate at rest and retire versions. Keep that policy split out of
   the core Contract type and in integration layers.
4. **Boundary/domain split.** Version records are plain public records of primitives — wire shapes constructed only by
   the parser and migrations. The *current* model is the refined one (`Value.refined` bridge; smart-constructor types
   such as `Email`), so direct construction in application code cannot be invalid and version records are visibly
   ingestion artifacts. Invariant-bearing constraints live in the refined types; schema-level constraints cover
   boundary concerns (presence, trimming, external names, representation).

## Why This Reinforces Existing Decisions

- **No attributes / no generation for versions.** Attributes describe the current type only; a superseded version
  needs standalone frozen schema code, which is exactly what the explicit pipeline (with `Axial.Schema.DSL` for
  terseness) produces. When a version is cut, the previous head materializes as ordinary committed code anyway.
- **No Draft type.** "Draft vs valid" is already the pre-`Ok` state of `Input.parse` (`parsed.Result` +
  `parsed.Errors`). A contract adds version dispatch in front of that gate and migration behind it; it does not need a
  parallel draft concept. Editing UIs hold the un-parsed/erroneous state; published desired state is always a valid,
  versioned instance.

## Remote Desired-State Configuration (motivating scenario)

A central editor sets desired config; devices ingest, attempt to apply, and report back.

- Shared rules mean the *same compiled schema code* in the browser editor (Fable) and on the device (.NET/AOT) — not
  reimplemented rules. The schema also drives the editor form via inspection metadata.
- Version skew: devices ingest older versions via the chain, but must cleanly reject *newer* versions than they know,
  reporting their highest supported version. Mixed fleets imply deliberate downgrade emission at the center — a
  separate lossy feature, not an accident of the chain.
- Report-back distinguishes **contract rejection** (bug or version skew — alarm) from **application failure** (valid
  config, device-local state prevented applying it — expected operational case). Schema constraints can never capture
  device-local state.

## Rejected Alternatives

- **External schema language + type provider.** Generative TPs cannot emit F# records (the models would stop being
  records), TPs do not run under Fable, and TP tooling cost is the highest in the ecosystem. An external language also
  inverts ownership of the domain types and adds a parser/diagnostics/editor-support burden.
- **Attribute-driven generation as the versioning surface.** See above; generation (per
  `schema-source-generation.md`) remains a possible sugar for the *head* version only.

## Promotion Criteria

Do not start `Contract` machinery until a concrete consumer exists (the remote-config scenario or event-sourced
storage). When opened: design `Contract.create` / `Contract.supersedes` / `Contract.parse` against
`Axial.Validation.Schema` interpreters, plus instance re-validation (`Validation.validate`) for post-migration checks.
