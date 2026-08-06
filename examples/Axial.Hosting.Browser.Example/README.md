# Browser Hosting Example

This Fable application mounts one root Flow in a browser and connects an `AbortController` to coordinated Axial
shutdown. The page owns application lifetime; the hosting adapter does not guess based on unload or visibility events.

Restore the repository's pinned Fable tool and compile the example:

```sh
dotnet tool restore
cd examples/Axial.Hosting.Browser
npm run build
```

Serve the directory over HTTP using any static server. Python is sufficient:

```sh
python3 -m http.server 8080
```

Open <http://localhost:8080/> and click **Stop application**. The button aborts the signal, the root Flow is
interrupted, its finalizer updates the page, and `Completion` reports the structured outcome.

The generated JavaScript is written to `dist/` and is intentionally not committed. In React, Elmish, a custom
element, or another UI framework, the component/root owns the same two operations: mount the `AppHandle`, then request
and await stop when ownership ends.
