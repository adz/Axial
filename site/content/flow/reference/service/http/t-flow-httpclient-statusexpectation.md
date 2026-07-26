---
title: "Flow.HttpClient.StatusExpectation"
linkTitle: "StatusExpectation"
weight: 1002
type: docs
---

 Decides which response status codes count as success for a request.

## Signature

<div class="fsdocs-usage">
<code>type StatusExpectation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Success` |  Any status in the 200-299 range succeeds. This is the default. |
| `Statuses` |  Only the listed statuses succeed. |
| `Any` |  Every status succeeds; the caller inspects the status explicitly. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow.HttpClient/Http.fs#L50-50)
