---
title: "Check"
weight: 70
type: docs
---

This page shows the `Check` surface for reusable, pure predicates. A check is a `Result<unit, unit>`-style decision: it says whether a condition passed, without deciding the final domain error yet. This makes checks easy to compose, negate, reuse, and convert into typed failures with `orError` or `orErrorWith`. Use `Check` for local facts such as non-empty strings, equality, null checks, and option presence. When you need to collect several named failures, move to `Validation`; when you need environment or async work, lift the result into `Flow`.

## Core type

- [`Check`](./t-check.md): 
 Predicate helpers that return <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> values with a unit error,
 plus the bridge functions that turn those checks into application errors. Some helpers preserve
 the source value; others are gates and return <code>unit</code> on success.
 

## Structured errors

- [`CheckError`](./t-checkerror.md): Structured errors returned by error-rich check helpers.
- [`CardinalityFailure`](./t-cardinalityfailure.md): Structured errors returned by sequence cardinality checks.

## Module functions

- [`Check.fromPredicate`](./m-check-frompredicate.md): Builds a check from a predicate while preserving the successful value.
- [`Check.fromTry`](./m-check-fromtry.md): Converts a .NET <code>Try*</code> tuple into a check result.
- [`Check.fromChoice`](./m-check-fromchoice.md): Converts an F# <code>Choice</code> into a <code>Result</code>.
- [`Check.okIfTrueTuple`](./m-check-okiftruetuple.md): Alias for <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-checkmodule.html">Check.fromTry</a> when tuple-form intent should be explicit.
- [`Check.``not```](./m-check-not.md): Returns success when the supplied check fails.
- [`Check.``and```](./m-check-and.md): Returns success when both checks succeed.
- [`Check.``or```](./m-check-or.md): Returns success when either check succeeds.
- [`Check.all`](./m-check-all.md): Returns success when every check in the sequence succeeds.
- [`Check.any`](./m-check-any.md): Returns success when at least one check in the sequence succeeds.
- [`Check.okIf`](./m-check-okif.md): Returns success when the condition is true.
- [`Check.failIf`](./m-check-failif.md): Returns success when the condition is false.
- [`Check.okIfSome`](./m-check-okifsome.md): Returns the value when the option is <code>Some</code>.
- [`Check.okIfNone`](./m-check-okifnone.md): Returns success when the option is <code>None</code>.
- [`Check.failIfSome`](./m-check-failifsome.md): Returns success when the option is <code>None</code>.
- [`Check.failIfNone`](./m-check-failifnone.md): Returns the value when the option is <code>Some</code>.
- [`Check.okIfValueSome`](./m-check-okifvaluesome.md): Returns the value when the value option is <code>ValueSome</code>.
- [`Check.okIfValueNone`](./m-check-okifvaluenone.md): Returns success when the value option is <code>ValueNone</code>.
- [`Check.failIfValueSome`](./m-check-failifvaluesome.md): Returns success when the value option is <code>ValueNone</code>.
- [`Check.failIfValueNone`](./m-check-failifvaluenone.md): Returns the value when the value option is <code>ValueSome</code>.
- [`Check.okIfNotNullable`](./m-check-okifnotnullable.md): Returns the value when the nullable has a value.
- [`Check.okIfNullable`](./m-check-okifnullable.md): Returns success when the nullable has no value.
- [`Check.failIfNotNullable`](./m-check-failifnotnullable.md): Returns success when the nullable has no value.
- [`Check.failIfNullable`](./m-check-failifnullable.md): Returns the value when the nullable has a value.
- [`Check.notNullable`](./m-check-notnullable.md): Returns the value when the nullable has a value, or a structured null error when it does not.
- [`Check.okIfNotNull`](./m-check-okifnotnull.md): Returns the value when it is not null.
- [`Check.okIfNull`](./m-check-okifnull.md): Returns success when the value is null.
- [`Check.failIfNotNull`](./m-check-failifnotnull.md): Returns success when the value is null.
- [`Check.failIfNull`](./m-check-failifnull.md): Returns the value when it is null.
- [`Check.okIfNotEmpty`](./m-check-okifnotempty.md): Returns the sequence when it is not empty.
- [`Check.okIfEmpty`](./m-check-okifempty.md): Returns success when the sequence is empty.
- [`Check.failIfNotEmpty`](./m-check-failifnotempty.md): Returns success when the sequence is empty.
- [`Check.failIfEmpty`](./m-check-failifempty.md): Returns the sequence when it is not empty.
- [`Check.okIfExactlyOne`](./m-check-okifexactlyone.md): Returns the single element when the sequence contains exactly one item.
- [`Check.failIfExactlyOne`](./m-check-failifexactlyone.md): Returns the sequence when it does not contain exactly one item.
- [`Check.okIfAtMostOne`](./m-check-okifatmostone.md): Returns an optional single element when the sequence contains at most one item.
- [`Check.failIfAtMostOne`](./m-check-failifatmostone.md): Returns the sequence when it contains more than one item.
- [`Check.okIfCountIs`](./m-check-okifcountis.md): Returns success when the sequence count matches the expected count.
- [`Check.okIfContains`](./m-check-okifcontains.md): Returns success when the sequence contains the expected value.
- [`Check.okIfEqual`](./m-check-okifequal.md): Returns success when the values are equal.
- [`Check.okIfNotEqual`](./m-check-okifnotequal.md): Returns success when the values are not equal.
- [`Check.failIfEqual`](./m-check-failifequal.md): Returns success when the values are equal.
- [`Check.failIfNotEqual`](./m-check-failifnotequal.md): Returns success when the values are not equal.
- [`Check.okIfNonEmptyStr`](./m-check-okifnonemptystr.md): Returns the string when it is not null or empty.
- [`Check.okIfEmptyStr`](./m-check-okifemptystr.md): Returns success when the string is null or empty.
- [`Check.failIfNonEmptyStr`](./m-check-failifnonemptystr.md): Returns success when the string is null or empty.
- [`Check.failIfEmptyStr`](./m-check-failifemptystr.md): Returns the string when it is null or empty.
- [`Check.okIfNotBlank`](./m-check-okifnotblank.md): Returns the string when it is not blank.
- [`Check.notBlank`](./m-check-notblank.md): Returns the string when it is not blank.
- [`Check.okIfBlank`](./m-check-okifblank.md): Returns success when the string is blank.
- [`Check.blank`](./m-check-blank.md): Returns success when the string is blank.
- [`Check.failIfNotBlank`](./m-check-failifnotblank.md): Returns success when the string is blank.
- [`Check.failIfBlank`](./m-check-failifblank.md): Returns the string when it is blank.
- [`Check.orError`](./m-check-orerror.md): Maps a unit error into the supplied application error value.
- [`Check.orErrorWith`](./m-check-orerrorwith.md): Maps a unit error into an application error produced on demand.
- [`Check.notNull`](./m-check-notnull.md): Returns the value when it is not null.
- [`Check.notEmpty`](./m-check-notempty.md): Returns the sequence when it is not empty.
- [`Check.equal`](./m-check-equal.md): Returns success when the values are equal.
- [`Check.notEqual`](./m-check-notequal.md): Returns success when the values are not equal.

