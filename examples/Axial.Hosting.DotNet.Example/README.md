# Standalone .NET Hosting Example

This console application uses `DotNetApp.run`, without Generic Host or a dependency-injection container. The adapter
translates Ctrl+C into coordinated Flow interruption and returns a conventional process exit code after finalizers
finish.

Run the default 30-second job:

```sh
dotnet run --project examples/Axial.Hosting.DotNet
```

Or choose the duration:

```sh
dotnet run --project examples/Axial.Hosting.DotNet -- 5
```

Press Ctrl+C while it runs. You should see `Root-scope cleanup finished.` before the process exits with code 130.
Normal completion returns 0, a typed error returns 1, and a defect returns 2.

Use this shape for command-line tools and services that want .NET signal and exit-code behavior but do not otherwise
need `Microsoft.Extensions.Hosting`.
