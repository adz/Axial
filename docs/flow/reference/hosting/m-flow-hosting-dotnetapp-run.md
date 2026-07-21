---
title: "Flow.Hosting.DotNetApp.run"
linkTitle: "run"
weight: 2000
---

 Runs a standalone application, translating Ctrl+C into coordinated stop and returning a process exit code.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.DotNetApp.run&#32;<span>describeError&#32;environment&#32;application</span></span></code>
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
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;int&gt;</span></code> |  |
