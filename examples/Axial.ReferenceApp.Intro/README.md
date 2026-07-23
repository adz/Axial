# Introductory reference app (Axial.ErrorHandling only)

A conference registration desk built with only the `Axial.ErrorHandling` package. It is the first tier of the
reference apps: everything here is plain F# `Result` with your own error union, and no Axial type appears in a
domain signature unless you choose a refined value.

The full reference app in [`examples/Axial.ReferenceApp`](../Axial.ReferenceApp/) is the next tier: the same
philosophy at schema boundaries, with versioned contracts, codecs, and Flow.

## Run it

```bash
dotnet run --project examples/Axial.ReferenceApp.Intro/Axial.ReferenceApp.Intro.fsproj --nologo
```

## What it demonstrates

Read `Program.fs` top to bottom:

1. **Checks** — `Check.minLength`/`Check.maxLength` plus `Result.orError` turn reusable constraints into your own
   `BadgeError` union without hand-rolled `if`/`Error` plumbing.
2. **`result {}`** — dependent steps (`parseTier`, then `Parse.int`, then a range gate) fail fast and stay one
   ordinary `Result<Tier * int, TicketError>`.
3. **`refine {}`** — raw strings become refined domain values (`PositiveInt`, `NonBlankString`) wrapped in
   domain types; a `Contact` cannot exist unless every parse succeeded.

## What it deliberately avoids

No `Schema`, no accumulated path-aware boundary errors, no `Flow`, and no persistence. When boundary parsing needs
complete error reports, redisplay, JSON, metadata, or versioning,
graduate to the schema tier; when workflows need dependencies, cancellation, or resources, graduate to Flow.
