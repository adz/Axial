---
title: "Flow.provideLayer"
linkTitle: "provideLayer"
---

<div class="fsdocs-usage">
<code><span>provideLayer&#32;<span>layer&#32;flow</span></span></code>
</div>

Runs a layer flow first, then runs a downstream flow with the layer&#39;s output as its environment.

## Remarks


 Use this at composition boundaries where one flow builds the environment needed by another
 flow. Ordinary workflow code should usually consume an environment directly with
 <code>Flow.read</code>; <code>provideLayer</code> is for deriving or provisioning an environment before a
 downstream workflow starts.
 

## Parameters

- `layer`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'input,&#32;'error,&#32;'environment</span>&gt;</span></code>
- `flow`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'environment,&#32;'error,&#32;'value</span>&gt;</span></code>

