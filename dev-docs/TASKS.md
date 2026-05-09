# FsFlow Tasks

This file is the active queue for `scripts/ralph-loop-tasks.sh`.
Keep completed work out of this file.
Keep settled design decisions in `dev-docs/decisions/`.
Keep live product and architecture direction in `dev-docs/PLAN.md`.

The CAPS implementation backlog is intentionally linear so each package can land with its own tests,
examples, and docs.

Shared conventions for the package work:

- follow existing FsFlow naming where possible, but expose capability functions with the first
  letter lower-cased and without `Async` suffixes
- auto-pass `CancellationToken` for operations that need it instead of making every call site thread
  it manually
- wrap expected, app-important failures in the typed result model
- let defects, programmer errors, and unexpected provider failures bubble to the nearest higher
  boundary where they can be caught and translated
- keep each package as its own NuGet so users only pull in the capabilities they explicitly choose

1. [x] Implement `FsFlow.Caps.Core` as the smallest shared capability package: provide clock,
   random, GUID, and environment-variable capabilities with live and test implementations, typed
   errors for meaningful absence or invalid values, and docs/examples showing the sync-first surface
   shape.
2. [ ] Implement `FsFlow.Caps.Context` as the execution-context package: provide request id,
   correlation id, tenant id, current user, locale/culture, request metadata, and any request-scoped
   flags needed by app code, with simple live/test context providers and docs/examples that show the
   context flowing into logging, auditing, and authorization.
3. [ ] Implement `FsFlow.Caps.Observability` as the FsFlow-owned observability abstraction package:
   provide logging, spans/traces, metrics, baggage/annotations, and structured log entry support;
   keep the core surface independent of provider libraries; document which observability failures are
   returned as results and which are treated as higher-level defects.
4. [ ] Implement `FsFlow.Caps.Observability.MicrosoftLogging` as the Microsoft logging adapter:
   bridge `ILogger` and `ILoggerFactory` into the FsFlow observability abstractions, support logger
   categories and scopes, and include tests/examples/docs that prove the adapter preserves the
   expected level mapping and structured state.
5. [ ] Implement `FsFlow.Caps.Observability.OpenTelemetry` as the OpenTelemetry adapter: bridge
   `ActivitySource`, `Meter`, and the provider types into the FsFlow observability abstractions,
   support span creation, metric emission, and export-friendly configuration, and add tests/examples
   docs for the adapter boundary.
6. [ ] Implement `FsFlow.Caps.Console` as the console IO package: provide `console.write`,
   `console.writeLine`, `console.readLine`, `console.readKey`, and any other boring console helpers
   that stay synchronous at the API surface while auto-threading cancellation where applicable; add a
   captured-output/scripted-input test runtime and runnable examples.
7. [ ] Implement `FsFlow.Caps.FileSystem` as the file and directory package: provide
   `file.readAllText`, `file.writeAllText`, `file.exists`, `file.openRead`, `file.copy`, `file.move`,
   `file.delete`, `directory.exists`, `directory.create`, `directory.enumerateFiles`,
   `directory.delete`, `path.combine`, and temp-file/temp-directory helpers; return typed errors for
   not-found, unauthorized, invalid-path, already-exists, IO, and cancellation cases; add fake/live
   file-system support plus docs/examples.
8. [ ] Implement `FsFlow.Caps.Http` as the HTTP package: provide `http.send`, `http.getString`,
   `http.getJson`, `http.postJson`, and the other basic request helpers that mirror the existing
   FsFlow style without async suffixes; auto-pass cancellation, wrap expected HTTP failures such as
   invalid URI, timeout, non-success status, and decode errors in the result model, and keep the
   package testable with an in-memory handler or fake client.
9. [ ] Implement `FsFlow.Caps.Process` as the process execution package: provide
   `process.run`, `process.start`, `process.capture`, standard working-directory/environment/stdio
   configuration, and exit-code/timeout handling; wrap expected process failures such as executable
   not found, start failure, non-zero exit, timeout, and cancellation in typed errors, with tests,
   examples, and docs for both live and fake execution.
10. [ ] Implement `FsFlow.Caps.ServiceProvider` as the DI bridge package: provide
    `service.get`, `service.tryGet`, `options.get`, and keyed-service lookup helpers against
    `IServiceProvider`; keep the dependency on `Microsoft.Extensions.DependencyInjection.Abstractions`
    isolated here; document how the package maps container lookups into FsFlow results and when
    missing services should be treated as expected failures versus higher-level defects.
11. [ ] Add package-specific tests, runnable examples, and reference docs for every CAPS package:
    create/refresh the example projects, make the docs entry pages point at the package surface, and
    keep the examples aligned with the sync-first, lower-cased capability style.
12. [ ] Validate the full CAPS package story end to end: run the test suite, regenerate API docs, and
    build the site; fix any broken links, stale examples, or reference gaps before considering the
    package set complete.
