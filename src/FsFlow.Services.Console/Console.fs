namespace FsFlow.Services.Console

open System
open FsFlow

/// <summary>Provides synchronous access to standard console I/O.</summary>
type IConsole =
    /// <summary>Reads a line of characters from the standard input stream.</summary>
    abstract ReadLine : unit -> string

    /// <summary>Writes the specified string value, followed by the current line terminator, to the standard output stream.</summary>
    abstract WriteLine : string -> unit

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Console =
    /// <summary>Reads a line through an explicit console service.</summary>
    let readLine<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, string> =
        Service<IConsole>.get()
        |> Flow.map (fun console -> console.ReadLine())

    /// <summary>Writes a line through an explicit console service.</summary>
    let writeLine<'env, 'error when 'env :> IHas<IConsole>>
        (message: string)
        : Flow<'env, 'error, unit> =
        Service<IConsole>.get()
        |> Flow.map (fun console -> console.WriteLine message)

#if !FABLE_COMPILER
    /// <summary>Creates a live console service backed by <see cref="T:System.Console" />.</summary>
    let live : IConsole =
        { new IConsole with
            member _.ReadLine() = Console.ReadLine()
            member _.WriteLine(message) = Console.WriteLine(message) }
#endif

    /// <summary>Builds the live console service as a layer.</summary>
    let layer : Layer<unit, Never, IConsole> =
#if FABLE_COMPILER
        Layer.effect (fun _ _ -> async { return Exit.Failure (Cause.Die (PlatformNotSupportedException("Console services are not supported on Fable."))) })
#else
        Layer.succeed live
#endif
