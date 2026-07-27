# PROTOTYPE — private refined record generation

Questions:

1. Can a generated companion module in a later F# file read fields from a private record while consumers cannot construct or record-update it?
2. Can a generated immutable `Draft` support `{ draft with ... }` inside `Booking.update`?
3. Can user-written `Booking.Create` accept a generated draft type, given F# file ordering and companion-module rules?

Run:

```bash
bash dev-docs/current-ideas/prototypes/refined-private-record/run.sh
```

Throwaway: delete after conclusions are folded into the design brief.

## Findings

- Later-file generated code can read fields from a `private` F# record.
- Consumers cannot construct or record-update that private record.
- A public generated draft supports inferred `{ draft with Field = value }` updates and can be re-admitted through the user constructor.
- A type and same-named companion module cannot be split across files (`FS0250`). Generated API therefore cannot use a later-file `module Booking` when `Booking` is declared at namespace scope.
- A user constructor can accept a generated draft only when that draft is compiled before the user source. Current schemagen inserts generated output immediately after its declaration source, so this requires split pre-source/post-source generation or a different declaration/API shape.
