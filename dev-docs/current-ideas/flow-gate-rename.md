# Axial — application gate naming

**Status:** extracted idea; not implemented or accepted.

This rename is independent of the Parse/Check/Refinement/Schema cleanup.

## Proposal

Rename application `Policy<'env,'error,'input,'output>` to:

```fsharp
type FlowGate<'env,'error,'input,'output> =
    'env -> 'input -> Result<'output,'error>

type FlowCheck<'env,'error,'value> =
    FlowGate<'env,'error,'value,'value>
```

`FlowGate` may admit or transform input. `FlowCheck` names the same-value specialization.
Neither is a pure `Check<'value>` because it may depend on application environment and
return application errors.

Rename `Flow.verify` to:

```fsharp
Flow.through :
    FlowGate<'env,'error,'input,'output> ->
    'input ->
    Flow<'env,'error,'output>
```

Example:

```fsharp
let canPlaceOrder : FlowGate<AppEnv,OrderError,OrderDraft,Order> =
    ...

flow {
    let! order = Flow.through canPlaceOrder draft
    return order
}
```

Keep `RetryPolicy<'error>` unchanged. Retry policy is established operational
configuration; the ambiguous application-level `Policy` name is the problem.

## Checklist

- [ ] Rename application `Policy` and its module to `FlowGate`.
- [ ] Add `FlowCheck`.
- [ ] Rename `Flow.verify` to `Flow.through`.
- [ ] Update tests, source comments, generated references, and guides.
- [ ] Add API-shape tests proving the old application names are absent.
- [ ] Keep `RetryPolicy` unchanged.
