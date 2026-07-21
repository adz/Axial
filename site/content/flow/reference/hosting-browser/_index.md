---
title: "Browser Hosting"
weight: 500
type: docs
---

This page shows the JavaScript-only `Axial.Flow.Hosting.Browser` surface. `BrowserApp.mount` gives a UI owner an `AppHandle`; `startWithSignal` connects a structural browser `AbortSignal` to coordinated stop. The package deliberately does not treat page visibility or unload events as dependable application shutdown. See the [browser hosting guide](/flow/hosting/browser/) for complete setup.

## Browser application

- [`Flow.Hosting.Browser.AbortSignal`](./t-flow-hosting-browser-abortsignal.md): A structural binding for the browser and JavaScript <code>AbortSignal</code> contract.
- [`Flow.Hosting.Browser.BrowserApp.mount`](./m-flow-hosting-browser-browserapp-mount.md): Starts an application owned explicitly by the calling UI or browser module.
- [`Flow.Hosting.Browser.BrowserApp.startWithSignal`](./m-flow-hosting-browser-browserapp-startwithsignal.md): Starts an application and translates an AbortSignal into coordinated application stop.
