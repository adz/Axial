# Examples

This page points to runnable examples so you can see what the Axial package family looks like in code.

## Run The Examples

Main example:

```bash
dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Readme example:

```bash
dotnet run --project examples/Axial.ReadmeExample/Axial.ReadmeExample.fsproj --nologo
```

Full source:

[`examples/Axial.ReadmeExample/Program.fs`](./Axial.ReadmeExample/Program.fs)

Maintenance example:

```bash
dotnet run --project examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj --nologo
```

Playground example:

```bash
dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo
```

NativeAOT probe:

```bash
bash scripts/run-aot-probe.sh
```

## Main Example

The main example in [`examples/Axial.Examples/Program.fs`](./Axial.Examples/Program.fs) shows a small application-shaped set of flows:

- validate configuration with plain `Result`
- build a smaller runtime environment from config
- call a `Task<Result<_,_>>` dependency
- retry transient failures
- apply a timeout
- persist an audit record through a `Task` boundary
- scope an async resource with `use`
- compose smaller flows into a larger config-driven flow with `Flow.localEnv`

Read it in this order:

1. `validateConfig`
2. `fetchResponse`
3. `saveAudit`
4. `program`

## Maintenance Example

The maintenance example in [`examples/Axial.MaintenanceExamples/Program.fs`](./Axial.MaintenanceExamples/Program.fs) is smaller and more focused. It shows:

- how to normalize awkward nested wrapper shapes one layer at a time
- the difference between cold task factories and already-created task values

## Playground Example

The playground example in [`examples/Axial.Playground/Program.fs`](./Axial.Playground/Program.fs) is the quickest way to feel the new surface in practice. It shows:

- plain `Result` validation first
- a small `flow {}` workflow
- projected environment reads through `Flow.read`
- one `.NET` boundary through `taskFlow {}`

## Smallest Docs-First Examples

If you want the smallest possible snippets rather than runnable projects, read:

- [`docs/TINY_EXAMPLES.md`](../docs/TINY_EXAMPLES.md)
- [`docs/FSTOOLKIT_MIGRATION.md`](../docs/FSTOOLKIT_MIGRATION.md)

## Next

If you want the smallest introduction, read [`docs/GETTING_STARTED.md`](../docs/GETTING_STARTED.md).
For task and async boundary shapes, read [`docs/TASK_ASYNC_INTEROP.md`](../docs/TASK_ASYNC_INTEROP.md).
