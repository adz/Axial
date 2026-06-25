# Flow Type Alias Proposal

Status: proposed.

The canonical workflow type remains:

```fsharp
Flow<'env, 'error, 'value>
```

The proposal is to add aliases for common channel shapes:

```fsharp
type Flow<'value> = Flow<unit, Never, 'value>
type Flow<'error, 'value> = Flow<unit, 'error, 'value>
type EnvFlow<'env, 'value> = Flow<'env, Never, 'value>
type ExnFlow<'value> = Flow<unit, exn, 'value>
type ExnEnvFlow<'env, 'value> = Flow<'env, exn, 'value>
```

Use `Never` for "cannot fail"; do not use `unit` for that meaning.

If this proposal is implemented, add explicit attempt-style constructors for recoverable exception interop, while
keeping existing `Flow.fromTask`, `Flow.fromValueTask`, and `Flow.fromAsync` defect-oriented.

Open questions:

- Should `Flow.catch` recover `Cause.Die`, or should defect recovery use a new name?
- Should `attemptAsync` exist for Fable-compatible `Async<'value>` code?
- Should aliases live next to `Flow` and `Never`, or later in the compile order?

