---
weight: 20
title: Validation & Results
type: docs
description: Overview of the FsFlow validation stack, from pure checks to structured diagnostics.
---


FsFlow provides a unified stack for handling failure, ranging from pure predicate checks to complex, path-aware diagnostics graphs.

The core philosophy is to **check once, lift later**. You write pure logic with simple tools, then lift it into richer execution contexts only when needed.

## The Progression

1.  **[Check and Take](./checks/)**: Choose between yes/no predicates and value-returning checks with [`Check`]({{< relref "/reference/check/" >}}) and [`Take`]({{< relref "/reference/take/" >}}).
2.  **[Result & Validation](./result-validation/)**: Domain logic that either fails fast (`result {}`) or accumulates multiple errors ([`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}})).
3.  **[BindError](./bind-error/)**: A flow bind-site marker for assigning or mapping a source error before binding.
4.  **[Flow](../start/getting-started/)**: The application boundary where you need dependencies, async work, or interop.

## Why use this stack?

-   **Consistency**: Use the same patterns for simple form validation and complex background job logic.
-   **Testability**: Pure checks are trivial to test in isolation.
-   **Ergonomics**: Computation expressions like `result {}` and `validate {}` make complex logic readable and idiomatic.
-   **Structured Reporting**: Diagnostics graphs preserve the shape of your data, making it easy to report errors back to users or external systems.

## Getting Started

If you are new to FsFlow, start with **[Check and Take](./checks/)** to see how the smallest building blocks work.

## See it in Action

For a complete, runnable example that demonstrates how these pieces fit together—from nested validation to JSON API error formatting—see the [Diagnostics Example](../patterns/examples/#diagnostics-example).
