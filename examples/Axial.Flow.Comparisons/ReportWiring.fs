/// Scenario 5 — application wiring that cannot omit a capability.
///
/// A scheduled report needs a clock, a filesystem, a console, and a report store; production and
/// tests supply different implementations of the same environment.
///
/// Made visible by the type: the report cannot run until every declared capability is supplied,
/// and reading DateTimeOffset.UtcNow directly is impossible without changing the signature.
/// Enforced by the runtime: layers build the environment once at the edge; missing services are
/// compile errors, not startup surprises.
/// Still the application's responsibility: the type proves presence and shape, not that an
/// implementation is configured correctly.
module Axial.Flow.Comparisons.ReportWiring

open System
open System.IO
open Axial.Flow
open Axial.Flow.Console
open Axial.Flow.FileSystem
open Axial.Flow.PlatformService

// --- Shared domain -----------------------------------------------------------------

type ReportError = | StoreRejected of string

type IReportStore =
    abstract Save : name: string * body: string -> Result<unit, string>

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    /// Constructor injection: four parameters that every caller and every test must thread. The
    /// temptation this code fights daily is `DateTimeOffset.UtcNow` and `File.ReadAllText` — the
    /// compiler does not care if someone gives in, and a service-locator variant would defer the
    /// missing-registration failure to runtime.
    let writeDailyReport
        (clock: unit -> DateTimeOffset)
        (readSource: string -> string)
        (store: IReportStore)
        (log: string -> unit)
        (sourcePath: string)
        : Result<string, ReportError> =
        let today = (clock ()).UtcDateTime.ToString "yyyy-MM-dd"
        let name = $"daily-{today}.txt"
        let body = readSource sourcePath

        match store.Save(name, body) with
        | Error reason -> Error(StoreRejected reason)
        | Ok() ->
            log $"wrote {name}"
            Ok name

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    /// The full capability set, declared once. Production and tests build the same record from
    /// different layers; business code below can name only what it uses.
    type ReportEnv =
        { Clock: IClock
          FileSystem: IFileSystem
          Console: IConsole
          Store: IReportStore }

        interface IHas<IClock> with
            member this.Service = this.Clock

        interface IHas<IFileSystem> with
            member this.Service = this.FileSystem

        interface IHas<IConsole> with
            member this.Service = this.Console

        interface IHas<IReportStore> with
            member this.Service = this.Store

    /// Flow<ReportEnv, ReportError, string>
    let writeDailyReport (sourcePath: string) : Flow<ReportEnv, ReportError, string> =
        flow {
            let! now = Clock.now
            let name = $"""daily-{now.UtcDateTime.ToString "yyyy-MM-dd"}.txt"""

            let! body =
                FileSystem.readAllText sourcePath
                |> Flow.mapError (FileSystemError.describe >> StoreRejected)

            let! store = Flow.read _.Store
            do! store.Save(name, body) |> Flow.fromResult |> Flow.mapError StoreRejected

            do! Console.writeLine $"wrote {name}"
            return name
        }

    /// Production wiring: live platform services, merged into the one environment record at the
    /// application edge. Tests build the same record from Clock.fromValue and in-memory doubles.
    let liveLayer (store: IReportStore) : Layer<unit, Never, ReportEnv> =
        Layer.merge (Layer.merge Clock.layer FileSystem.layer) Console.layer
        |> Layer.map (fun ((clock, fileSystem), console) ->
            { Clock = clock
              FileSystem = fileSystem
              Console = console
              Store = store })
