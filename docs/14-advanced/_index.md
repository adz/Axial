---
title: Advanced
description: Publishing reusable services and enforcing application effect boundaries.
---

# Advanced

Most applications only need the model in [Dependencies](/dependencies/index.html). These topics cover publishing
services for other applications and enforcing effect boundaries.

## In this section

1. [Providing services from a package](reusable-packages.html): authoring a library whose callers you will never
   see, and the contract shape that makes it composable.
2. [Tutorial: Creating reusable services](custom-services.html): define your own named service contract, the way
   the built-in services are defined, instead of tying a dependency to one record field name.
3. [Write an effect-boundary analyzer](writing-effect-analyzers.html): detect direct access to an effect behind
   your service contract and run the check from a consumer's build.
