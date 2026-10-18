# HttpError

`HttpError` is the single typed error of `Axial.HttpClient`. Every request that cannot produce its expected value
fails with one case of this union, so callers handle one error type instead of catching exceptions. Interrupting the
workflow is not a case here: an interrupted request ends with `Cause.Interrupt`, never an `HttpError`.

## The union

| Case | Raised when |
| --- | --- |
| `InvalidRequest message` | The request could not be constructed, for example from a malformed URL. |
| `ConnectionFailed (request, message)` | The connection could not be established or was dropped before a response arrived. |
| `TimedOut (request, timeout)` | The per-request timeout elapsed before the response completed. |
| `Status response` | The response arrived with a status outside the request's expectation; the full transcript is carried. |
| `DecodeFailed (message, response)` | The response body could not be decoded into the requested value; the full transcript is carried. |
| `ResponseTooLarge (request, limit)` | The response body exceeded the request's `maxResponseBytes` limit, so it was not read. |

`ConnectionFailed`, `TimedOut`, and `ResponseTooLarge` carry a redacted request line; `Status` and `DecodeFailed`
carry the full `HttpResponse` transcript so an error handler can log the offending payload without re-fetching it.

## Module helpers

- `HttpError.describe error` formats any case with its redacted request context and a bounded body preview, so a
  single `Flow.mapError HttpError.describe` produces loggable messages.
- `HttpError.tryResponse error` extracts the transcript from the cases that carry one (`Status` and `DecodeFailed`).
- `HttpError.isTransient error` classifies exactly the failures where a retry can help: connection failures,
  timeouts, and 408/429/5xx statuses.
- `HttpError.transientRetry retries baseDelay` builds a `Schedule` that retries only transient errors, with
  exponential backoff from `baseDelay` capped at 65536 times it. `retries` counts retries after the first attempt.

Read the [responses and errors guide](/http/responses-and-errors.html) for a response walkthrough and
[reliability](/http/reliability.html) for timeouts and retries.
