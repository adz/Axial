---
weight: 60
title: Patterns
description: Common architectural patterns, troubleshooting, and benchmarks.
type: docs
---


Adopting a library is easy until you hit the questions the tutorials skip: why does this type error look like that,
what does this cost on a hot path, does it survive NativeAOT trimming or a Fable build, and what does a real end-to-end
boundary actually look like? This section collects those answers — troubleshooting guides for the type errors you will
actually see, measured benchmarks instead of performance folklore, deployment notes for AOT/trimming/Fable, and
runnable real-world examples.
