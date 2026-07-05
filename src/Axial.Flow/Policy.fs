namespace Axial.Flow

/// <summary>
/// Represents an environment-aware requirement that turns an input into either an output or a workflow error.
/// </summary>
/// <typeparam name="env">The workflow environment available to the policy.</typeparam>
/// <typeparam name="error">The workflow error produced by the policy.</typeparam>
/// <typeparam name="input">The input value checked by the policy.</typeparam>
/// <typeparam name="output">The output value produced by the policy.</typeparam>
type Policy<'env, 'error, 'input, 'output> =
    'env -> 'input -> Result<'output, 'error>

/// <summary>Constructors and combinators for environment-aware workflow requirements.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Policy =
    /// <summary>Lifts a pure result-returning function and maps its error into the workflow error type.</summary>
    let lift
        (operation: 'input -> Result<'output, 'innerError>)
        (mapError: 'innerError -> 'error)
        : Policy<'env, 'error, 'input, 'output> =
        fun _ input ->
            match operation input with
            | Ok value -> Ok value
            | Error failure -> Error(mapError failure)

    /// <summary>Lifts a pure result-returning function and replaces any error with a fixed workflow error.</summary>
    let withError
        (operation: 'input -> Result<'output, 'innerError>)
        (error: 'error)
        : Policy<'env, 'error, 'input, 'output> =
        lift operation (fun _ -> error)

    /// <summary>Lifts an environment-aware result-returning function and maps its error into the workflow error type.</summary>
    let context
        (operation: 'env -> 'input -> Result<'output, 'innerError>)
        (mapError: 'innerError -> 'error)
        : Policy<'env, 'error, 'input, 'output> =
        fun environment input ->
            match operation environment input with
            | Ok value -> Ok value
            | Error failure -> Error(mapError failure)

    /// <summary>A policy that returns the input unchanged.</summary>
    let pass : Policy<'env, 'error, 'input, 'input> =
        fun _ input -> Ok input

    /// <summary>Composes two policies left to right.</summary>
    let compose
        (first: Policy<'env, 'error, 'input, 'middle>)
        (second: Policy<'env, 'error, 'middle, 'output>)
        : Policy<'env, 'error, 'input, 'output> =
        fun environment input ->
            match first environment input with
            | Ok value -> second environment value
            | Error failure -> Error failure

    /// <summary>Runs a policy only when the environment predicate is true; otherwise returns the input unchanged.</summary>
    let optional
        (enabled: 'env -> bool)
        (policy: Policy<'env, 'error, 'input, 'input>)
        : Policy<'env, 'error, 'input, 'input> =
        fun environment input ->
            if enabled environment then
                policy environment input
            else
                Ok input
