---
weight: 40
title: Managing Dependencies
description: Introduction to dependency management in FsFlow.
---

# Managing Dependencies

In FsFlow, a workflow is not just a function; it is a description of work that needs an **environment** to run. 

As your application grows, you might be tempted to create a single "God Object" (like `AppEnv`) that contains every service in your system: Database, Logger, API Gateways, and more. However, if every small workflow depends on this massive record, your code becomes hard to test and tightly coupled. You'd have to mock the entire universe just to test a simple calculation.

FsFlow provides two primary architectural styles to help you "slice" your dependencies so that each workflow only asks for what it actually needs.

## The Two Styles

### 1. The Record Pattern (Environment Slicing)
This is the simplest way to start. You define your environment as a standard F# record. Workflows "read" or "project" the fields they need from this record. It is perfect for local helpers, internal logic, and smaller applications where record types are stable.

**Read the Deep Dive:** [Environment Slicing](./env-slicing/)

### 2. The CAPS Pattern (Decoupled Capabilities)
This style decouples your workflows from specific record types using interface-based contracts. A workflow says "I need a clock," and it doesn't care if that clock comes from a massive app runtime or a tiny test object. It is the best choice for public APIs, shared libraries, and large-scale systems.

**Read the Deep Dive:** [Capabilities (CAPS)](./capabilities/)

---

## Relationship to Architecture

These two patterns support different **Architectural Styles**:

- **Style 1: The Booted App**: Usually uses a single large record (Record Pattern) for simplicity.
- **Style 2: Parameters + Context**: Uses parameters for core logic and a thin record (Record Pattern) for request context.
- **Style 3: .NET DI**: Often uses the CAPS Pattern to bridge .NET services into the FsFlow execution model.

For more details on these structures, see [Architectural Styles](./architectural-styles/).

---

## Comparison at a Glance

| Feature | Record Pattern | CAPS Pattern |
| :--- | :--- | :--- |
| **Typical Use** | Local helpers, small apps | Public APIs, shared libraries |
| **Coupling** | Bound to a specific record type | Bound to an interface/contract |
| **Simplicity** | High (Standard F#) | Medium (Interface-based) |
| **Flexibility** | Moderate | Very High |

---

## Which style should I use?

**Start with the Record Pattern.** It is the most idiomatic way to write F# and requires the least amount of boilerplate. 

Move to the **CAPS Pattern** when:
- You are building a library that others will consume.
- You find yourself writing complex "shim" records just to run a workflow in a different context.
- You want your workflows to read like a set of required "Capabilities" rather than a set of record fields.

## Shared Helpers: The Capability Module

Regardless of which style you choose, FsFlow provides a `Capability` module that works across both. These helpers are polymorphic, meaning they work in `flow {}`, `flow {}`, and `flow {}` without needing specific module prefixes.

```fsharp
let log message =
    flow {
        let! logger = Capability.service _.Logger
        logger.Log message
    }
```

Learn more about these polymorphic helpers in the [Environment Slicing](./env-slicing/#the-capability-module) guide.
