---
title: "Schema.Http.GenHttp.Request.query"
linkTitle: "query"
weight: 2101
type: docs
---

Schema-parses the query string.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.Request.query&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> | The schema that interprets the complete query input. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span><span><a href="t-schema-http-genhttp-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-genhttp-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;'model</span>&gt;</span></code> | An endpoint Flow that succeeds with the trusted model. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">search</span> <span class="o">=</span> <span class="id">Request</span><span class="pn">.</span><span class="id">query</span> <span class="id">Search</span><span class="pn">.</span><span class="id">schema</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val search: obj</div>
