---
title: "Flow.PlatformService.EnvironmentVariableError"
linkTitle: "EnvironmentVariableError"
weight: 1005
---

Describes a meaningful environment-variable failure.

## Signature

<div class="fsdocs-usage">
<code>type EnvironmentVariableError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MissingVariable` | The requested variable was not present. |
| `InvalidVariable` | The requested variable existed but could not be parsed. |
