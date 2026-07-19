/// Scenario 3 — parallel dashboard fan-out.
///
/// Load account, recent orders, and recommendations concurrently. Account and orders are
/// mandatory; recommendations fall back to an empty list on their typed failure. When a
/// mandatory branch fails, the still-running sibling is interrupted rather than left racing.
///
/// Made visible by the type: the concurrency policy sits at the composition point (zipPar), and
/// the recovered branch is the only one whose error disappears from the signature.
/// Enforced by the runtime: zipPar interrupts the loser when a branch fails and merges causes
/// when both settle unsuccessfully.
/// Still the application's responsibility: adapters must honour cancellation, and concurrent
/// external writes still need their own consistency design.
module Axial.Flow.Comparisons.DashboardFanOut

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Telemetry

// --- Shared domain -----------------------------------------------------------------

type Account = { Id: string; Name: string }
type Order = { Id: string; Total: decimal }
type Recommendation = { Sku: string }

type PageError =
    | AccountUnavailable of string
    | OrdersUnavailable of string

type DashboardPage =
    { Account: Account
      Orders: Order list
      Recommendations: Recommendation list }

type IAccounts =
    abstract Load : id: string * cancellationToken: CancellationToken -> Task<Result<Account, string>>

type IOrders =
    abstract Recent : accountId: string * cancellationToken: CancellationToken -> Task<Result<Order list, string>>

type IRecommendations =
    abstract For : accountId: string * cancellationToken: CancellationToken -> Task<Result<Recommendation list, string>>

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    exception PageFailed of PageError

    /// Task.WhenAll plus a linked cancellation source, exception inspection, and hand-written
    /// fallback logic. Cancelling the sibling when a mandatory branch fails is manual, and two
    /// simultaneous failures surface as whichever exception WhenAll happens to publish first.
    let loadPage
        (accounts: IAccounts)
        (orders: IOrders)
        (recommendations: IRecommendations)
        (accountId: string)
        (cancellationToken: CancellationToken)
        : Task<DashboardPage> =
        task {
            use linked = CancellationTokenSource.CreateLinkedTokenSource cancellationToken

            let accountTask =
                task {
                    let! result = accounts.Load(accountId, linked.Token)

                    match result with
                    | Ok account -> return account
                    | Error reason ->
                        linked.Cancel() // remember to stop the siblings by hand
                        return raise (PageFailed(AccountUnavailable reason))
                }

            let ordersTask =
                task {
                    let! result = orders.Recent(accountId, linked.Token)

                    match result with
                    | Ok recent -> return recent
                    | Error reason ->
                        linked.Cancel()
                        return raise (PageFailed(OrdersUnavailable reason))
                }

            let recommendationsTask =
                task {
                    try
                        let! result = recommendations.For(accountId, linked.Token)

                        match result with
                        | Ok items -> return items
                        | Error _ -> return [] // fallback branch
                    with :? OperationCanceledException ->
                        return []
                }

            // WhenAll surfaces one exception; the second failure is in .Exception.InnerExceptions,
            // which most call sites never look at.
            let! _ = Task.WhenAll [| accountTask :> Task; ordersTask :> Task; recommendationsTask :> Task |]

            let! account = accountTask
            let! recent = ordersTask
            let! recommended = recommendationsTask
            return { Account = account; Orders = recent; Recommendations = recommended }
        }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    type DashboardEnv =
        { Accounts: IAccounts
          Orders: IOrders
          Recommendations: IRecommendations }

    /// Flow<DashboardEnv, PageError, DashboardPage>
    ///
    /// Concurrency policy is visible at the composition point: two mandatory branches combined
    /// with zipPar, one optional branch recovered with orElse before it joins. Interrupting the
    /// slow sibling on failure is the runtime's job, not a linked-token convention.
    let loadPage (accountId: string) : Flow<DashboardEnv, PageError, DashboardPage> =
        let account: Flow<DashboardEnv, PageError, Account> =
            flow {
                let! accounts = Flow.read _.Accounts
                let! token = Flow.Runtime.cancellationToken
                let! loaded = accounts.Load(accountId, token) |> Bind.mapError AccountUnavailable
                return loaded
            }

        let recent: Flow<DashboardEnv, PageError, Order list> =
            flow {
                let! orders = Flow.read _.Orders
                let! token = Flow.Runtime.cancellationToken
                let! loaded = orders.Recent(accountId, token) |> Bind.mapError OrdersUnavailable
                return loaded
            }

        let recommended: Flow<DashboardEnv, PageError, Recommendation list> =
            flow {
                let! recommendations = Flow.read _.Recommendations
                let! token = Flow.Runtime.cancellationToken
                let! loaded = recommendations.For(accountId, token) |> Bind.mapError OrdersUnavailable
                return loaded
            }
            |> Flow.orElse (Flow.succeed []) // recover ONLY this branch; mandatory errors stay typed

        Flow.zipPar (Flow.zipPar account recent) recommended
        |> Flow.map (fun ((account, recent), recommended) ->
            { Account = account; Orders = recent; Recommendations = recommended })
        |> Activity.trace "dashboard.load"

    /// First-success semantics are a different contract, so they get a different composition:
    /// race two equivalent sources and take whichever answers first; the loser is interrupted.
    let loadAccountFromFastestReplica
        (primary: Flow<DashboardEnv, PageError, Account>)
        (replica: Flow<DashboardEnv, PageError, Account>)
        : Flow<DashboardEnv, PageError, Account> =
        Flow.race primary replica
