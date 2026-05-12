---
weight: 20
title: Validation & Results
type: docs
description: Overview of the FsFlow validation stack, from pure checks to structured diagnostics.
---

# Validation & Results

FsFlow provides a unified stack for handling failure, ranging from pure predicate checks to complex, path-aware diagnostics graphs.

The core philosophy is to **check once, lift later**. You write your pure logic using simple tools and then lift them into richer execution contexts (like `Flow` or `Flow`) only when needed.

## The Progression

1.  **[Pure Checks](./checks/)**: Build reusable predicates with the [`Check`]({{< relref "/reference/check/" >}}) module.
2.  **[Result & Validation](./result-validation/)**: Domain logic that either fails fast (`result {}`) or accumulates multiple errors ([`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}})).
3.  **[Guard](./guard/)**: The bridge that allows pure checks and simple sources to fail a flow with a specific domain error.
4.  **[Flow](../start/getting-started/)**: The application boundary where you need dependencies, async work, or interop.

## Why use this stack?

-   **Consistency**: Use the same patterns for simple form validation and complex background job logic.
-   **Testability**: Pure checks are trivial to test in isolation.
-   **Ergonomics**: Computation expressions like `result {}` and `validate {}` make complex logic readable and idiomatic.
-   **Structured Reporting**: Diagnostics graphs preserve the shape of your data, making it easy to report errors back to users or external systems.

## Getting Started

If you are new to FsFlow, start with **[Pure Checks](../checks/)** to see how the smallest building blocks work.

## See it in Action

For a complete, runnable example that demonstrates how these pieces fit together—from nested validation to JSON API error formatting—see the [Diagnostics Example](../patterns/examples/#diagnostics-example).
