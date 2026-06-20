namespace Axial.Validation

/// <summary>
/// An accumulating validation result that keeps the structured diagnostics graph visible.
/// </summary>
/// <remarks>
/// Unlike <see cref="T:Microsoft.FSharp.Core.FSharpResult`2" />, this type is designed for applicative
/// composition using <c>and!</c> in the <c>validate { }</c> builder, which merges errors instead of
/// short-circuiting.
/// </remarks>
/// <example>
/// <code>
/// let v1 = Validation.ok 5
/// let v2 = Validation.error (Diagnostics.singleton "Error 1")
/// </code>
/// </example>
type Validation<'value, 'error> = private Validation of Result<'value, Diagnostics<'error>>

/// <summary>
/// Helpers for accumulating validation results with mergeable diagnostics.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Validation =
    let private unwrap (Validation result) = result

    /// <summary>Converts a <see cref="T:Axial.Validation`2" /> into a standard <see cref="T:System.Result`2" />.</summary>
    /// <param name="validation">The validation to convert.</param>
    /// <returns>A result containing either the success value or the full diagnostics graph.</returns>
    /// <example>
    /// <code>
    /// let res = Validation.ok 5 |> Validation.toResult // Ok 5
    /// </code>
    /// </example>
    let toResult (validation: Validation<'value, 'error>) : Result<'value, Diagnostics<'error>> =
        unwrap validation

    /// <summary>Creates a successful validation result.</summary>
    /// <param name="value">The success value of type <c>'value</c>.</param>
    /// <returns>A successful <see cref="T:Axial.Validation`2" />.</returns>
    /// <example>
    /// <code>
    /// let v = Validation.ok 5
    /// </code>
    /// </example>
    let ok (value: 'value) : Validation<'value, 'error> =
        Validation (Ok value)

    /// <summary>Alias for <c>ok</c>.</summary>
    /// <param name="value">The success value of type <c>'value</c>.</param>
    /// <returns>A successful <see cref="T:Axial.Validation`2" />.</returns>
    let succeed (value: 'value) : Validation<'value, 'error> =
        ok value

    /// <summary>Creates a failing validation result with the provided diagnostics.</summary>
    /// <param name="diagnostics">The <see cref="T:Axial.Diagnostics`1" /> graph.</param>
    /// <returns>A failing <see cref="T:Axial.Validation`2" />.</returns>
    /// <example>
    /// <code>
    /// let v = Validation.error (Diagnostics.singleton "Something went wrong")
    /// </code>
    /// </example>
    let error (diagnostics: Diagnostics<'error>) : Validation<'value, 'error> =
        Validation (Error diagnostics)

    /// <summary>Alias for <c>error</c>.</summary>
    /// <param name="diagnostics">The <see cref="T:Axial.Diagnostics`1" /> graph.</param>
    /// <returns>A failing <see cref="T:Axial.Validation`2" />.</returns>
    let fail (diagnostics: Diagnostics<'error>) : Validation<'value, 'error> =
        error diagnostics

    /// <summary>Lifts a standard <see cref="T:System.Result`2" /> into the <see cref="T:Axial.Validation`2" /> context.</summary>
    /// <remarks>
    /// If the result is an error, it is wrapped in a root-level <see cref="T:Axial.Diagnostics`1" /> graph.
    /// </remarks>
    /// <param name="result">The result to lift.</param>
    /// <returns>A <see cref="T:Axial.Validation`2" /> mirroring the result.</returns>
    /// <example>
    /// <code>
    /// let v = Result.Ok 5 |> Validation.fromResult // Validation (Ok 5)
    /// let v2 = Result.Error "fail" |> Validation.fromResult // Validation (Error { Errors = ["fail"]; ... })
    /// </code>
    /// </example>
    let fromResult (result: Result<'value, 'error>) : Validation<'value, 'error> =
        match result with
        | Ok value -> ok value
        | Error failure -> error (Diagnostics.singleton failure)

    /// <summary>Maps the successful value of a validation.</summary>
    /// <param name="mapper">A function of type <c>'value -> 'next</c>.</param>
    /// <param name="validation">The source <see cref="T:Axial.Validation`2" />.</param>
    /// <returns>A validation with the transformed success value.</returns>
    /// <example>
    /// <code>
    /// Validation.ok 5 |> Validation.map (fun x -> x * 2) // Validation (Ok 10)
    /// </code>
    /// </example>
    let map
        (mapper: 'value -> 'next)
        (validation: Validation<'value, 'error>)
        : Validation<'next, 'error> =
        validation |> unwrap |> Result.map mapper |> Validation

    /// <summary>Sequences a validation-producing continuation.</summary>
    /// <remarks>
    /// This is the monadic "bind" for validation. Note that this operation short-circuits
    /// and does not accumulate errors from the binder if the source has already failed.
    /// For accumulation, use <see cref="map2" /> or the applicative <c>and!</c> syntax.
    /// </remarks>
    /// <param name="binder">A function of type <c>'value -> Validation&lt;'next, 'error&gt;</c>.</param>
    /// <param name="validation">The source validation.</param>
    /// <returns>The result of the binder or the original diagnostics.</returns>
    /// <example>
    /// <code>
    /// Validation.ok 5 |> Validation.bind (fun x -> Validation.ok (x + 1)) // Validation (Ok 6)
    /// </code>
    /// </example>
    let bind
        (binder: 'value -> Validation<'next, 'error>)
        (validation: Validation<'value, 'error>)
        : Validation<'next, 'error> =
        match unwrap validation with
        | Ok value -> binder value
        | Error diagnostics -> error diagnostics

    /// <summary>Maps the error type of a validation graph.</summary>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="validation">The source <see cref="T:Axial.Validation`2" />.</param>
    /// <returns>A validation with transformed error values.</returns>
    /// <example>
    /// <code>
    /// validation |> Validation.mapError (fun e -> e.ToString())
    /// </code>
    /// </example>
    let mapError
        (mapper: 'error -> 'nextError)
        (validation: Validation<'value, 'error>)
        : Validation<'value, 'nextError> =
        let rec mapDiagnostics (graph: Diagnostics<'error>) : Diagnostics<'nextError> =
            {
                Errors =
                    graph.Errors
                    |> List.map mapper
                Children =
                    graph.Children
                    |> Map.map (fun _ child -> mapDiagnostics child)
            }

        validation |> unwrap |> Result.mapError mapDiagnostics |> Validation

    /// <summary>Combines two validations, accumulating errors if both fail.</summary>
    /// <remarks>
    /// This is the core applicative operation. If both <paramref name="left" /> and 
    /// <paramref name="right" /> fail, their diagnostics graphs are merged.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'left -> 'right -> 'value</c>.</param>
    /// <param name="left">The first validation.</param>
    /// <param name="right">The second validation.</param>
    /// <returns>A validation with the combined result.</returns>
    /// <example>
    /// <code>
    /// let v1 = Validation.ok 1
    /// let v2 = Validation.ok 2
    /// Validation.map2 (+) v1 v2 // Validation (Ok 3)
    /// </code>
    /// </example>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: Validation<'left, 'error>)
        (right: Validation<'right, 'error>)
        : Validation<'value, 'error> =
        Validation(
            match unwrap left, unwrap right with
            | Ok leftValue, Ok rightValue -> Ok(mapper leftValue rightValue)
            | Error leftDiagnostics, Ok _ -> Error leftDiagnostics
            | Ok _, Error rightDiagnostics -> Error rightDiagnostics
            | Error leftDiagnostics, Error rightDiagnostics -> Error(Diagnostics.merge leftDiagnostics rightDiagnostics)
        )

    /// <summary>Applies a validation-wrapped function to a validation-wrapped value.</summary>
    /// <param name="validation">The validation containing the function.</param>
    /// <param name="value">The validation containing the value.</param>
    /// <returns>The result of applying the function to the value, with accumulated errors.</returns>
    /// <example>
    /// <code>
    /// let fn = Validation.ok (fun x -> x + 1)
    /// let v = Validation.ok 5
    /// Validation.apply fn v // Validation (Ok 6)
    /// </code>
    /// </example>
    let apply
        (validation: Validation<'value -> 'next, 'error>)
        (value: Validation<'value, 'error>)
        : Validation<'next, 'error> =
        map2 (fun mapper input -> mapper input) validation value

    /// <summary>Maps a successful validation value to <c>unit</c> while preserving the diagnostics.</summary>
    /// <param name="validation">The source validation.</param>
    /// <returns>A validation that keeps the original diagnostics and discards the success value.</returns>
    /// <example>
    /// <code>
    /// Validation.ok 5 |> Validation.ignore // Validation (Ok ())
    /// </code>
    /// </example>
    let ignore (validation: Validation<'value, 'error>) : Validation<unit, 'error> =
        map (fun _ -> ()) validation

    /// <summary>Combines three validations, accumulating errors when any input fails.</summary>
    /// <param name="mapper">A function of type <c>'left -> 'middle -> 'right -> 'value</c>.</param>
    /// <param name="left">The first validation.</param>
    /// <param name="middle">The second validation.</param>
    /// <param name="right">The third validation.</param>
    /// <returns>A validation with the combined result.</returns>
    /// <example>
    /// <code>
    /// let v1 = Validation.ok 1
    /// let v2 = Validation.ok 2
    /// let v3 = Validation.ok 3
    /// Validation.map3 (fun x y z -> x + y + z) v1 v2 v3 // Validation (Ok 6)
    /// </code>
    /// </example>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: Validation<'left, 'error>)
        (middle: Validation<'middle, 'error>)
        (right: Validation<'right, 'error>)
        : Validation<'value, 'error> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Falls back to another validation when the source validation fails.</summary>
    /// <remarks>
    /// This is a left-biased choice operator. If the source succeeds, the fallback is not used.
    /// If the source fails, the fallback validation is returned as-is.
    /// </remarks>
    /// <param name="fallback">The validation to use when the source fails.</param>
    /// <param name="validation">The source validation.</param>
    /// <returns>The source validation when it succeeds, otherwise the fallback validation.</returns>
    /// <example>
    /// <code>
    /// let v1 = Validation.fail (Diagnostics.singleton "err")
    /// let v2 = Validation.ok 5
    /// v1 |> Validation.orElse v2 // Validation (Ok 5)
    /// </code>
    /// </example>
    let orElse
        (fallback: Validation<'value, 'error>)
        (validation: Validation<'value, 'error>)
        : Validation<'value, 'error> =
        match unwrap validation with
        | Ok value -> ok value
        | Error _ -> fallback

    /// <summary>Computes a fallback validation from the source diagnostics when validation fails.</summary>
    /// <remarks>
    /// This is the lazy counterpart to <see cref="orElse" /> and is useful when the alternate
    /// branch depends on the accumulated diagnostics.
    /// </remarks>
    /// <param name="fallback">A function that turns the diagnostics into an alternate validation.</param>
    /// <param name="validation">The source validation.</param>
    /// <returns>The source validation when it succeeds, otherwise the computed fallback validation.</returns>
    /// <example>
    /// <code>
    /// let v1 = Validation.fail (Diagnostics.singleton "err")
    /// v1 |> Validation.orElseWith (fun diag -> Validation.ok 10) // Validation (Ok 10)
    /// </code>
    /// </example>
    let orElseWith
        (fallback: Diagnostics<'error> -> Validation<'value, 'error>)
        (validation: Validation<'value, 'error>)
        : Validation<'value, 'error> =
        match unwrap validation with
        | Ok value -> ok value
        | Error diagnostics -> fallback diagnostics

    /// <summary>Maps the successful value of a validation.</summary>
    /// <param name="mapper">A function of type <c>'value -> 'next</c>.</param>
    /// <param name="validation">The source <see cref="T:Axial.Validation`2" />.</param>
    /// <returns>A validation with the transformed success value.</returns>
    /// <example>
    /// <code>
    /// (+) &lt;!&gt; Validation.ok 1 &lt;*&gt; Validation.ok 2 // Validation (Ok 3)
    /// </code>
    /// </example>
    let inline (<!>) (mapper: 'value -> 'next) (validation: Validation<'value, 'error>) : Validation<'next, 'error> =
        map mapper validation

    /// <summary>Applies a validation-wrapped function to a validation-wrapped value.</summary>
    /// <param name="validation">The validation containing the function.</param>
    /// <param name="value">The validation containing the value.</param>
    /// <returns>The result of applying the function to the value, with accumulated errors.</returns>
    /// <example>
    /// <code>
    /// Validation.ok ((+) 1) &lt;*&gt; Validation.ok 2 // Validation (Ok 3)
    /// </code>
    /// </example>
    let inline (<*>) (validation: Validation<'value -> 'next, 'error>) (value: Validation<'value, 'error>) : Validation<'next, 'error> =
        apply validation value

    /// <summary>Collects a sequence of validations into a single validation of a list.</summary>
    /// <remarks>
    /// This operation is applicative: it will collect errors from ALL items in the sequence.
    /// </remarks>
    /// <param name="validations">A sequence of type <c>seq&lt;Validation&lt;'value, 'error&gt;&gt;</c>.</param>
    /// <returns>A validation containing the list of values or accumulated diagnostics.</returns>
    /// <example>
    /// <code>
    /// [Validation.ok 1; Validation.ok 2] |> Validation.collect // Validation (Ok [1; 2])
    /// </code>
    /// </example>
    let collect (validations: seq<Validation<'value, 'error>>) : Validation<'value list, 'error> =
        let folder
            (state: Validation<'value list, 'error>)
            (validation: Validation<'value, 'error>) =
            map2 (fun values value -> values @ [ value ]) state validation

        Seq.fold folder (ok []) validations

    /// <summary>Transforms a sequence of validations into a validation of a list.</summary>
    /// <param name="validations">The input sequence.</param>
    /// <returns>A validation containing the list of values.</returns>
    /// <example>
    /// <code>
    /// [Validation.ok 1] |> Validation.sequence // Validation (Ok [1])
    /// </code>
    /// </example>
    let sequence (validations: seq<Validation<'value, 'error>>) : Validation<'value list, 'error> =
        collect validations

    /// <summary>Merges two validations into a validation of a tuple.</summary>
    /// <param name="left">The first validation.</param>
    /// <param name="right">The second validation.</param>
    /// <returns>A validation containing a tuple of the results.</returns>
    /// <example>
    /// <code>
    /// Validation.merge (Validation.ok 1) (Validation.ok "a") // Validation (Ok (1, "a"))
    /// </code>
    /// </example>
    let merge (left: Validation<'value, 'error>) (right: Validation<'next, 'error>) : Validation<'value * 'next, 'error> =
        map2 (fun leftValue rightValue -> leftValue, rightValue) left right

    /// <summary>Scopes a validation under the supplied path segments.</summary>
    /// <param name="path">The path segments to apply to the validation.</param>
    /// <param name="validation">The validation to scope.</param>
    /// <returns>A validation nested under the given path.</returns>
    /// <example>
    /// <code>
    /// Validation.error (Diagnostics.singleton "fail") 
    /// |> Validation.at [PathSegment.Name "user"]
    /// </code>
    /// </example>
    let at (path: PathSegment list) (validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        let rec attach path graph =
            match path with
            | [] -> graph
            | segment :: rest ->
                {
                    Errors = []
                    Children = Map.add segment (attach rest graph) Map.empty
                }

        validation |> unwrap |> Result.mapError (attach path) |> Validation

    /// <summary>Prefixes a validation with a keyed branch.</summary>
    /// <param name="key">The branch key.</param>
    /// <param name="validation">The validation to scope.</param>
    /// <returns>A validation whose diagnostics are prefixed with <c>Key key</c>.</returns>
    /// <example>
    /// <code>
    /// Validation.error (Diagnostics.singleton "fail") 
    /// |> Validation.key "id-123"
    /// </code>
    /// </example>
    let key (key: string) (validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        at [ PathSegment.Key key ] validation

    /// <summary>Prefixes a validation with an indexed branch.</summary>
    /// <param name="index">The branch index.</param>
    /// <param name="validation">The validation to scope.</param>
    /// <returns>A validation whose diagnostics are prefixed with <c>Index index</c>.</returns>
    /// <example>
    /// <code>
    /// Validation.error (Diagnostics.singleton "fail") 
    /// |> Validation.index 0
    /// </code>
    /// </example>
    let index (index: int) (validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        at [ PathSegment.Index index ] validation

    /// <summary>Prefixes a validation with a named branch.</summary>
    /// <param name="name">The branch name.</param>
    /// <param name="validation">The validation to scope.</param>
    /// <returns>A validation whose diagnostics are prefixed with <c>Name name</c>.</returns>
    /// <example>
    /// <code>
    /// Validation.error (Diagnostics.singleton "fail") 
    /// |> Validation.name "email"
    /// </code>
    /// </example>
    let name (name: string) (validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        at [ PathSegment.Name name ] validation

    /// <summary>Maps a sequence into validations while prefixing each item with its index.</summary>
    /// <remarks>
    /// This is the indexed version of <see cref="sequence" />. It is useful for list and array
    /// validation because each item can keep its own <see cref="T:Axial.PathSegment.Index" />
    /// branch without the caller manually wrapping every item.
    /// </remarks>
    /// <param name="binder">A function of type <c>int -> 'source -> Validation&lt;'value, 'error&gt;</c>.</param>
    /// <param name="values">The input sequence.</param>
    /// <returns>A validation containing the list of values or accumulated diagnostics.</returns>
    /// <example>
    /// <code>
    /// [ "a"; "b" ] |> Validation.traverseIndexed (fun i s -> Validation.ok (s.ToUpper()))
    /// </code>
    /// </example>
    let traverseIndexed
        (binder: int -> 'source -> Validation<'value, 'error>)
        (values: seq<'source>)
        : Validation<'value list, 'error> =
        values
        |> Seq.mapi (fun i value -> binder i value |> index i)
        |> collect
