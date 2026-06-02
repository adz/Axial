# FsFlow Capability Plan
## Corrected capability architecture

Status: historical. This document records the corrected structural-accessor plan after removing
invalid SRTP trait aliases. The current recommendation is in `CAPS_SUMMARY.md`, which prefers
nominal micro-capability interfaces for the 1.0 strict capability model.

This document is no longer the current source of truth for the FsFlow capability system.

The key correction from earlier drafts:

> F# does not support reusable aliases for SRTP member constraints.
>
> FsFlow therefore models capabilities as inline accessor functions, not named requirement aliases.

## Vision: Dual API strategy

FsFlow should provide two distinct capability experiences.

### 1. Strict API: `FsFlow.Strict`

**Mental model:** Compile-time honesty.

**Mechanic:** SRTP trait accessors + structural environments.

**Best for:** Core business logic, DDD-style workflows, library code, tests, and any area where dependencies should be statically visible to the compiler.

### 2. Pragmatic API: `FsFlow.Pragmatic`

**Mental model:** Enterprise convenience.

**Mechanic:** `IServiceProvider` access and app-host integration.

**Best for:** Web controllers, minimal APIs, jobs, Aspire/AppHost integration, and teams that prefer DI-container ergonomics at the edge.

## User model

Instead of passing dependencies manually as function arguments, FsFlow treats capability usage as adding requirements to a flow.

In Strict mode, requirements are induced by inline accessor functions:

```fsharp
module Cap =
    let inline logger (env: ^env) : ILogger =
        (^env : (member Logger : ILogger) env)
```

Any operation that calls `Cap.logger env` requires an environment with a `Logger : ILogger` member.

When functions are composed, the compiler infers the combined structural requirements. FsFlow does not name the combined requirement set.

## How to use Strict mode

### Step 1: Open the API

```fsharp
open FsFlow
open FsFlow.Strict
```

### Step 2: Write capability accessors

For built-ins, FsFlow provides these. For app-specific capabilities, users add their own.

```fsharp
module MyApp.Cap =
    let inline db (env: ^env) : IDb =
        (^env : (member Db : IDb) env)

    let inline email (env: ^env) : IEmail =
        (^env : (member Email : IEmail) env)
```

### Step 3: Write meaningful ops

```fsharp
module MyApp.Db =
    let inline fetchOrder orderId = taskFlow {
        let! env = TaskFlow.env
        let db = MyApp.Cap.db env
        return! db.FetchOrder orderId
    }

module MyApp.Email =
    let inline sendOrderStarted address = taskFlow {
        let! env = TaskFlow.env
        let email = MyApp.Cap.email env
        return! email.Send(address, "Your order is processing")
    }
```

### Step 4: Compose business workflows

```fsharp
let inline processOrder orderId = taskFlow {
    do! Log.info $"Processing {orderId}"
    let! order = MyApp.Db.fetchOrder orderId
    do! MyApp.Email.sendOrderStarted order.CustomerEmail
    return order
}
```

### Step 5: Run at the edge

```fsharp
let env =
    {| Logger = ConsoleLogger()
       Db = SqlDb()
       Email = SmtpEmail() |}

TaskFlow.run env (processOrder "ORD-123")
```

## How to add a custom capability

### 1. Define the interface

```fsharp
type IEmail =
    abstract Send : string * string -> Task<unit>
```

### 2. Define the trait accessor

```fsharp
module Cap =
    let inline email (env: ^env) : IEmail =
        (^env : (member Email : IEmail) env)
```

### 3. Define a public op

```fsharp
module Email =
    let inline send address body = taskFlow {
        let! env = TaskFlow.env
        let email = Cap.email env
        return! email.Send(address, body)
    }
```

## Naming and discovery conventions

Use three layers:

```text
Cap.email        raw structural accessor
Email.send       public operation
processOrder     business workflow
```

Recommended module layout:

```text
FsFlow.Strict.Cap.logger
FsFlow.Strict.Cap.clock
FsFlow.Strict.Cap.random

MyApp.Cap.db
MyApp.Cap.email
MyApp.Cap.paymentGateway

MyApp.Orders.submit
MyApp.Users.find
MyApp.Email.sendWelcome
```

The raw `Cap` layer is for centralizing SRTP ugliness. Application code should mostly call meaningful operations.

## Internal implementation guidance

### Accessor style

Prefer simple inline functions:

```fsharp
let inline logger (env: ^env) : ILogger =
    (^env : (member Logger : ILogger) env)
```

### Operation style

Build user-facing operations on top of accessors:

```fsharp
let inline info message = taskFlow {
    let! env = TaskFlow.env
    let logger = Cap.logger env
    return logger.Info message
}
```

### Multi-mode dispatch

If Strict and Pragmatic APIs need shared names, keep the implementations separated by namespace/module where possible.

Avoid making every Strict operation secretly dispatch through `IServiceProvider`. That weakens the mental model.

If overload-based dispatch is used, keep it internal and heavily tested.

## AOT considerations

- Structural Accessor functions are static and AOT-friendly.
- `IServiceProvider` usage can be AOT-friendly when registrations are compatible with NativeAOT.
- Reflection bridges are JIT-only.
- Myriad or source-generator bridges are preferred for AOT.

## Compiler probe proof: no Trait Aliases

Invalid probe:

```fsharp
type IEmail =
    abstract Send : string -> unit

type Env =
    { Email : IEmail }

type EmailDeps< ^env > = ^env
    when ^env : (member Email : IEmail)
```

Observed result:

```text
/home/adam/projects/FsFlow/main/probe.fsx(8,5): error FS0010: Unexpected keyword 'when' in interaction. Expected ';', ';;' or other token.
```

Design consequence:

```text
Requirements are not named as types.
Requirements are induced by accessor functions.
Composition happens through ordinary function composition and CE binding.
```

## Recommendation

Standardize on Structural Accessors for `FsFlow.Strict`, with Simple Record SRTP as a conservative documented fallback and `IServiceProvider` as the basis for `FsFlow.Pragmatic`.

The final position:

> Strict where logic matters. Pragmatic where app hosts demand it.
