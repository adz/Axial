---
title: Advanced
description: Publishing services from a package, and owning resources whose lifetime is the runtime's.
---

# Advanced

Two topics that most applications never need, and that make the earlier sections harder to read if they sit inline.

Neither is required to use Axial. [Dependencies](/dependencies/index.html) covers the model you will actually write
day to day; this section is for the two places that model runs out.

## In this section

1. [Providing services from a package](reusable-packages.html) — authoring a library whose callers you will never
   see, and the contract shape that makes it composable.
2. [Scopes and resources](scopes-and-resources.html) — deterministic cleanup for resources acquired during
   provisioning or execution.
