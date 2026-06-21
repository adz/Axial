namespace Axial.Benchmarks

open System.Threading
open System.Threading.Tasks

type CandidateValueTaskFlow<'env, 'error, 'value> =
    private
    | CandidateValueTaskFlow of ('env -> CancellationToken -> ValueTask<Result<'value, 'error>>)

[<RequireQualifiedAccess>]
module CandidateValueTaskFlow =
    let run
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (CandidateValueTaskFlow operation: CandidateValueTaskFlow<'env, 'error, 'value>)
        : ValueTask<Result<'value, 'error>> =
        operation environment cancellationToken

    let succeed (value: 'value) : CandidateValueTaskFlow<'env, 'error, 'value> =
        CandidateValueTaskFlow(fun _ _ -> ValueTask<Result<'value, 'error>>(Ok value))

    let map
        (mapper: 'value -> 'next)
        (flow: CandidateValueTaskFlow<'env, 'error, 'value>)
        : CandidateValueTaskFlow<'env, 'error, 'next> =
        CandidateValueTaskFlow(fun environment cancellationToken ->
            let operation = run environment cancellationToken flow

            if operation.IsCompletedSuccessfully then
                match operation.Result with
                | Ok value -> ValueTask<Result<'next, 'error>>(Ok(mapper value))
                | Error error -> ValueTask<Result<'next, 'error>>(Error error)
            else
                ValueTask<Result<'next, 'error>>(
                    task {
                        let! result = operation.AsTask()

                        return
                            match result with
                            | Ok value -> Ok(mapper value)
                            | Error error -> Error error
                    }
                ))

    let bind
        (binder: 'value -> CandidateValueTaskFlow<'env, 'error, 'next>)
        (flow: CandidateValueTaskFlow<'env, 'error, 'value>)
        : CandidateValueTaskFlow<'env, 'error, 'next> =
        CandidateValueTaskFlow(fun environment cancellationToken ->
            let operation = run environment cancellationToken flow

            if operation.IsCompletedSuccessfully then
                match operation.Result with
                | Ok value -> binder value |> run environment cancellationToken
                | Error error -> ValueTask<Result<'next, 'error>>(Error error)
            else
                ValueTask<Result<'next, 'error>>(
                    task {
                        let! result = operation.AsTask()

                        match result with
                        | Ok value ->
                            return! binder value |> run environment cancellationToken |> _.AsTask()
                        | Error error -> return Error error
                    }
                ))

type CandidateValueTaskFlowBuilder() =
    member _.Return(value: 'value) : CandidateValueTaskFlow<'env, 'error, 'value> =
        CandidateValueTaskFlow.succeed value

    member _.ReturnFrom(flow: CandidateValueTaskFlow<'env, 'error, 'value>) : CandidateValueTaskFlow<'env, 'error, 'value> =
        flow

    member _.Bind
        (
            flow: CandidateValueTaskFlow<'env, 'error, 'value>,
            binder: 'value -> CandidateValueTaskFlow<'env, 'error, 'next>
        ) : CandidateValueTaskFlow<'env, 'error, 'next> =
        CandidateValueTaskFlow.bind binder flow

[<RequireQualifiedAccess>]
module CandidateValueTaskFlowDsl =
    let candidateValueTaskFlow = CandidateValueTaskFlowBuilder()
