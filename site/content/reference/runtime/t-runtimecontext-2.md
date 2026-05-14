---
title: "RuntimeContext"
linkTitle: "RuntimeContext<runtime, env>"
type: docs
---


 Captures the two-context shape of a task workflow execution:
 runtime services, application capabilities, and the cancellation token for the current run.
 

## Remarks


 This type is the execution carrier above the adapter layer for the unified
 <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>.
 It separates low-level operational concerns (Runtime) from high-level domain dependencies
 (Environment).
 
