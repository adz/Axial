# Node Hosting Example

This Fable application runs a long-lived root Flow under Node. It reads arguments and `process.env`, translates
SIGINT/SIGTERM into coordinated interruption, waits for finalizers, and then assigns `process.exitCode`.

From the repository root, restore the pinned Fable tool once:

```sh
dotnet tool restore
```

Then build and run the example:

```sh
cd examples/Axial.Hosting.Node
npm run run -- first second
```

You can also demonstrate the environment adapter:

```sh
AXIAL_GREETING="Hello from process.env" npm run run
```

Press Ctrl+C. `Node root cleanup finished.` appears before Node exits. SIGINT produces code 130; SIGTERM produces
143; typed failures produce 1; defects produce 2.

The generated JavaScript is written to `dist/` and is intentionally not committed. No `npm install` is required for
this example: Fable's JavaScript runtime modules are emitted alongside the compiled source.
