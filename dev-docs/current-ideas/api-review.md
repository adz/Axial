# Pre-1.0 API review candidates

This sketch records public APIs that deserve a deliberate decision before Axial 1.0. It does not assert that every
candidate must change. For each item, test the current API in realistic application code, choose the intended shape,
update tests and user documentation, and remove the item once the decision has moved into current architecture or code.

## 1. Stabilize the schedule contract

Before freezing schedules, specify and test:

- whether `Schedule.recurs 3` counts runs, retries, or recurrences;
- whether schedules are safely reusable and how state resets;
- deterministic clock and delay behavior;
- composition requirements for 1.0;
- accepted ranges and failure behavior for injected jitter samples;
- elapsed-time outputs;
- overflow and invalid-delay behavior;
- retry and repeat behavior on interruption and defects.

Keep the 1.0 schedule surface small if these semantics do not justify broader composition yet.

## Reviewed and not currently considered awkward

The abbreviated Flow type forms preserve parameter meaning consistently:

```fsharp
type Flow<'value> = Flow<unit, Never, 'value>
type Flow<'error, 'value> = Flow<unit, 'error, 'value>
type Flow<'env, 'error, 'value> = ...
```

The forms progressively add channels from right to left: value; error and value; environment, error, and value.
`'error` does not change meaning between the two- and three-parameter forms. Do not treat these aliases as a 1.0 issue
without separate evidence from application use.

## Suggested review order

1. Exact schedule semantics.
