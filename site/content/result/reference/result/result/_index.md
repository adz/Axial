---
title: "Result"
type: docs
---

This page shows the helpers on the `Result` module.

- [`Result.ok`](./m-result-result-ok.md): Creates an <code>Ok</code> result.
- [`Result.error`](./m-result-result-error.md): Creates an <code>Error</code> result.
- [`Result.map`](./m-result-result-map.md): Maps the success value of a result.
- [`Result.mapError`](./m-result-result-maperror.md): Maps the error value of a result.
- [`Result.bind`](./m-result-result-bind.md): Binds a result to the next fail-fast operation.
- [`Result.orElse`](./m-result-result-orelse.md): Falls back to another result when the source result fails.
- [`Result.orElseWith`](./m-result-result-orelsewith.md): Computes a fallback result from the source error when the result fails.
- [`Result.requireTrue`](./m-result-result-requiretrue.md): Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.
- [`Result.okIf`](./m-result-result-okif.md): Keeps the input value when the predicate holds, or returns the supplied error.
- [`Result.failIf`](./m-result-result-failif.md): Keeps the input value when the predicate does not hold, or returns the supplied error.
- [`Result.orError`](./m-result-result-orerror.md): Replaces whatever error a result carries with the supplied typed error. <code>Ok</code> passes through unchanged.
- [`Result.fromTry`](./m-result-result-fromtry.md): Converts a .NET <code>Try*</code> tuple into a unit-error result.
- [`Result.fromChoice`](./m-result-result-fromchoice.md): Converts an F# <code>Choice</code> into a result.
- [`Result.toOption`](./m-result-result-tooption.md): Drops the error channel and returns <code>Some</code> for success.
- [`Result.toValueOption`](./m-result-result-tovalueoption.md): Drops the error channel and returns <code>ValueSome</code> for success.
- [`Result.defaultValue`](./m-result-result-defaultvalue.md): Returns the success value or the supplied fallback value.
- [`Result.someOr`](./m-result-result-someor.md): Takes the value from an option when it is <code>Some</code>, or returns the supplied error.
- [`Result.noneOr`](./m-result-result-noneor.md): Returns success when the option is <code>None</code>, or returns the supplied error.
- [`Result.valueSomeOr`](./m-result-result-valuesomeor.md): Takes the value from a value option when it is <code>ValueSome</code>, or returns the supplied error.
- [`Result.valueNoneOr`](./m-result-result-valuenoneor.md): Returns success when the value option is <code>ValueNone</code>, or returns the supplied error.
- [`Result.nullableOr`](./m-result-result-nullableor.md): Takes the value from a nullable when it has a value, or returns the supplied error.
- [`Result.notNullOr`](./m-result-result-notnullor.md): Keeps a non-null reference, or returns the supplied error.
- [`Result.okOr`](./m-result-result-okor.md): Takes the successful value from a result, or returns the supplied error.
- [`Result.errorOr`](./m-result-result-erroror.md): Takes the error value from a result, or returns the supplied error when the result is successful.
- [`Result.headOr`](./m-result-result-heador.md): Takes the first item from a sequence, or returns the supplied error.
- [`Result.traverse`](./m-result-result-traverse.md): Maps each value with a result-returning function, stopping at the first error.
- [`Result.sequence`](./m-result-result-sequence.md): Turns a sequence of results into one fail-fast result containing all successes.
- [`Result.tap`](./m-result-result-tap.md): Runs a side effect on the successful value and returns the result unchanged.
- [`Result.tapError`](./m-result-result-taperror.md): Runs a side effect on the error value and returns the result unchanged.
