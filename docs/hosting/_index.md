---
title: Hosting
linkTitle: Hosting
description: Connect Flow applications to Generic Host, ASP.NET Core, GenHttp, Node, browsers, and service managers.
---

# Hosting

Hosting adapters connect a root `Flow` to the process or server that owns its lifetime. Install only the adapter for
your host, then open its namespace. Core application lifecycle, runtime operations, and platform services stay in the
[core platform chapter](/platforms-and-hosting/index.html).

## Guides

1. [.NET and Generic Host](/hosting/dotnet.html): standalone console processes, Generic Host, dependency injection, Microsoft logging, and fiber-defect logging.
2. [ASP.NET Core](aspnet-core.html): schema-driven HTTP endpoints backed by Flow workflows.
3. [GenHttp](genhttp.html): schema-driven HTTP endpoints on the GenHTTP server.
4. [Node](/hosting/node.html): Node signals, arguments, exit codes, and `process.env`.
5. [Browser](/hosting/browser.html): browser ownership and `AbortSignal` integration.
6. [Running as a service](/hosting/running-as-a-service.html): systemd notification and watchdog integration.

## Reference

Each host package exposes its own API namespace. The generated reference for `Axial.Hosting` and the host adapters is
available from the API links on the individual pages.
