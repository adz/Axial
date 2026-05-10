---
title: "FsFlow.Flow.interrupt"
linkTitle: "interrupt`"
type: docs
---

Signals a fiber to stop and waits for it to finish its cleanup.



## Parameters

- `fiber`: The fiber to interrupt.

## Returns

A flow that completes with the fiber's final outcome after interruption.

