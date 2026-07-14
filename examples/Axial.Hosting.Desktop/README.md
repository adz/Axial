# Desktop-Owned Lifecycle Example

Desktop frameworks already own startup, windows, dispatching, and shutdown. This example therefore uses portable
`App.start` rather than inventing an Axial adapter for one UI toolkit. A console `ReadLine` stands in for a window's
closing event so the lifecycle is runnable on every development machine.

Run it:

```sh
dotnet run --project examples/Axial.Hosting.Desktop
```

Press Enter. The simulated close handler awaits `running.Stop()`, the root finalizer saves state, and only then does
the framework proceed to exit.

In Avalonia, WPF, WinUI, MAUI, or another desktop framework, keep the same ownership:

1. Call `App.start environment application` after framework startup.
2. Retain the returned `AppHandle` in the application shell.
3. In the asynchronous closing path, await `Stop()` before approving shutdown.
4. Do not synchronously block the UI dispatcher on `Completion`.
