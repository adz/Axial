---
title: "Flow.map"
linkTitle: "map"
---

<div class="fsdocs-usage">
<code><span>map&#32;<span>mapper&#32;flow</span></span></code>
</div>

Transforms the successful value of a flow.

## Remarks


 If the source <span class="fsdocs-param-name">flow</span> fails, the <span class="fsdocs-param-name">mapper</span> is not executed.
 The original failure cause is preserved, including typed failures, interruption, and defects.
 Use <code>map</code> for pure value transformations after an effect has succeeded.
 

## Parameters

- `mapper`: <code><span>'value&#32;->&#32;'next</span></code>
  A function of type <code>&#39;value -&gt; &#39;next</code> to transform the successful value.
- `flow`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code>
  The source flow of type <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> to transform.

## Returns

A new <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> with the transformed success value of type <code>&#39;next</code>.

