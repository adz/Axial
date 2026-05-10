---
title: "Check"
---

This page shows the source-documented `Check` surface: the unit-failure result type and reusable predicate helpers.

## Core type

- [`FsFlow.Check`](./t-check.md): A reusable predicate result that either preserves a value on success or acts as a gate with
 `unit` on success, while carrying a unit failure placeholder until the caller maps it into
 a domain-specific error.

## Module functions

- [`FsFlow.CheckModule.fromPredicate`](./m-checkmodule-frompredicate.md): Builds a check from a predicate while preserving the successful value.
- [`FsFlow.CheckModule.not`](./m-checkmodule-not.md): Returns success when the supplied check fails.
- [`FsFlow.CheckModule.and`](./m-checkmodule-and.md): Returns success when both checks succeed.
- [`FsFlow.CheckModule.or`](./m-checkmodule-or.md): Returns success when either check succeeds.
- [`FsFlow.CheckModule.all`](./m-checkmodule-all.md): Returns success when every check in the sequence succeeds.
- [`FsFlow.CheckModule.any`](./m-checkmodule-any.md): Returns success when at least one check in the sequence succeeds.
- [`FsFlow.CheckModule.okIf`](./m-checkmodule-okif.md): Returns success when the condition is true.
- [`FsFlow.CheckModule.failIf`](./m-checkmodule-failif.md): Returns success when the condition is false.
- [`FsFlow.CheckModule.okIfSome`](./m-checkmodule-okifsome.md): Returns the value when the option is `Some`.
- [`FsFlow.CheckModule.okIfNone`](./m-checkmodule-okifnone.md): Returns success when the option is `None`.
- [`FsFlow.CheckModule.failIfSome`](./m-checkmodule-failifsome.md): Returns success when the option is `None`.
- [`FsFlow.CheckModule.failIfNone`](./m-checkmodule-failifnone.md): Returns the value when the option is `Some`.
- [`FsFlow.CheckModule.okIfValueSome`](./m-checkmodule-okifvaluesome.md): Returns the value when the value option is `ValueSome`.
- [`FsFlow.CheckModule.okIfValueNone`](./m-checkmodule-okifvaluenone.md): Returns success when the value option is `ValueNone`.
- [`FsFlow.CheckModule.failIfValueSome`](./m-checkmodule-failifvaluesome.md): Returns success when the value option is `ValueNone`.
- [`FsFlow.CheckModule.failIfValueNone`](./m-checkmodule-failifvaluenone.md): Returns the value when the value option is `ValueSome`.
- [`FsFlow.CheckModule.okIfNotNull`](./m-checkmodule-okifnotnull.md): Returns the value when it is not null.
- [`FsFlow.CheckModule.okIfNull`](./m-checkmodule-okifnull.md): Returns success when the value is null.
- [`FsFlow.CheckModule.failIfNotNull`](./m-checkmodule-failifnotnull.md): Returns success when the value is null.
- [`FsFlow.CheckModule.failIfNull`](./m-checkmodule-failifnull.md): Returns the value when it is null.
- [`FsFlow.CheckModule.okIfNotEmpty`](./m-checkmodule-okifnotempty.md): Returns the sequence when it is not empty.
- [`FsFlow.CheckModule.okIfEmpty`](./m-checkmodule-okifempty.md): Returns success when the sequence is empty.
- [`FsFlow.CheckModule.failIfNotEmpty`](./m-checkmodule-failifnotempty.md): Returns success when the sequence is empty.
- [`FsFlow.CheckModule.failIfEmpty`](./m-checkmodule-failifempty.md): Returns the sequence when it is not empty.
- [`FsFlow.CheckModule.okIfEqual`](./m-checkmodule-okifequal.md): Returns success when the values are equal.
- [`FsFlow.CheckModule.okIfNotEqual`](./m-checkmodule-okifnotequal.md): Returns success when the values are not equal.
- [`FsFlow.CheckModule.failIfEqual`](./m-checkmodule-failifequal.md): Returns success when the values are equal.
- [`FsFlow.CheckModule.failIfNotEqual`](./m-checkmodule-failifnotequal.md): Returns success when the values are not equal.
- [`FsFlow.CheckModule.okIfNonEmptyStr`](./m-checkmodule-okifnonemptystr.md): Returns the string when it is not null or empty.
- [`FsFlow.CheckModule.okIfEmptyStr`](./m-checkmodule-okifemptystr.md): Returns success when the string is null or empty.
- [`FsFlow.CheckModule.failIfNonEmptyStr`](./m-checkmodule-failifnonemptystr.md): Returns success when the string is null or empty.
- [`FsFlow.CheckModule.failIfEmptyStr`](./m-checkmodule-failifemptystr.md): Returns the string when it is null or empty.
- [`FsFlow.CheckModule.okIfNotBlank`](./m-checkmodule-okifnotblank.md): Returns the string when it is not blank.
- [`FsFlow.CheckModule.notBlank`](./m-checkmodule-notblank.md): Returns the string when it is not blank.
- [`FsFlow.CheckModule.okIfBlank`](./m-checkmodule-okifblank.md): Returns success when the string is blank.
- [`FsFlow.CheckModule.blank`](./m-checkmodule-blank.md): Returns success when the string is blank.
- [`FsFlow.CheckModule.failIfNotBlank`](./m-checkmodule-failifnotblank.md): Returns success when the string is blank.
- [`FsFlow.CheckModule.failIfBlank`](./m-checkmodule-failifblank.md): Returns the string when it is blank.
- [`FsFlow.CheckModule.orError`](./m-checkmodule-orerror.md): Maps a unit error into the supplied application error value.
- [`FsFlow.CheckModule.orErrorWith`](./m-checkmodule-orerrorwith.md): Maps a unit error into an application error produced on demand.
- [`FsFlow.CheckModule.notNull`](./m-checkmodule-notnull.md): Returns the value when it is not null.
- [`FsFlow.CheckModule.notEmpty`](./m-checkmodule-notempty.md): Returns the sequence when it is not empty.
- [`FsFlow.CheckModule.equal`](./m-checkmodule-equal.md): Returns success when the values are equal.
- [`FsFlow.CheckModule.notEqual`](./m-checkmodule-notequal.md): Returns success when the values are not equal.

