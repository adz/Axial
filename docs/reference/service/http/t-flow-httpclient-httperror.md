---
title: "Flow.HttpClient.HttpError"
linkTitle: "HttpError"
weight: 1006
---

 A recoverable HTTP transport, timeout, status, or decoding failure.

## Signature

<div class="fsdocs-usage">
<code>type HttpError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `InvalidRequest` |  The request could not be constructed, for example from a malformed URL. |
| `ConnectionFailed` |  The connection could not be established or was dropped before a response arrived. |
| `TimedOut` |  The per-request timeout elapsed before the response completed. |
| `Canceled` |  The workflow was canceled while the request was in flight. |
| `Status` |  The response arrived with a status outside the request's expectation. |
| `DecodeFailed` |  The response body could not be decoded into the requested value. |
