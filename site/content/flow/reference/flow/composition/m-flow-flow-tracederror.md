---
title: "Flow.tracedError"
linkTitle: "tracedError"
weight: 2323
type: docs
---

Attaches diagnostic trace text to any failure cause of the flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.tracedError&#32;<span>trace&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `trace` | <code>string</code> | The diagnostic trace text, typically an operation or boundary name. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow whose failures carry the trace annotation. |

## Remarks


 On failure the cause is wrapped in <code>Cause.Traced</code>, so retries, parallel composition, and
 telemetry (<code>Cause.prettyPrint</code>, the <code>axial.flow.cause</code> span tag) can show where in the
 workflow the failure passed through. Successful values are untouched, and the typed error is not
 changed — only the cause tree grows a trace node.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">loadUser</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">tracedError</span> <span class="s">&quot;billing.load-user&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
