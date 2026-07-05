---
title: "Flow.Deferred"
linkTitle: "Deferred<error, value>"
weight: 1000
type: docs
---


 A one-shot, typed handoff point that can be completed exactly once with a full <a href="https://learn.microsoft.com/dotnet/api/axial.exit-2">Exit</a>.


## Signature

<div class="fsdocs-usage">
<code>type Deferred<'error, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |
| `value` |

## Remarks


 Use <code>Deferred</code> when fibers need to coordinate through Axial Flow outcomes rather than raw
 <a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcompletionsource-1">TaskCompletionSource</a> values. Completion functions are idempotent and
 return <code>true</code> only to the caller that won the completion race.
