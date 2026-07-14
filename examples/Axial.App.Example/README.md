# Portable App Example

This is the smallest application-shaped Axial program. It uses `App.run` directly and has no dependency on
`Axial.Flow.Hosting`, dependency injection, or a platform host.

Run it from the repository root:

```sh
dotnet run --project examples/Axial.App.Example -- Ada
```

Try omitting `Ada` to see a typed startup failure and a non-zero exit code.

`application` is a normal `Flow<string array, AppError, string>`. `App.run` owns the root scope, waits for cleanup,
and returns its structured `Exit`. The outermost `main` function chooses console rendering and the process code.

Use this shape for finite CLI tools, scripts, tests, and applications whose surrounding framework already supplies
everything needed to construct the Flow environment.
