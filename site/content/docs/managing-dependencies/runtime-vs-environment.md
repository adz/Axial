---
weight: 10
title: Runtime vs Environment
description: Understanding the architectural split between operational services and business dependencies.
type: docs
---


FsFlow models dependencies by drawing a strict line between **business dependencies** and **operational services**. This is one of the defining architectural ideas of the library, inspired by the runtime model of ZIO.

In FsFlow:
- **Business dependencies** (repositories, gateways, domain services) live in the **Explicit Environment**.
- **Operational services** (clock, logging, random numbers, scheduling) live in the **Ambient Runtime**.

Understanding *why* this distinction exists is key to writing clean, maintainable workflows that scale without suffering from type-signature bloat.

---

## 1. The Explicit Environment (Business Logic)

The `'env` type parameter in `Flow<'env, 'error, 'value>` is your explicit environment. It should contain only the services that define the **business behavior** of your application.

```fsharp
type AppDeps = { Database: IDbRepo; StripeClient: IPaymentGateway }

let processPayment (amount: decimal) : Flow<AppDeps, PaymentError, unit> =
    flow {
        let! db = Flow.read _.Database
        let! stripe = Flow.read _.StripeClient
        
        // This workflow advertises exactly what it needs to do business logic.
        do! stripe.Charge(amount) |> Guard.Of PaymentFailed
        do! db.MarkAsPaid() |> Guard.Of DatabaseError
    }
```

**Why keep it explicit?**
Because business dependencies change often, require complex mocking in tests, and define the architecture of your application. When you look at `Flow<AppDeps, ...>`, you instantly know this workflow talks to the database and Stripe.

---

## 2. The Ambient Runtime (Operational Logic)

Operational services are things that almost every real-world application needs, but that you rarely want to advertise in your business signatures. 

These include:
- `Clock` (getting the current time)
- `Log` (writing diagnostics)
- `Random` (generating numbers)
- `Guid` (creating unique IDs)
- `EnvironmentVariables` (reading config)

In FsFlow, these are pushed down into the **Ambient Runtime**. You access them via the `Flow.Runtime` module or the `FsFlow.Capabilities.Core` helpers.

```fsharp
let doWork =
    flow {
        // We access the clock without needing IClock in our 'env type
        let! start = Clock.now
        
        // We log without needing ILogger in our 'env type
        do! Log.info $"Starting work at {start}"
    }
```

**Why make it ambient?**
If operational services lived in the explicit environment, *every single workflow* would end up looking like this:
`Flow<IHasClock & IHasLog & IHasRandom & AppDeps, ...>`

This is pure noise. A workflow that charges a credit card shouldn't have to defensively declare that it *might* need to log a diagnostic message.

---

## 3. How Overrides Work (Testing)

The danger of ambient services in traditional .NET (like `DateTime.UtcNow` or `Guid.NewGuid()`) is that they are hardcoded static calls, making testing impossible.

FsFlow solves this by making the runtime ambient **but contextual**. The runtime is threaded through the execution engine behind the scenes. When you need to control time in a test, you simply override the runtime for that specific flow:

```fsharp
[<Fact>]
let ``Test timeout logic`` () =
    let fakeClock = Clock.fromValue (DateTimeOffset.Parse("2026-05-17"))
    
    myWorkflow
    |> Flow.withClock fakeClock  // Overrides the ambient clock for this flow
    |> Flow.run () CancellationToken.None
```

This gives you the best of both worlds: clean, noise-free business signatures, combined with total testability and deterministic control over side effects.

---

## 4. How This Differs from Traditional DI

In traditional ASP.NET Core Dependency Injection (`IServiceProvider`), **everything** is ambient. Business services and operational services are all dumped into a single, untyped container.

```csharp
// Traditional DI: I don't know what this method actually needs until it crashes at runtime.
public async Task Process(IServiceProvider services) { ... }
```

FsFlow flips this:
- **Business dependencies** must be proven at compile time (Level 1 Records or Level 2 Nominal Capabilities).
- **Operational dependencies** are provided by the engine but remain safely overridable.

This approach—structured composition over normal F#/.NET code—prevents the "service locator anti-pattern" for your domain logic, while keeping the operational noise out of your type signatures.
