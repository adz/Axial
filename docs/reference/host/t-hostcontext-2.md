---
title: "HostContext"
linkTitle: "HostContext<host, appEnv>"
---


 Captures the two-context shape of a task workflow execution:
 host services, application capabilities, and the cancellation token for the current run.
 

## Remarks


 This type is the execution carrier above the adapter layer for the unified
 <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>.
 It separates low-level operational concerns (Host) from high-level domain dependencies
 (AppEnv).
 