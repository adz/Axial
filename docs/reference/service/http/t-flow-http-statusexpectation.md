---
title: "Flow.Http.StatusExpectation"
linkTitle: "StatusExpectation"
weight: 1002
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
