---
title: "Result Builder"
weight: 70
type: docs
---

This page shows the `result { }` computation expression for ordinary fail-fast `Result` workflows. It is the smallest effect in FsFlow's stack: no environment, no async boundary, and no runtime services. Use it for pure domain transformations where the first error should stop the computation. If the same logic later needs dependency access, async work, cancellation, logging, or typed execution outcomes, lift it into `Flow` without changing the underlying error model.

## Builder

- [`result`](./p-result.md): 
 The fail-fast <code>result { }</code> computation expression.
 

