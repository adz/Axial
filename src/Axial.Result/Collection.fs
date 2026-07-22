namespace Axial.ErrorHandling

/// <summary>Small collection helpers for fail-fast result traversal.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Collection =
    /// <summary>Maps each value with a result-returning function, stopping at the first error.</summary>
    let traverseResult
        (mapping: 'input -> Result<'output, 'error>)
        (values: seq<'input>)
        : Result<'output list, 'error> =
        let rec loop accumulated remaining =
            match remaining with
            | [] -> Ok(List.rev accumulated)
            | head :: tail ->
                match mapping head with
                | Ok value -> loop (value :: accumulated) tail
                | Error failure -> Error failure

        loop [] (Seq.toList values)

    /// <summary>Turns a sequence of results into one fail-fast result containing all successes.</summary>
    let sequenceResult (values: seq<Result<'value, 'error>>) : Result<'value list, 'error> =
        traverseResult id values
