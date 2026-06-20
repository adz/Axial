namespace Axial.Flow.Process

open System.Diagnostics
open System.Threading.Tasks
open Axial.Flow
open Axial.Result
open Axial.Validation

/// <summary>Represents the outcome of an external process execution.</summary>
type ProcessResult =
    {
        /// <summary>The exit code returned by the process.</summary>
        ExitCode: int
        /// <summary>The standard output stream of the process.</summary>
        StdOut: string
        /// <summary>The standard error stream of the process.</summary>
        StdErr: string
    }

/// <summary>Provides asynchronous access to external process execution.</summary>
type IProcess =
    /// <summary>Executes an external process and returns its result asynchronously.</summary>
    abstract Execute : fileName: string * arguments: string -> Task<ProcessResult>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Process =
    /// <summary>Executes a process through an explicit process service and returns the result.</summary>
    let execute<'env, 'error when 'env :> IHas<IProcess>>
        (fileName: string)
        (arguments: string)
        : Flow<'env, 'error, ProcessResult> =
        flow {
            let! processService = Service<IProcess>.get()
            return! processService.Execute(fileName, arguments)
        }

#if !FABLE_COMPILER
    /// <summary>Creates a live process service backed by <see cref="T:System.Diagnostics.Process" />.</summary>
    let live : IProcess =
        { new IProcess with
            member _.Execute(fileName, arguments) =
                task {
                    let startInfo = ProcessStartInfo(fileName, arguments)
                    startInfo.RedirectStandardOutput <- true
                    startInfo.RedirectStandardError <- true
                    startInfo.UseShellExecute <- false
                    startInfo.CreateNoWindow <- true

                    use proc = new Process()
                    proc.StartInfo <- startInfo
                    proc.Start() |> ignore

                    let! stdOut = proc.StandardOutput.ReadToEndAsync()
                    let! stdErr = proc.StandardError.ReadToEndAsync()
                    #if NETSTANDARD2_1
                    proc.WaitForExit()
                    #else
                    do! proc.WaitForExitAsync()
                    #endif

                    return
                        {
                            ExitCode = proc.ExitCode
                            StdOut = stdOut
                            StdErr = stdErr
                        }
                } }

    /// <summary>Builds the live process service as a layer.</summary>
    let layer : Layer<unit, Never, IProcess> =
        Layer.succeed live
#endif
