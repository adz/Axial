---
title: Fiber
api:
  family: Axial.FiberModule
  sections:
    - id: inspect
      title: Inspect fibers
      order: 10
      members: ["dump", "poll", "Metadata"]
      facets:
        task: [inspect]
    - id: await
      title: Await results
      order: 20
      members: ["join", "await"]
      facets:
        task: [await]
    - id: interrupt
      title: Interrupt fibers
      order: 30
      members: ["requestInterrupt", "interrupt"]
      facets:
        task: [interrupt]
---
# Fiber
