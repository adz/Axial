---
title: "Flow.read"
linkTitle: "read"
type: docs
---

<div class="fsdocs-usage">
<code><span>read&#32;<span>projection</span></span></code>
</div>

Projects one value from the current environment.

## Remarks


 This is the primary way to access app dependencies, configuration, or request metadata stored
 in <code>env</code>. The projection runs only when the flow is executed, so constructing the flow is
 still pure and side-effect free. Prefer small projections over passing a large environment
 deeper into reusable helpers.
 

## Parameters

- `projection`: <code><span>'env&#32;->&#32;'value</span></code>
  A function that extracts a value from the environment.

## Returns

A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> containing the projected value.

