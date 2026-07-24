---
title: "Choice"
---

`Choice` functions try alternative refinement functions.

- [`Refined.Choice.orElse`](./m-refined-choice-orelse.md): Tries the left parser first, then the right parser, mapping either success into your output type.
- [`Refined.Choice.tryAny`](./m-refined-choice-tryany.md): Tries parser strategies in order and returns the first success.
