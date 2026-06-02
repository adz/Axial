namespace FsFlow.Services.FileSystem

open System
open System.IO
open FsFlow

/// <summary>Provides synchronous access to file system operations.</summary>
type IFileSystem =
    /// <summary>Opens a text file, reads all lines of the file into a string, and then closes the file.</summary>
    abstract ReadAllText : path: string -> string

    /// <summary>Creates a new file, writes the specified string to the file, and then closes the file. If the target file already exists, it is overwritten.</summary>
    abstract WriteAllText : path: string * contents: string -> unit

    /// <summary>Determines whether the specified file exists.</summary>
    abstract Exists : path: string -> bool

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FileSystem =
    /// <summary>Reads all text through an explicit file-system service.</summary>
    let readAllText<'env, 'error when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, 'error, string> =
        Service<IFileSystem>.get()
        |> Flow.map (fun fileSystem -> fileSystem.ReadAllText(path))

    /// <summary>Writes all text through an explicit file-system service.</summary>
    let writeAllText<'env, 'error when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: string)
        : Flow<'env, 'error, unit> =
        Service<IFileSystem>.get()
        |> Flow.map (fun fileSystem -> fileSystem.WriteAllText(path, contents))

    /// <summary>Checks file existence through an explicit file-system service.</summary>
    let exists<'env, 'error when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, 'error, bool> =
        Service<IFileSystem>.get()
        |> Flow.map (fun fileSystem -> fileSystem.Exists(path))

#if !FABLE_COMPILER
    /// <summary>Creates a live file-system service backed by <see cref="T:System.IO.File" />.</summary>
    let live : IFileSystem =
        { new IFileSystem with
            member _.ReadAllText(path) = File.ReadAllText(path)
            member _.WriteAllText(path, contents) = File.WriteAllText(path, contents)
            member _.Exists(path) = File.Exists(path) }
#endif

    /// <summary>Builds the live file-system service as a layer.</summary>
    let layer : Layer<unit, Never, IFileSystem> =
#if FABLE_COMPILER
        Layer.effect (fun _ _ -> async { return Exit.Failure (Cause.Die (PlatformNotSupportedException("File-system services are not supported on Fable."))) })
#else
        Layer.succeed live
#endif
