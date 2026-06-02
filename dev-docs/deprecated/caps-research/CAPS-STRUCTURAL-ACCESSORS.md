# Structural Accessor Pattern
## Anonymous records + SRTP trait accessors

Status: historical research. This was the preferred strict FsFlow model in an earlier design pass, but
`CAPS_RECOMMENDED_MODEL.md` now supersedes it for the 1.0 direction.

This model combines SRTP member constraints with anonymous records to create a low-boilerplate, structural
capability model.

The key correction from earlier drafts is this:

> F# does not support reusable aliases for SRTP member constraints.
>
> Therefore this model does not name requirements as type aliases. It models requirements as inline trait accessor functions.

## Core idea

A capability is represented by a small inline accessor function:

```fsharp
module Cap =
    let inline email (env: ^env) : IEmail =
        (^env : (member Email : IEmail) env)

    let inline db (env: ^env) : IDb =
        (^env : (member Db : IDb) env)

    let inline logger (env: ^env) : ILogger =
        (^env : (member Logger : ILogger) env)
```

Any operation that calls `Cap.email env` now structurally requires an environment with an `Email : IEmail` member.

## Why anonymous records work here

Standard F# anonymous records are exact structural record types. For example, `{| A = 1; B = 2 |}` is not a subtype of `{| A = 1 |}`.

This model does not ask for a specific anonymous record type. It asks for any type with required members. SRTP member constraints make the environment behave like a structural object for this narrow purpose.

```fsharp
let env =
    {| Logger = ConsoleLogger()
       Db = SqlDb("connection-string")
       Email = SmtpEmail()
       ExtraConfig = "extra fields do not break consumers" |}
```

A flow that only uses `Logger` and `Db` can still run with this environment. Extra fields do not matter because the flow is not typed as an exact anonymous record.

## Layering convention

Use three layers:

```text
Cap.email        // raw structural accessor
Email.send       // public effect/domain operation
processOrder     // business workflow
```

Raw accessors should be centralized. Most application code should call meaningful operations rather than repeatedly touching `Cap.*` directly.

## Example

```fsharp
module Cap =
    let inline db (env: ^env) : IDb =
        (^env : (member Db : IDb) env)

    let inline logger (env: ^env) : ILogger =
        (^env : (member Logger : ILogger) env)

    let inline email (env: ^env) : IEmail =
        (^env : (member Email : IEmail) env)

module Log =
    let inline info message = taskFlow {
        let! env = TaskFlow.env
        let logger = Cap.logger env
        return logger.Info message
    }

module Db =
    let inline getOrder orderId = taskFlow {
        let! env = TaskFlow.env
        let db = Cap.db env
        return! db.GetOrder orderId
    }

module Email =
    let inline send toAddr body = taskFlow {
        let! env = TaskFlow.env
        let email = Cap.email env
        return! email.Send(toAddr, body)
    }

let inline processOrder orderId = taskFlow {
    do! Log.info $"Processing {orderId}"
    let! order = Db.getOrder orderId
    do! Email.send order.CustomerEmail "Your order is processing"
    return order
}
```

The compiler infers the combined requirement set from the operations used by `processOrder`. You do not name that set; inference carries it.

## Capability accessors, not requirement aliases

Earlier drafts proposed “Trait Aliases” like this:

```fsharp
type OrderDeps< ^env > = ^env
    when ^env : (member Db : IDb)
    and  ^env : (member Logger : ILogger)
```

This is not valid F#. `when` constraints are not allowed on type aliases in this way, and SRTP member constraints cannot currently be packaged into reusable named requirement sets.

The correct primitive is an accessor function:

```fsharp
let inline db (env: ^env) : IDb =
    (^env : (member Db : IDb) env)
```

## Compiler probe proof

Probe file:

```fsharp
type IEmail =
    abstract Send : string -> unit

type Env =
    { Email : IEmail }

type EmailDeps< ^env > = ^env
    when ^env : (member Email : IEmail)
```

Observed compiler error:

```text
/home/adam/projects/FsFlow/main/probe.fsx(8,5): error FS0010: Unexpected keyword 'when' in interaction. Expected ';', ';;' or other token.
```

Conclusion:

> SRTP requirement aliases are not available. Requirements must be induced by inline functions, methods, or inferred constraints.

## Pros

- **Zero environment boilerplate.** Users can provision dependencies with anonymous records.
- **Compile-time safe.** Missing required members fail at compile time.
- **Automatic composition.** Composed flows accumulate structural requirements through inference.
- **Ad-hoc tests.** Tests can pass small anonymous records with fakes.
- **Narrow dependencies.** Accessors can require exactly one member.

## Cons

- **No reusable requirement aliases.** F# cannot name a set of SRTP member constraints.
- **Verbose inferred types.** IDE tooltips may show expanded SRTP constraints.
- **F#-centric.** C# cannot conveniently produce or consume this style directly.
- **Name convention sensitive.** Everyone must agree on property names such as `Logger`, `Db`, and `Email`.

## Verdict

This remains valuable research, but it is no longer the preferred 1.0 strict model.

The winning idea is not named requirement types. The winning idea is:

> Requirements are induced by capability accessor functions, and environments are provided structurally at the edge.
