---
title: Advanced
description: Publishing services from a package, and owning resources whose lifetime is the runtime's.
---

# Advanced

Topics that most applications never need, and that make the earlier sections harder to read if they sit inline.

None of these are required to use Axial. [Dependencies](/dependencies/index.html) covers the model you will
actually write day to day; this section is for the places that model runs out, or scales past one application.

## In this section

1. [Providing services from a package](reusable-packages.html) — authoring a library whose callers you will never
   see, and the contract shape that makes it composable.
2. [Scopes and resources](scopes-and-resources.html) — deterministic cleanup for resources acquired during
   provisioning or execution.
3. [Tutorial: Creating reusable services](custom-services.html) — define your own named service contract, the way
   the built-in services are defined, instead of tying a dependency to one record field name.
