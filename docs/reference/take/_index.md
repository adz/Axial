---
title: "Take"
weight: 75
---

This page shows the `Take` surface for checks that should return a useful value. `Take.whenX` helpers keep the original input when a property holds, while bare `Take.x` helpers extract or narrow the value exposed by that property. Use `Take` when a later step needs the non-blank string, unwrapped option value, non-null reference, or cardinality-narrowed element. Attach domain errors to unit-error Take results with `Check.withError` in pure code, or use `BindError.withError` at a `flow { }` bind site. Cardinality helpers already carry `CardinalityFailure`; use `Result.mapError` when you need a domain error.

## Option and nullable

- [`Take.whenSome`](./m-take-whensome.md): Keeps the option when it is <code>Some</code>.
- [`Take.some`](./m-take-some.md): Takes the value from an option when it is <code>Some</code>.
- [`Take.whenValueSome`](./m-take-whenvaluesome.md): Keeps the value option when it is <code>ValueSome</code>.
- [`Take.valueSome`](./m-take-valuesome.md): Takes the value from a value option when it is <code>ValueSome</code>.
- [`Take.whenHasValue`](./m-take-whenhasvalue.md): Keeps the nullable when it has a value.
- [`Take.hasValue`](./m-take-hasvalue.md): Takes the value from a nullable when it has a value.
- [`Take.whenNotNull`](./m-take-whennotnull.md): Keeps the reference when it is not null.

## Strings and collections

- [`Take.whenNotEmpty`](./m-take-whennotempty.md): Keeps the collection when it is not empty.
- [`Take.whenNotNullOrEmpty`](./m-take-whennotnullorempty.md): Keeps the string when it is not null or empty.
- [`Take.whenNotBlank`](./m-take-whennotblank.md): Keeps the string when it is not blank.
- [`Take.whenExactlyOne`](./m-take-whenexactlyone.md): Keeps the collection when it contains exactly one item.
- [`Take.exactlyOne`](./m-take-exactlyone.md): Takes the only item from a sequence when it contains exactly one item.
- [`Take.whenAtMostOne`](./m-take-whenatmostone.md): Keeps the collection when it contains at most one item.
- [`Take.atMostOne`](./m-take-atmostone.md): Takes zero or one item from a sequence when it contains at most one item.
