# Structural Accessors with IServiceProvider Bridges
## Structural business logic, pragmatic DI provisioning

This document describes how to connect strict Structural Accessor business logic to standard .NET dependency injection without making core logic depend directly on `IServiceProvider`.

## Core architecture

1. **Business logic** remains structural and SRTP-based.
2. **Application edge** can construct an environment record from DI.
3. **Bridge code** maps services from `IServiceProvider` into the environment.
4. **Flows run** against that concrete environment.

This preserves honest core logic while keeping app-host integration practical.

## Why a bridge is useful

Structural Accessors logic wants an environment with named members:

```fsharp
let inline processJob data = taskFlow {
    let! env = TaskFlow.env
    let db = Cap.db env
    let logger = Cap.logger env
    logger.Info "Processing job"
    return! db.Process data
}
```

ASP.NET Core and Aspire usually provide dependencies through `IServiceProvider`. The bridge builds the structural environment once at the edge.

## Bridge 1: Reflection

Best for standard JIT-based .NET applications.

```fsharp
type AppEnv =
    { Db : IDb
      Logger : ILogger
      Email : IEmail }

let env = Bridge.create<AppEnv>(serviceProvider)
TaskFlow.run env (processJob data)
```

### Pros

- Zero manual mapping.
- Good for rapid development.
- Works naturally at the application edge.

### Cons

- Not NativeAOT friendly.
- Uses runtime reflection.
- Errors can occur at startup if registrations are missing.

## Bridge 2: Myriad source generation

Best for NativeAOT-safe F# projects.

```fsharp
[<MyriadGenerator("FsFlow.Bridge")>]
type AppEnv =
    { Db : IDb
      Logger : ILogger
      Email : IEmail }

let env = AppEnvBridge.create serviceProvider
TaskFlow.run env (processJob data)
```

Generated code is equivalent to:

```fsharp
module AppEnvBridge =
    let create (sp: IServiceProvider) =
        { Db = sp.GetRequiredService<IDb>()
          Logger = sp.GetRequiredService<ILogger>()
          Email = sp.GetRequiredService<IEmail>() }
```

### Pros

- NativeAOT safe.
- No runtime reflection.
- Good startup behavior.

### Cons

- Requires generator integration.
- Requires a named environment record at the edge.

## Bridge 3: C# source generator

Best for mixed F# / C# systems where the app host is C# but business logic is F#.

```csharp
public partial class FsFlowBridges
{
    [FsFlowBridgeFor(typeof(MyFSharpProject.AppEnv))]
    public static partial MyFSharpProject.AppEnv CreateAppEnv(IServiceProvider sp);
}

app.MapGet("/", (IServiceProvider sp) =>
{
    var env = FsFlowBridges.CreateAppEnv(sp);
    return FsFlow.TaskFlow.Run(env, MyLogic.ProcessJob(data));
});
```

### Pros

- Fits C#-heavy entry points.
- AOT-safe when generated.
- Keeps F# logic free of DI container dependency.

### Cons

- Requires C# generator support.
- Requires a partial C# declaration.

## Relationship to Strict and Pragmatic APIs

- `FsFlow.Strict` uses structural capability accessors.
- `FsFlow.Pragmatic` may use `IServiceProvider` directly for convenience.
- Bridges let strict logic participate in pragmatic app hosts without weakening the core model.

## Verdict

The bridge strategy remains valid after removing Trait Aliases. Bridges operate over concrete edge records, not reusable SRTP requirement aliases, so they are unaffected by the alias limitation.
