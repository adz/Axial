# .NET Generic Host Example

This example registers one root Flow as an `IHostedService`. Generic Host owns process lifetime and dependency
injection; Axial owns the root Flow scope and its finalizers.

Run it from the repository root:

```sh
dotnet run --project examples/Axial.Hosting.GenericHost
```

Press Ctrl+C. Generic Host publishes `ApplicationStopping`, the Axial adapter requests `App.Stop()`, and
`StopAsync` waits for `Root cleanup finished` before host shutdown completes.

The environment factory is intentionally at the composition edge. It resolves `IMessageSource` and `ILoggerFactory`
from `IServiceProvider`, then constructs the explicit `AppEnv` used by the workflow. Domain code never receives the
service provider.

For a finite root Flow, the default registration calls `StopApplication()` when it completes. Use
`Hosting.addAppWith { StopHostOnCompletion = false }` when another hosted service owns host completion.
