---
title: Testing
---

# Testing

Supply fake environment values or replace layers at the execution boundary. Public examples use the same workflow
shape as production code.

A first test is a fake environment record, one `Flow.run`, and one assertion — no HTTP or layers required:

```fsharp prepare
// Setup for the checked example on this page.
open Axial

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
type UserLookup = { FindName: int -> string }

type UserEnv = { Lookup: UserLookup }

let findUserName (id: int) : Flow<UserEnv, Never, string> =
    Flow.envWith (fun env -> env.Lookup.FindName id)

// The test supplies a fake environment; production supplies the real lookup.
let fakeEnv = { Lookup = { FindName = fun id -> if id = 42 then "Ada" else "unknown" } }
```

```fsharp run
findUserName 42 |> Flow.run fakeEnv |> shouldEqual (Exit.Success "Ada" : Exit<string, Never>)
findUserName 1 |> Flow.run fakeEnv |> shouldEqual (Exit.Success "unknown" : Exit<string, Never>)
```

- [HTTP testing and layers](/http/testing.html)
- [Test fakes](fakes.html)
- [Runnable examples](runnable-examples.html)
