namespace Axial.Flow.Console

open System
open System.IO
open System.Text
open Axial.Flow

/// <summary>Provides explicit access to standard console and terminal I/O.</summary>
type IConsole =
    abstract In : TextReader
    abstract Out : TextWriter
    abstract Error : TextWriter
    abstract InputEncoding : Encoding with get, set
    abstract OutputEncoding : Encoding with get, set
    abstract IsInputRedirected : bool
    abstract IsOutputRedirected : bool
    abstract IsErrorRedirected : bool
    abstract KeyAvailable : bool
    abstract Read : unit -> int
    abstract ReadLine : unit -> string
    abstract ReadKey : intercept: bool -> ConsoleKeyInfo
    abstract Write : value: string -> unit
    abstract WriteLine : value: string -> unit
    abstract WriteError : value: string -> unit
    abstract WriteErrorLine : value: string -> unit
    abstract OpenStandardInput : unit -> Stream
    abstract OpenStandardOutput : unit -> Stream
    abstract OpenStandardError : unit -> Stream
    abstract Clear : unit -> unit
    abstract Beep : unit -> unit
    abstract ResetColor : unit -> unit
    abstract ForegroundColor : ConsoleColor with get, set
    abstract BackgroundColor : ConsoleColor with get, set
    abstract CursorLeft : int with get, set
    abstract CursorTop : int with get, set
    abstract CursorVisible : bool with get, set
    abstract SetCursorPosition : left: int * top: int -> unit
    abstract Title : string with get, set
    abstract TreatControlCAsInput : bool with get, set

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Console =
    let private withService<'env, 'error, 'value when 'env :> IHas<IConsole>> operation : Flow<'env, 'error, 'value> =
        Service<IConsole>.get() |> Flow.map operation

    let input<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, TextReader> = withService _.In
    let output<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, TextWriter> = withService _.Out
    let error<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, TextWriter> = withService _.Error
    let inputEncoding<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, Encoding> = withService _.InputEncoding
    let setInputEncoding<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.InputEncoding <- value)
    let outputEncoding<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, Encoding> = withService _.OutputEncoding
    let setOutputEncoding<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.OutputEncoding <- value)
    let isInputRedirected<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.IsInputRedirected
    let isOutputRedirected<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.IsOutputRedirected
    let isErrorRedirected<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.IsErrorRedirected
    let keyAvailable<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.KeyAvailable
    let read<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, int> = withService (fun console -> console.Read())
    let readLine<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, string> = withService (fun console -> console.ReadLine())
    let readKey<'env, 'error when 'env :> IHas<IConsole>> intercept : Flow<'env, 'error, ConsoleKeyInfo> = withService (fun console -> console.ReadKey intercept)
    let write<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.Write value)
    let writeLine<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.WriteLine value)
    let writeError<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.WriteError value)
    let writeErrorLine<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.WriteErrorLine value)
    let openStandardInput<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, Stream> = withService (fun console -> console.OpenStandardInput())
    let openStandardOutput<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, Stream> = withService (fun console -> console.OpenStandardOutput())
    let openStandardError<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, Stream> = withService (fun console -> console.OpenStandardError())
    let clear<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, unit> = withService (fun console -> console.Clear())
    let beep<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, unit> = withService (fun console -> console.Beep())
    let resetColor<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, unit> = withService (fun console -> console.ResetColor())
    let foregroundColor<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, ConsoleColor> = withService _.ForegroundColor
    let setForegroundColor<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.ForegroundColor <- value)
    let backgroundColor<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, ConsoleColor> = withService _.BackgroundColor
    let setBackgroundColor<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.BackgroundColor <- value)
    let cursorPosition<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, int * int> = withService (fun console -> console.CursorLeft, console.CursorTop)
    let setCursorPosition<'env, 'error when 'env :> IHas<IConsole>> left top : Flow<'env, 'error, unit> = withService (fun console -> console.SetCursorPosition(left, top))
    let cursorVisible<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.CursorVisible
    let setCursorVisible<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.CursorVisible <- value)
    let title<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, string> = withService _.Title
    let setTitle<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.Title <- value)
    let treatControlCAsInput<'env, 'error when 'env :> IHas<IConsole>> : Flow<'env, 'error, bool> = withService _.TreatControlCAsInput
    let setTreatControlCAsInput<'env, 'error when 'env :> IHas<IConsole>> value : Flow<'env, 'error, unit> = withService (fun console -> console.TreatControlCAsInput <- value)

#if !FABLE_COMPILER
    /// <summary>Creates a live console service backed by <see cref="T:System.Console" />.</summary>
    let live : IConsole =
        { new IConsole with
            member _.In = System.Console.In
            member _.Out = System.Console.Out
            member _.Error = System.Console.Error
            member _.InputEncoding with get () = System.Console.InputEncoding and set value = System.Console.InputEncoding <- value
            member _.OutputEncoding with get () = System.Console.OutputEncoding and set value = System.Console.OutputEncoding <- value
            member _.IsInputRedirected = System.Console.IsInputRedirected
            member _.IsOutputRedirected = System.Console.IsOutputRedirected
            member _.IsErrorRedirected = System.Console.IsErrorRedirected
            member _.KeyAvailable = System.Console.KeyAvailable
            member _.Read() = System.Console.Read()
            member _.ReadLine() = System.Console.ReadLine()
            member _.ReadKey(intercept) = System.Console.ReadKey intercept
            member _.Write(value) = System.Console.Write value
            member _.WriteLine(value) = System.Console.WriteLine value
            member _.WriteError(value) = System.Console.Error.Write value
            member _.WriteErrorLine(value) = System.Console.Error.WriteLine value
            member _.OpenStandardInput() = System.Console.OpenStandardInput()
            member _.OpenStandardOutput() = System.Console.OpenStandardOutput()
            member _.OpenStandardError() = System.Console.OpenStandardError()
            member _.Clear() = System.Console.Clear()
            member _.Beep() = System.Console.Beep()
            member _.ResetColor() = System.Console.ResetColor()
            member _.ForegroundColor with get () = System.Console.ForegroundColor and set value = System.Console.ForegroundColor <- value
            member _.BackgroundColor with get () = System.Console.BackgroundColor and set value = System.Console.BackgroundColor <- value
            member _.CursorLeft with get () = System.Console.CursorLeft and set value = System.Console.CursorLeft <- value
            member _.CursorTop with get () = System.Console.CursorTop and set value = System.Console.CursorTop <- value
            member _.CursorVisible with get () = System.Console.CursorVisible and set value = System.Console.CursorVisible <- value
            member _.SetCursorPosition(left, top) = System.Console.SetCursorPosition(left, top)
            member _.Title with get () = System.Console.Title and set value = System.Console.Title <- value
            member _.TreatControlCAsInput with get () = System.Console.TreatControlCAsInput and set value = System.Console.TreatControlCAsInput <- value }
#endif

    /// <summary>Builds the live console service as a layer.</summary>
    let layer : Layer<unit, Never, IConsole> =
#if FABLE_COMPILER
        Layer.fromAsync (fun _ _ -> async { return Exit.Failure (Cause.Die (PlatformNotSupportedException("Console services are not supported on Fable."))) })
#else
        Layer.succeed live
#endif
