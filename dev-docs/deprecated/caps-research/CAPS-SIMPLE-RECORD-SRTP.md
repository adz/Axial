# Simple Record SRTP Pattern
## Safe, clean, and conservative

This approach combines explicit records with SRTP accessors. Instead of requiring one exact environment type, operations require that the environment has a known property such as `.System`, `.Logger`, or `.Db`.

## Mechanics

1. **Capabilities** are grouped into named records or interfaces.
2. **Environment convention** defines well-known properties.
3. **SRTP accessors** retrieve those properties from any compatible environment.
4. **User-facing ops** remain clean while retaining compile-time checks.

## Example

```fsharp
type SystemCaps =
    { Log : string -> unit
      Sleep : TimeSpan -> Task }

[<AutoOpen>]
module FsFlowSystem =
    let inline log message : TaskFlow< ^env, 'err, unit> =
        fun env ->
            let system = (^env : (member System : SystemCaps) env)
            system.Log message
            Task.FromResult(Ok ())

type AppEnv =
    { System : SystemCaps
      Db : IDbConnection }

let myFlow = taskFlow {
    do! log "Hello World"
    let! env = TaskFlow.env
    return env.Db.ConnectionString
}
```

## Pros

- **Compile-time safe.** Missing properties fail at compile time.
- **Cleaner than Boilerplate.** Accessors can work across many environment types.
- **Better IDE readability than Structural Accessors.** Grouped properties can keep inferred SRTP constraints smaller.
- **Good fallback model.** It is easier to teach than fully structural anonymous-record provisioning.

## Cons

- **Convention-based.** Users must follow property names such as `System`, `Logger`, or `Db`.
- **Less ad-hoc than Structural Accessors.** Users usually define a named environment record.
- **Still uses SRTP.** Library internals and inferred types can still look unfamiliar.
- **Can become coarse.** Grouping many things into `SystemCaps` may hide narrower dependencies.

## Verdict

This is the conservative strict option. It is a strong alternative for teams that prefer named environment records and better IDE readability over the zero-boilerplate ad-hoc style of Structural Accessors.
