---
title: "Flow.Process.OutputTarget"
linkTitle: "OutputTarget"
weight: 1003
---

 Receives bytes from a process topology. Capture limits are measured in bytes.

## Signature

<div class="fsdocs-usage">
<code>type OutputTarget</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Capture` |  |
| `CaptureTail` |  |
| `Console` |  |
| `Inherit` |  |
| `Discard` |  |
| `File` |  |
| `AppendFile` |  |
| `Callback` |  |
| `Sink` |  |
| `Tee` |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow.Process/Process.fs#L36-36)
