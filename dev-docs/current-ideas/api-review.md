# Pre-1.0 API review candidates

This sketch records public APIs that deserve a deliberate decision before Axial 1.0. It does not assert that every
candidate must change. For each item, test the current API in realistic application code, choose the intended shape,
update tests and user documentation, and remove the item once the decision has moved into current architecture or code.

All items from the original review are closed. See [`schedule-expansion.md`](schedule-expansion.md) for the two
open, non-blocking questions that came out of the schedule contract item.

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
