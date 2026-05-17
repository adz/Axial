---
weight: 40
title: Managing Dependencies
description: How FsFlow models dependency boundaries with records, nominal contracts, and host DI.
---

# Managing Dependencies

In FsFlow, the environment (`'env`) represents the "world" your effects need to run. Keeping the environment explicit is the foundation of **Effect Discipline**, allowing you to write testable, refactor-safe, and honest code.

> **Key Architecture Concept:** Before diving into the technical details below, we highly recommend reading [Runtime vs Environment](./runtime-vs-environment/) to understand FsFlow's philosophy of separating operational services from business dependencies.

> **Tutorials Available:** For step-by-step guides on setting up these patterns, see the [Tutorials section](../../tutorials/).

FsFlow provides three primary ways to model and access dependencies, ranging from direct pragmatism to strict architectural honesty.

## The Three Levels of Dependency Access

| Level | Name | Best For | Requirement | Main API |
| :--- | :--- | :--- | :--- | :--- |
| **1** | **Direct** | Local logic, small apps | Plain Record | `Flow.read` |
| **2** | **Honest** | Shared logic, large apps | `IHas<'T>` | `Flow.service` |
| **3** | **Pragmatic**| Edge integration, Host DI | `IServiceProvider` | `Flow.inject` |

---

## 1. Level 1: Records (The Pragmatic Way)

Plain F# records are the default choice for most logic. They are easy to construct, mock, and understand.

```fsharp
type ApiDeps = { Orders: IOrderRepo; Email: IEmailSender }

let workflow : Flow<ApiDeps, string, unit> =
    flow {
        let! email = Flow.read _.Email
        do! email.SendConfirmation()
    }
```

**Why use it?** It requires zero extra boilerplate (no interfaces needed) and keeps the boundary concrete.

---

## 2. Level 2: Nominal Capabilities (The Honest Way)

As your application grows, you may want function signatures that clearly advertise every service they need. This is the "Nominal Capability" model.

```fsharp
type IOrderRepo = abstract member Save : Order -> unit
type IHasOrders = inherit IHas<IOrderRepo>

let saveOrder order : Flow<#IHasOrders, _, _> =
    flow {
        let! repo = Flow.service<IOrderRepo>()
        repo.Save order
    }
```

**Why use it?** It behavior like "programming to interfaces," but for effects. It makes the effect’s requirements visible in the type and gives refactoring a boundary the compiler can check.

---

## 3. Level 3: Dependency Injection (The Edge Way)

At the application host boundary (e.g., ASP.NET), you can lean into the .NET ecosystem using `IServiceProvider`.

```fsharp
let handleRequest = flow {
    let! db = Flow.inject<MyDbContext>()
    // ...
}
```

**Why use it?** It allows you to use FsFlow inside existing .NET apps with minimal ceremony.

---

## How The Pieces Fit Together

The FsFlow dependency story follows these principles:

- **Implicit Infrastructure**: Operational services like `Clock`, `Log`, and `Random` stay in the ambient runtime to avoid cluttering type signatures. They are overridable via `Flow.withClock`, etc.
- **Explicit Domain**: Business services (DB, API, Email) are kept explicit in the `'env` type using either records (Level 1) or `IHas<'T>` (Level 2).
- **The Honest Bridge**: Use `Flow.inject` or manual mapping at the host edge to satisfy strict Level 2 requirements from a Level 3 DI container.
- **Progressive Honesty**: Start with `Flow.inject` for fast prototyping, then refactor to `Flow.service` for static verification.

This architecture provides a flexible path from rapid prototyping to strict architectural honesty, while maintaining compatibility with the broader .NET ecosystem.
