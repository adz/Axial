---
title: Providing Services from a Package
description: Authoring a library that asks callers for a dependency it cannot name.
---

# Providing Services from a Package

Application code owns both sides of its environment: the workflow names `AppEnv`, and the composition root supplies
one. A package is compiled before its callers exist, so it cannot mention their types or require one record shape.
This page covers the package side of [service contracts](/dependencies/service-contracts.html).

```fsharp prepare
/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## The shape

Declare an interface, a contract (`IHasFoo` exposing one `Foo` member), and a module-level accessor. The
[reusable services tutorial](custom-services.html) walks through the complete exchange-rate example — the
`IExchangeRates` interface, the `IHasExchangeRates` contract, the `ExchangeRates.service` accessor, a `priceInAud`
helper, an app environment, a typed failure, and a test double. This page covers the rules that keep such packages
composable.

## Rules that keep contracts composable

**One member per contract, named after the suffix.** `IHasFoo` exposes `Foo`. This is what makes `Flow.envWith _.Foo`
predictable and keeps a consumer's composition root readable when it implements six of them.

**Never inherit a generic interface.** F# rejects a type parameter constrained by two instantiations of the same
generic interface, so a generic parent makes your contract impossible to combine with any other, including one from
a different package. A contract inherits nothing, or inherits other plain contracts.

```fsharp no-check reason="Shows the definition to avoid beside the one to use; both have the same name"
type IHasRates = inherit IServiceContract<IExchangeRates>   // do not do this
type IHasRates = abstract ExchangeRates : IExchangeRates    // do this
```

**Member names may collide freely.** Two packages can both define `IHasClient` exposing `Client`, and one record can
implement both. The record's implementations are explicit — each `interface` block names the contract it implements
— so two contracts that expose the same member name do not clash and package authors do not need to coordinate
names:

```fsharp
// Two packages independently define contracts with the same member name.
type IHasClient =
    abstract Client : string

type IHasGateway =
    abstract Client : string

// One record implements both; each interface block names the contract it implements.
type ClientEnv =
    { ClientValue: string
      GatewayValue: string }
    interface IHasClient with
        member this.Client = this.ClientValue
    interface IHasGateway with
        member this.Client = this.GatewayValue
```

```fsharp run
let clientEnv = { ClientValue = "http"; GatewayValue = "db" }
(clientEnv :> IHasClient).Client |> shouldEqual "http"
(clientEnv :> IHasGateway).Client |> shouldEqual "db"
```

## Typed errors belong in the package

Publishing operations lets the package wrap the failure model once. Compare the raw interface call with what
`Axial.FileSystem` publishes:

```fsharp no-check reason="Two calls side by side for their result types"
fileSystem.ReadAllText path                 // string, throws
FileSystem.readAllText path                 // Flow<'env, FileSystemError, string>
```

`FileSystem.readAllText` uses `Flow.catch` to classify exceptions into a union the caller can match on.

## Also expose the raw service

Publish the accessor (`ExchangeRates.service`) as part of the public API. Callers occasionally need the interface
itself for interop, and without it there is no way to reach it once the environment is contract-based. Axial's own
packages all do this.
