namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks
open System.ComponentModel

module private ResultFlow =
    let map
        (mapper: 'value -> 'next)
        (result: Result<'value, 'error>)
        : Result<'next, 'error> =
        Result.map mapper result

    let bind
        (binder: 'value -> Result<'next, 'error>)
        (result: Result<'value, 'error>)
        : Result<'next, 'error> =
        Result.bind binder result

    let mapError
        (mapper: 'error -> 'nextError)
        (result: Result<'value, 'error>)
        : Result<'value, 'nextError> =
        Result.mapError mapper result

[<EditorBrowsable(EditorBrowsableState.Never)>]
module OptionFlow =
    let toUnitResult (value: 'value option) : Result<'value, unit> =
        match value with
        | Some innerValue -> Ok innerValue
        | None -> Error()

    let toUnitResultValueOption (value: 'value voption) : Result<'value, unit> =
        match value with
        | ValueSome innerValue -> Ok innerValue
        | ValueNone -> Error()

    let toResult (error: 'error) (value: 'value option) : Result<'value, 'error> =
        match value with
        | Some innerValue -> Ok innerValue
        | None -> Error error

    let toResultValueOption (error: 'error) (value: 'value voption) : Result<'value, 'error> =
        match value with
        | ValueSome innerValue -> Ok innerValue
        | ValueNone -> Error error

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Execution =
    let mapBoth
        (onSuccess: 'value -> 'next)
        (onFailure: Cause<'error> -> Cause<'nextError>)
        (effect: Execution<'value, 'error>)
        : Execution<'next, 'nextError> =
#if FABLE_COMPILER
        async {
            let! exit = effect
            return Exit.mapBoth onSuccess onFailure exit
        }
#else
        ValueTask<Exit<'next, 'nextError>>(
            task {
                let! exit = effect
                return Exit.mapBoth onSuccess onFailure exit
            })
#endif

    let causeOfException (exn: exn) : Cause<'error> =
        if exn :? OperationCanceledException then
            Cause.Interrupt
        else
            Cause.Die exn

    let ofExit (exit: Exit<'value, 'error>) : Execution<'value, 'error> =
#if FABLE_COMPILER
        async.Return exit
#else
        ValueTask<Exit<'value, 'error>>(exit)
#endif

    let ofValue (value: 'value) : Execution<'value, 'error> =
        ofExit (Exit.Success value)

    let ofCause (cause: Cause<'error>) : Execution<'value, 'error> =
        ofExit (Exit.Failure cause)

    let ofError (error: 'error) : Execution<'value, 'error> =
        ofCause (Cause.Fail error)

    let ofDie (exn: exn) : Execution<'value, 'error> =
        ofCause (Cause.Die exn)

    let ofException (exn: exn) : Execution<'value, 'error> =
        ofCause (causeOfException exn)

    let ofInterrupt () : Execution<'value, 'error> =
        ofCause Cause.Interrupt

    let ofResult (result: Result<'value, 'error>) : Execution<'value, 'error> =
        match result with
        | Ok value -> ofValue value
        | Error error -> ofError error

    let fold
        (onSuccess: 'value -> Execution<'next, 'nextError>)
        (onFailure: Cause<'error> -> Execution<'next, 'nextError>)
        (effect: Execution<'value, 'error>)
        : Execution<'next, 'nextError> =
#if FABLE_COMPILER
        async {
            let! exit = effect
            match exit with
            | Exit.Success value -> return! onSuccess value
            | Exit.Failure cause -> return! onFailure cause
        }
#else
        ValueTask<Exit<'next, 'nextError>>(
            task {
                let! exit = effect

                match exit with
                | Exit.Success value -> return! onSuccess value
                | Exit.Failure cause -> return! onFailure cause
            })
#endif

    let map
        (mapper: 'value -> 'next)
        (effect: Execution<'value, 'error>)
        : Execution<'next, 'error> =
        fold (mapper >> ofValue) ofCause effect

    let bind
        (binder: 'value -> Execution<'next, 'error>)
        (effect: Execution<'value, 'error>)
        : Execution<'next, 'error> =
        fold binder ofCause effect

    let mapError
        (mapper: 'error -> 'nextError)
        (effect: Execution<'value, 'error>)
        : Execution<'next, 'nextError> =
        fold ofValue (Cause.map mapper >> ofCause) effect

module internal InternalCombinatorCore =
    let mapWith
        (mapOutcome: (Exit<'value, 'error> -> Exit<'next, 'error>) -> 'operation -> 'nextOperation)
        (mapper: 'value -> 'next)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Exit.map mapper)

    let bindWith
        (bindOutcome: 'operation -> ('value -> 'nextOperation) -> (Cause<'error> -> 'nextOperation) -> 'nextOperation)
        (continueWith: 'context -> 'value -> 'nextOperation)
        (failWith: Cause<'error> -> 'nextOperation)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> bindOutcome (operation context) (continueWith context) failWith

    let mapErrorWith
        (mapOutcome: (Exit<'value, 'error> -> Exit<'value, 'nextError>) -> 'operation -> 'nextOperation)
        (mapper: 'error -> 'nextError)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Exit.mapError mapper)

    let localEnvWith
        (run: 'innerEnvironment -> 'flow -> 'operation)
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: 'flow)
        : 'outerEnvironment -> 'operation =
        fun environment -> flow |> run (mapping environment)

    let delayWith
        (run: 'environment -> 'flow -> 'operation)
        (factory: unit -> 'flow)
        : 'environment -> 'operation =
        fun environment -> factory () |> run environment
