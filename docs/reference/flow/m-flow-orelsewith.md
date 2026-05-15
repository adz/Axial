---
title: "Flow.orElseWith"
linkTitle: "orElseWith"
---

<div class="fsdocs-usage">
<code><span>orElseWith&#32;<span>fallback&#32;flow</span></span></code>
</div>

Computes a fallback flow from the typed error when the source flow fails.

## Remarks


 The fallback runs only for expected typed failures represented by <code>Cause.Fail</code>. It does
 not catch interruption or defects. Use this for domain-level recovery, not for swallowing
 cancellation or unexpected exceptions.
 

## Parameters

- `fallback`: <code><span>'error&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code>
- `flow`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code>

