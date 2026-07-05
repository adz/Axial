---
title: "Validation.Schema.ParsedInput.mapErrors"
linkTitle: "mapErrors"
weight: 2104
type: docs
---

Maps a failed parse&#39;s errors to a domain or application error type, preserving the raw input and paths.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.ParsedInput.mapErrors&#32;<span>mapper&#32;parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> | A function of type <code>&#39;error -&gt; &#39;nextError</code>. |
| `parsed` | <code><span><a href="t-validation-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;'error</span>&gt;</span></code> | The parsed input to map. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;'nextError</span>&gt;</span></code> | A <a href="t-validation-schema-parsedinput.md">ParsedInput</a> with the same input and model, and mapped errors. |

## Remarks


 Use this at the boundary between schema input parsing and application code, where <code>SchemaError</code> (or any
 other interpreter error type) should become the caller&#39;s own domain/application error type before flowing
 further into the system. A successful parse is returned unchanged apart from its error type.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">domainParsed</span> <span class="o">=</span> <span class="id">parsed</span> <span class="o">|&gt;</span> <span class="id">ParsedInput</span><span class="pn">.</span><span class="id">mapErrors</span> <span class="id">SignupError</span><span class="pn">.</span><span class="id">ofSchemaError</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val domainParsed: obj</div>
