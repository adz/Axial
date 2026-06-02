# Boilerplate Capability Pattern
## Compile-time safe, explicit, high ceremony

This approach uses ordinary F# records for environment definition and small capability slices to group related services or functions. It is the most conventional and explicit F# approach, but it requires significant manual wiring.

## Mechanics

1. **Contracts** are defined as interfaces or records of functions.
2. **Slices** group a single capability or small capability area.
3. **Environment** is a concrete record containing all slices.
4. **Accessors / ops** manually pluck services from the environment.

## Example

```fsharp
type IClock =
    abstract UtcNow : unit -> DateTimeOffset
    abstract Sleep : TimeSpan -> Task

type ClockCap =
    { Clock : IClock }

type LoggerCap =
    { Logger : ILogger }

type AppEnv =
    { Clock : ClockCap
      Logger : LoggerCap }

module Clock =
    let currentUtc : TaskFlow<AppEnv, 'err, DateTimeOffset> =
        fun env ->
            Task.FromResult(Ok (env.Clock.Clock.UtcNow()))

    let sleep (duration: TimeSpan) : TaskFlow<AppEnv, 'err, unit> =
        fun env -> task {
            do! env.Clock.Clock.Sleep duration
            return Ok ()
        }

let myFlow = taskFlow {
    let! start = Clock.currentUtc
    do! Clock.sleep (TimeSpan.FromSeconds 1.0)
    return start
}
```

## Pros

- **100% compile-time safe.** Missing dependencies fail at compile time.
- **Explicit and unsurprising.** It uses standard F# records and functions.
- **Easy testing.** Tests can construct an `AppEnv` with mocks or fakes.
- **Good IDE readability.** Types are named and ordinary.

## Cons

- **High ceremony.** Every capability needs records, slices, and accessors.
- **Rigid environment type.** Generic reuse across different environment records requires more indirection or SRTP.
- **Potential module-prefix noise.** Usage often becomes `Clock.currentUtc`, `Logger.info`, etc.

## Verdict

CAPS1 is a useful baseline and fallback for teams that value maximum explicitness over ergonomics. It is safe, simple, and boring, but too boilerplate-heavy to be the primary FsFlow experience.
