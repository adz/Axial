---
title: "Flow.localEnv"
linkTitle: "localEnv"
---

<div class="fsdocs-usage">
<code><span>localEnv&#32;<span>mapping&#32;flow</span></span></code>
</div>

Runs a flow against an environment derived from the outer environment.

## Remarks


 Use this to embed a smaller workflow inside a larger application environment without changing
 the smaller workflow&#39;s type. The mapping is applied at execution time. This is useful for
 preserving narrow helper signatures while still running everything from one app boundary.
 

## Parameters

- `mapping`: <code><span>'outerEnvironment&#32;->&#32;'innerEnvironment</span></code>
- `flow`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'innerEnvironment,&#32;'error,&#32;'value</span>&gt;</span></code>

