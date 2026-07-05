---
title: "Validation.Validation.name"
linkTitle: "name"
weight: 2203
type: docs
---

Prefixes a validation with a named branch.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.name&#32;<span>Validation.Validation.name&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> | The branch name. |
| `validation` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to scope. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A validation whose diagnostics are prefixed with <code>Name name</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span><span class="pn">)</span>
 <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">name</span> <span class="s">&quot;email&quot;</span>
</code></pre>
