/// Scenario 4 — a scoped temporary workspace.
///
/// Create a temporary directory, perform several fallible steps inside it, and remove it exactly
/// once even when the work fails, throws, or is interrupted.
///
/// Made visible by the type: acquisition and use are one construct with one signature.
/// Enforced by the runtime: the finalizer runs on success, typed failure, defect, and
/// interruption; a finalizer failure is preserved in the cause instead of replacing the outcome.
/// Still the application's responsibility: choosing the right scope and keeping finalizers
/// idempotent and non-blocking.
module Axial.Flow.Comparisons.ScopedWorkspace

open System
open System.IO
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.FileSystem

// --- Shared domain -----------------------------------------------------------------

type ImportError =
    | UnreadableBatch of string
    | NoRecords

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    exception ImportFailed of ImportError

    /// The correct shape: create, then immediately enter try/finally.
    let importBatch (records: string list) : Task<int> =
        task {
            let workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")
            Directory.CreateDirectory workspace |> ignore

            try
                if List.isEmpty records then
                    return raise (ImportFailed NoRecords)
                else
                    File.WriteAllLines(Path.Combine(workspace, "batch.csv"), records)
                    return records.Length
            finally
                Directory.Delete(workspace, recursive = true)
        }

    /// The frequent leak: construction succeeds, then setup fails BEFORE ownership transfers
    /// into the try/finally. The directory survives the exception.
    let importBatchLeaky (records: string list) : Task<int> =
        task {
            let workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")
            Directory.CreateDirectory workspace |> ignore

            // Setup between acquisition and try/finally: throws with no owner to clean up.
            if List.isEmpty records then
                raise (ImportFailed NoRecords)

            try
                File.WriteAllLines(Path.Combine(workspace, "batch.csv"), records)
                return records.Length
            finally
                Directory.Delete(workspace, recursive = true)
        }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    type WorkspaceEnv =
        { FileSystem: IFileSystem }

        interface IHas<IFileSystem> with
            member this.Service = this.FileSystem

    /// Flow<WorkspaceEnv, ImportError, int>
    ///
    /// acquireReleaseWith owns the directory from the instant acquisition succeeds. There is no
    /// gap where setup code can fail without an owner, and no way to add a step outside the
    /// finalizer's reach.
    let importBatch (records: string list) : Flow<WorkspaceEnv, ImportError, int> =
        let acquire: Flow<WorkspaceEnv, ImportError, string> =
            flow {
                let workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")
                do! FileSystem.createDirectory workspace |> Flow.mapError (FileSystemError.describe >> UnreadableBatch)
                return workspace
            }

        Flow.acquireReleaseWith
            acquire
            (fun workspace _ -> Task.Run(fun () -> Directory.Delete(workspace, recursive = true)))
            (fun workspace ->
                flow {
                    // Every step here — including this gate that fails before any file exists —
                    // is inside the resource's lifetime.
                    do! if List.isEmpty records then Flow.fail NoRecords else Flow.succeed ()

                    do!
                        FileSystem.writeAllLines (Path.Combine(workspace, "batch.csv")) records
                        |> Flow.mapError (FileSystemError.describe >> UnreadableBatch)

                    return records.Length
                })
