# Axial examples

Start with:

```bash
dotnet run --project examples/Axial.ReadmeExample/Axial.ReadmeExample.fsproj --nologo
dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo
dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Hosting examples are independent applications with local instructions:

- `Axial.App.Example` — portable finite application using `App.run`
- `Axial.Hosting.DotNet.Example` — standalone .NET process lifecycle
- `Axial.Hosting.GenericHost.Example` — Microsoft Generic Host
- `Axial.Hosting.Desktop.Example` — desktop-owned lifecycle
- `Axial.Hosting.Node.Example` — Fable on Node
- `Axial.Hosting.Browser.Example` — Fable in a browser

`Axial.Comparisons` implements equivalent scenarios with ordinary Task-based code and Axial. `Axial.AotProbe` is the NativeAOT smoke application.

The ASP.NET Core and GenHTTP examples plus `Axial.ReferenceApp` are temporary cross-product examples. They require published `Reified.*` packages and are intentionally outside `Axial.slnx` until those packages exist.
