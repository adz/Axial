---
title: "FsFlow.ValidationModule.bind"
linkTitle: "bind`"
type: docs
---

Sequences a validation-producing continuation.

## Remarks

This is the monadic "bind" for validation. Note that this operation short-circuits
 and does not accumulate errors from the binder if the source has already failed.
 For accumulation, use `map2` or the applicative `and!` syntax.


## Parameters

- `binder`: A function of type `'value -> Validation&lt;'next, 'error&gt;`.
- `validation`: The source validation.

## Returns

The result of the binder or the original diagnostics.

