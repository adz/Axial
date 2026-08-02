---
title: For AI agents
description: High-signal Axial.Data guidance for coding agents.
weight: 100
---

# For AI agents

Use `Axial.Data.Syntax` for source-neutral fixtures and produced output. Construct values with `data`, derive related
values with `patch`, `variants`, or `matrix`, and prove output with `matching`. Use `Data.compare` when the complete tree
is the contract and partial patterns when only selected evidence matters.

Use `Data.render` for human-readable diagnostics and `Data.Json.render` for JSON text. On .NET 8+, convert existing JSON
DOM values with `Data.ofJsonElement` or `Data.ofJsonDocument`. Under Fable, use `Data.ofJsonValue` for a native
`JSON.parse` result. Install `Axial.Schema.Json` and use `Json.parseData` when portable, lossless JSON parsing is needed.

For compact prompt context, load [`/data/llms.txt`](/data/llms.txt).
