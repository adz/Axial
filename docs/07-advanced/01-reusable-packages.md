---
title: Providing Services from a Package
description: Authoring a library that asks callers for a dependency it cannot name.
---

# Providing Services from a Package

Application code owns both sides of its environment: the workflow names `AppEnv`, and the composition root supplies
one. A package author has neither. Your library is compiled before its callers exist, so it cannot mention their
types — and it should not force every consumer into one record shape.

This page is the authoring side of [service contracts](/dependencies/service-contracts.html). Everything Axial's own
service packages do, you can do.

## The shape

Three declarations per service, and the third is the only one with any subtlety.

**The service** — an ordinary interface describing the capability:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type IExchangeRates =
    abstract GetUsdToAud : unit -> Task<decimal>
```

**The contract** — how an environment advertises that it supplies one. Named `IHasFoo`, exposing exactly one member
`Foo`:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type IHasExchangeRates =
    abstract ExchangeRates : IExchangeRates
```

**The accessor** — one module-level binding that reads it:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
[<RequireQualifiedAccess>]
module ExchangeRates =
    let service<'env, 'error when 'env :> IHasExchangeRates> : Flow<'env, 'error, IExchangeRates> =
        Flow.read _.ExchangeRates
```

Bind the accessor at module level, not inline. `Flow.read _.ExchangeRates` cannot resolve inside a `flow { }` block,
because the lambda's parameter type is not known until the surrounding annotation is applied — and that happens after
the body is checked. At module level the annotation sits next to the expression that needs it, so it resolves once
and every caller binds it with no annotation at all.

Everything the package publishes then builds on the accessor:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let priceInAud (usdAmount: decimal) : Flow<#IHasExchangeRates, RateError, decimal> =
    flow {
        let! rates = ExchangeRates.service
        let! rate = rates.GetUsdToAud()
        return usdAmount * rate
    }
```

## Rules that keep contracts composable

**One member per contract, named after the suffix.** `IHasFoo` exposes `Foo`. This is what makes `Flow.read _.Foo`
predictable and keeps a consumer's composition root readable when it implements six of them.

**Never inherit a generic interface.** F# rejects a type parameter constrained by two instantiations of the same
generic interface, so a generic parent makes your contract impossible to combine with any other — including one from
a different package. A contract inherits nothing, or inherits other plain contracts.

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
type IHasRates = inherit IServiceContract<IExchangeRates>   // do not do this
type IHasRates = abstract ExchangeRates : IExchangeRates    // do this
```

**Member names may collide freely.** Two packages can both define `IHasClient` exposing `Client`, and one record can
implement both — F# interface implementations are always explicit, so there is no ambiguity and no coordination
needed between package authors.

## Typed errors belong in the package

The reason to publish operations rather than just the interface is that you can wrap the failure model once. Compare
the raw interface call with what `Axial.FileSystem` publishes:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
fileSystem.ReadAllText path                 // string, throws
FileSystem.readAllText path                 // Flow<'env, FileSystemError, string>
```

The second is the first plus `Flow.catch`, classifying exceptions into a union the caller can match on. That
translation is the package's job — doing it once is why consumers get typed failures for free.

## Also expose the raw service

Publish the accessor (`ExchangeRates.service`) as part of the public surface. Callers occasionally need the interface
itself for interop, and without it there is no way to reach it once the environment is contract-based. Axial's own
packages all do this.
