---
title: Recommended Patterns
weight: 70
description: Practical ways to keep untrusted input, trusted domain values, and legal updates separate.
---

# Recommended patterns

These pages show how to use Schema with ordinary F# modules and projects. They focus on problems that appear as an
application grows: public records bypassing checks, wire DTOs entering business code, and updates breaking invariants.

Use only the strength of guarantee the code needs. A plain record is often right for a wire payload or edit form. A
private type is useful when many callers must rely on the same invariant without checking it again.

- [Build a private aggregate](./private-aggregates/) — keep record syntax inside the owning module while callers see
  only safe construction and update functions.
- [Model legal transitions](./legal-transitions/) — replace unrestricted record updates with named operations and typed
  refusals.
- [Separate wire and domain models](./wire-and-domain-models/) — generate permissive wire schemas during the build,
  then admit them into hand-written domain types.
- [Split a larger application](./project-structure/) — use project references to stop boundary and infrastructure
  types from reaching the domain.
- [Test schema guarantees](./testing-schema-guarantees/) — study the repository-only FsCheck adapter pattern for
  testing constructors, transitions, codecs, and migrations.

Start with [Construction Guarantees](../trusted-construction/) if you are deciding whether a public record, refined
field, or private aggregate fits the model.
