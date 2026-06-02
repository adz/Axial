# IServiceProvider Capability Pattern
## Low ceremony, high .NET interop, runtime safety only

This approach uses `IServiceProvider` as the environment. It fits ASP.NET Core, Aspire, generic host, and conventional .NET dependency injection extremely well, but it cannot provide compile-time proof that dependencies are registered.

## Mechanics

1. **Environment** is always `IServiceProvider`.
2. **Accessor** is a generic `service<'T>` operation.
3. **User-facing ops** call `service<'T>` internally.

## Example

```fsharp
let inline service<'T> : TaskFlow<IServiceProvider, FsFlowError, 'T> =
    fun sp -> task {
        match sp.GetService(typeof<'T>) with
        | null -> return Error (MissingService typeof<'T>)
        | :? 'T as service -> return Ok service
        | _ -> return Error (InvalidService typeof<'T>)
    }

let log message = taskFlow {
    let! logger = service<ILogger>
    return logger.Log message
}

let myFlow = taskFlow {
    do! log "Starting..."
    let! db = service<IDbConnection>
    return! db.QueryAsync()
}
```

## Pros

- **Excellent ecosystem interop.** Works naturally with ASP.NET Core, Aspire, and generic host.
- **Very clean syntax.** No environment record is required.
- **Zero capability wiring.** The DI container is the source of truth.
- **Good for entry points.** Controllers, handlers, jobs, and endpoints often already have DI available.

## Cons

- **Runtime errors.** Missing service registration is not caught by the F# type checker.
- **Opaque dependencies.** The type of the flow does not reveal what services it needs.
- **Weak environment type.** `IServiceProvider` is effectively a `Type -> obj option` map.
- **Less honest core logic.** Business logic can accidentally reach for arbitrary services.

## Verdict

CAPS2 should not be the primary strict FsFlow model. It is, however, valuable as a pragmatic integration layer for app edges and DI-heavy .NET applications.
