/// Scenario 2 — resilient HTTP call with a retry budget.
///
/// Fetch an exchange rate: retry only transient transport failures with exponential delay, stop
/// after three attempts, and turn a two-second deadline into RateError.TimedOut. Malformed
/// successful responses and programming defects are never retried.
///
/// Made visible by the type: the error taxonomy (transient vs terminal) and the IHttp requirement.
/// Enforced by the runtime: retry applies only to typed failures the predicate accepts; the
/// timeout interrupts sleeps and the running request.
/// Still the application's responsibility: the operation must be safe to repeat.
module Axial.Flow.Comparisons.RetryBudget

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.HttpClient

// --- Shared domain -----------------------------------------------------------------

type Rate = { Pair: string; Value: decimal }

type RateError =
    | Transport of string
    | TimedOut
    | Malformed of body: string

/// What HttpClient throws for DNS/socket/5xx-style trouble in the ordinary version.
exception TransientTransportException of message: string

let parseRate (pair: string) (body: string) : Result<Rate, string> =
    match Decimal.TryParse body with
    | true, value -> Ok { Pair = pair; Value = value }
    | false, _ -> Error body

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    /// Task<Result<Rate, RateError>> around a raw send function (standing in for HttpClient).
    /// Cancellation, timeout, transport exceptions, and Result errors all meet in one recursive
    /// function, and the catch must be careful not to swallow defects.
    let fetchRate
        (send: CancellationToken -> Task<string>)
        (pair: string)
        (maxAttempts: int)
        (baseDelay: TimeSpan)
        (timeout: TimeSpan)
        (cancellationToken: CancellationToken)
        : Task<Result<Rate, RateError>> =
        task {
            use timeoutSource = CancellationTokenSource.CreateLinkedTokenSource cancellationToken
            timeoutSource.CancelAfter timeout

            let mutable attempt = 1
            let mutable outcome: Result<Rate, RateError> option = None

            while outcome = None do
                try
                    let! body = send timeoutSource.Token

                    match parseRate pair body with
                    | Ok rate -> outcome <- Some(Ok rate)
                    | Error body -> outcome <- Some(Error(Malformed body)) // must NOT retry: same bytes come back
                with
                | :? OperationCanceledException when cancellationToken.IsCancellationRequested ->
                    raise (OperationCanceledException cancellationToken)
                | :? OperationCanceledException ->
                    outcome <- Some(Error TimedOut)
                | TransientTransportException message when attempt < maxAttempts ->
                    // An overly broad `with _ ->` here would also retry NullReferenceException.
                    do! Task.Delay(baseDelay * float (pown 2 (attempt - 1)), timeoutSource.Token)
                    attempt <- attempt + 1
                | TransientTransportException message ->
                    outcome <- Some(Error(Transport message))

            return outcome.Value
        }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    /// Flow<'env, RateError, Rate> where 'env :> IHas<IHttp>.
    ///
    /// The request itself is one declaration; retry and timeout are policies applied to the cold
    /// workflow from outside. The retry predicate selects typed transient failures only — defects
    /// and interruption are structurally out of its reach.
    let fetchRate (url: string) (pair: string) : Flow<'env, RateError, Rate> when 'env :> IHas<IHttp> =
        let request =
            flow {
                let! response = Http.get url |> Http.send |> Bind.mapError (HttpError.describe >> Transport)
                let! rate = parseRate pair (Response.text response) |> Bind.mapError Malformed
                return rate
            }

        let transientOnly: RetryPolicy<RateError> =
            { MaxAttempts = 3
              Delay = fun attempt -> TimeSpan.FromMilliseconds(50.0 * float (pown 2 (attempt - 1)))
              ShouldRetry =
                function
                | Transport _ -> true
                | TimedOut
                | Malformed _ -> false }

        request
        |> Flow.Runtime.retry transientOnly
        |> Flow.Runtime.timeout (TimeSpan.FromSeconds 2.0) TimedOut
