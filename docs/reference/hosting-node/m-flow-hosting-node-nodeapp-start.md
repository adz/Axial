---
title: "Flow.Hosting.Node.NodeApp.start"
linkTitle: "start"
weight: 2001
---

 Starts a Node application, translating SIGINT and SIGTERM into coordinated stop and publishing its exit code.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.Node.NodeApp.start&#32;<span>describeError&#32;environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `describeError` | <code><span>'error&#32;->&#32;string</span></code> |  |
| `environment` | <code>'env</code> |  |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../app/t-flow-apphandle.md">AppHandle</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> |  |
