---
title: "Capabilities.Http.Http.getString"
linkTitle: "getString"
---

Sends a GET request using the HTTP environment and returns the response body.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Http.Http.getString&#32;<span>url</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `url` | <code>string</code> | The URL to fetch. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;string</span>&gt;</span></code> | A flow that returns the response body as a string. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">content</span> <span class="o">=</span> <span class="id">Http</span><span class="pn">.</span><span class="id">getString</span> <span class="s">&quot;https://example.com&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val content: obj</div>



