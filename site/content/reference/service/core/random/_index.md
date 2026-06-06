---
title: "Random"
type: docs
---

This page shows the `Core.Random` helpers for reading values from an explicit random-number service.

- [`Core.Random.next`](./m-core-random-next.md): Reads a non-negative random integer from an explicit random-number service.
- [`Core.Random.nextMax`](./m-core-random-nextmax.md): Reads a random integer less than the supplied maximum from an explicit random-number service.
- [`Core.Random.nextInt`](./m-core-random-nextint.md): Reads a random integer from an explicit random-number service.
- [`Core.Random.nextDouble`](./m-core-random-nextdouble.md): Reads a random floating-point value from an explicit random-number service.
- [`Core.Random.nextBytes`](./m-core-random-nextbytes.md): Fills a byte buffer through an explicit random-number service.
- [`Core.Random.bytes`](./m-core-random-bytes.md): Creates a byte array filled through an explicit random-number service.
- [`Core.Random.live`](./p-core-random-live.md): Creates a live random-number generator backed by <a href="https://learn.microsoft.com/dotnet/api/system.random">Random</a>.
- [`Core.Random.layer`](./p-core-random-layer.md): Builds the live random-number generator as a layer.
- [`Core.Random.fromValue`](./m-core-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.
- [`Core.Random.fromFixed`](./m-core-random-fromfixed.md): Creates a deterministic random generator from fixed integer, double, and byte values.
