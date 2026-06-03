---
title: "Cause"
weight: 40
---

This page shows the `Cause<'error>` type, which distinguishes between expected domain failures, unexpected technical defects (exceptions), and administrative interruptions (cancellation). Understanding the cause allows FsFlow's runtime to make smart decisions about retries, cleanup, and observability.

## Core type

- [`Cause`](./t-cause.md):
 Represents the cause of a failed workflow.


## Module functions

- [`Cause.map`](./m-cause-map.md): Transforms the error value of a failure cause using the provided function.
