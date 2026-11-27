---
title: "shared-fixture (AXG004): create fresh test fixtures"
linkTitle: shared-fixture
description: Reports module-level values in xUnit test modules, which every test shares.
---

# shared-fixture (AXG004)

## The problem

A module-level `let` runs once, and every test in the module gets the same value. xUnit runs tests in parallel, so two
tests that change it interfere. The failure depends on timing, and passes when you run the test alone.

```fsharp no-check reason="Test module shape; the docs build has no xUnit"
let store = Dictionary<string, int>()     // Reported: shared by every test below

[<Fact>]
let ``adds an item`` () =
    store["a"] <- 1
    Assert.Equal(1, store.Count)          // fails when another test added to store first
```

```fsharp no-check reason="Test module shape; the docs build has no xUnit"
let newStore () = Dictionary<string, int>()   // Fixed: each test makes its own

[<Fact>]
let ``adds an item`` () =
    let store = newStore ()
    store["a"] <- 1
    Assert.Equal(1, store.Count)
```

## What it reports

Module-level values in a module that has an xUnit `[<Fact>]` or `[<Theory>]`, or an FsCheck `[<Property>]`.
Functions, constants and point-free function definitions are not reported. There is no automatic fix, because every
use must change to a call.

## When it is deliberate

For a value that cannot change, add a reason:

```fsharp
let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-shared-fixture: immutable timestamp
```
