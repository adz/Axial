namespace Axial.Validation

/// <summary>Location markers used to describe where a diagnostic belongs in a validation graph.</summary>
/// <example>
/// <code>
/// let s1 = PathSegment.Key "user-123"
/// let s2 = PathSegment.Index 0
/// let s3 = PathSegment.Name "Email"
/// </code>
/// </example>
[<RequireQualifiedAccess>]
type PathSegment =
    /// <summary>A string-based key, usually for map or record fields.</summary>
    | Key of string
    /// <summary>A zero-based integer index, usually for lists or arrays.</summary>
    | Index of int
    /// <summary>A descriptive name for a property or field.</summary>
    | Name of string

/// <summary>A path through a validation graph, represented as a list of <see cref="T:Axial.Validation.PathSegment" />.</summary>
/// <example>
/// <code>
/// let path: Path = [ PathSegment.Name "User"; PathSegment.Index 0; PathSegment.Name "Email" ]
/// </code>
/// </example>
type Path = PathSegment list

/// <summary>A single failure item attached to a path in a validation graph.</summary>
/// <example>
/// <code>
/// let d = { Path = [ PathSegment.Name "Email" ]; Error = "Invalid format" }
/// </code>
/// </example>
type Diagnostic<'error> =
    {
        /// <summary>The path to the source of the error.</summary>
        Path: Path
        /// <summary>The application-specific error value of type <c>'error</c>.</summary>
        Error: 'error
    }

/// <summary>
/// A mergeable validation graph that carries local errors and nested child branches.
/// </summary>
/// <remarks>
/// <para>
/// <c>Errors</c> holds the application errors attached exactly to the current node, while
/// <c>Children</c> holds nested branches keyed by <see cref="T:Axial.Validation.PathSegment" />.
/// </para>
/// <para>
/// This structure allows hierarchical validation to stay navigable before flattening.
/// Use <see cref="T:Axial.Diagnostics.flatten" /> to convert it into a linear list.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// let diag = { Errors = ["Root error"]; Children = Map.empty }
/// </code>
/// </example>
type Diagnostics<'error> =
    {
        /// <summary>Errors that occurred exactly at this node in the graph.</summary>
        Errors: 'error list
        /// <summary>Nested diagnostic branches, keyed by <see cref="T:Axial.Validation.PathSegment" />.</summary>
        Children: Map<PathSegment, Diagnostics<'error>>
    }

/// <summary>
/// Helpers for building, merging, and flattening validation diagnostics graphs.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Diagnostics =
    /// <summary>Creates an empty diagnostics graph with no errors.</summary>
    /// <returns>An empty <see cref="T:Axial.Diagnostics`1" />.</returns>
    /// <example>
    /// <code>
    /// let d = Diagnostics.empty&lt;string&gt;
    /// </code>
    /// </example>
    let empty<'error> : Diagnostics<'error> =
        {
            Errors = []
            Children = Map.empty
        }

    /// <summary>Creates a diagnostics graph containing exactly one error at the root.</summary>
    /// <param name="error">The application error to store at the root.</param>
    /// <returns>A <see cref="T:Axial.Diagnostics`1" /> with a single error.</returns>
    /// <example>
    /// <code>
    /// let d = Diagnostics.singleton "Something failed"
    /// </code>
    /// </example>
    let singleton (error: 'error) : Diagnostics<'error> =
        {
            Errors = [ error ]
            Children = Map.empty
        }

    /// <summary>Recursively merges two diagnostics graphs, combining shared branches and local errors.</summary>
    /// <remarks>
    /// This is the core operation for applicative validation. It ensures that errors from sibling
    /// fields are collected together into a single structured graph.
    /// </remarks>
    /// <param name="left">The first graph of type <see cref="T:Axial.Diagnostics`1" />.</param>
    /// <param name="right">The second graph of type <see cref="T:Axial.Diagnostics`1" />.</param>
    /// <returns>A new <see cref="T:Axial.Diagnostics`1" /> containing the union of both inputs.</returns>
    /// <example>
    /// <code>
    /// let d1 = Diagnostics.singleton "Error 1"
    /// let d2 = Diagnostics.singleton "Error 2"
    /// let combined = Diagnostics.merge d1 d2
    /// </code>
    /// </example>
    let rec merge (left: Diagnostics<'error>) (right: Diagnostics<'error>) : Diagnostics<'error> =
        let addBranch children key branch =
            match Map.tryFind key children with
            | Some existing -> Map.add key (merge existing branch) children
            | None -> Map.add key branch children

        {
            Errors = left.Errors @ right.Errors
            Children = Map.fold addBranch left.Children right.Children
        }

    /// <summary>Renders a diagnostics graph in a YAML-like layout for display.</summary>
    /// <remarks>
    /// This is intended for human-readable output. Empty sections are omitted, and children are
    /// shown directly under their branch labels at the same indentation level as errors. Errors
    /// render as YAML-style bullet items without an `Errors:` key. Use
    /// <see cref="T:Axial.Diagnostics.flatten" /> when you need path-bearing diagnostics for
    /// reporting or assertions.
    /// </remarks>
    /// <param name="graph">The diagnostics graph to render.</param>
    /// <returns>A formatted string representation of the graph.</returns>
    /// <example>
    /// <code>
    /// let d = Diagnostics.singleton "fail"
    /// let s = Diagnostics.toString d
    /// </code>
    /// </example>
    let toString (graph: Diagnostics<'error>) : string =
        let indent level = String.replicate (level * 2) " "

        let segmentText = function
            | PathSegment.Key key -> key
            | PathSegment.Index index -> $"[{index}]"
            | PathSegment.Name name -> name

        let rec renderNode level (node: Diagnostics<'error>) =
            let lines = System.Collections.Generic.List<string>()

            match node.Errors with
            | [] -> ()
            | errors ->
                errors
                |> List.iter (fun error -> lines.Add($"{indent level}- {string error}"))

            match node.Children |> Map.toList with
            | [] -> ()
            | children ->
                children
                |> List.iter (fun (segment, child) ->
                    lines.Add($"{indent level}{segmentText segment}:")
                    lines.Add(renderNode (level + 1) child))

            if lines.Count = 0 then
                $"{indent level}[]"
            else
                String.concat System.Environment.NewLine lines

        renderNode 0 graph

    /// <summary>Flattens the structured diagnostics graph into a linear list of diagnostics.</summary>
    /// <remarks>
    /// During flattening, child paths are accumulated from the root down into each emitted diagnostic.
    /// The tree itself stores only local errors and child branches, while <see cref="T:Axial.Diagnostic`1" />
    /// is reserved for reporting output.
    /// </remarks>
    /// <param name="graph">The <see cref="T:Axial.Diagnostics`1" /> to flatten.</param>
    /// <returns>A list of type <see cref="T:Axial.Diagnostic`1" /> list.</returns>
    /// <example>
    /// <code>
    /// let d = Diagnostics.singleton "fail"
    /// let flat = Diagnostics.flatten d
    /// </code>
    /// </example>
    let flatten (graph: Diagnostics<'error>) : Diagnostic<'error> list =
        let rec flattenWithPrefix (prefix: Path) (node: Diagnostics<'error>) =
            let local =
                node.Errors
                |> List.map (fun error -> { Path = prefix; Error = error })

            let children =
                node.Children
                |> Map.toList
                |> List.collect (fun (segment, child) -> flattenWithPrefix (prefix @ [ segment ]) child)

            local @ children

        flattenWithPrefix [] graph

    /// <summary>Maps every error in a diagnostics graph to a domain or application error, preserving structure and paths.</summary>
    /// <remarks>
    /// This is the boundary-error translation point: schema and input interpreters emit an interpreter-specific error
    /// type such as <c>SchemaError</c>, and callers use <c>map</c> to translate that into their own domain or
    /// application error type before the diagnostics graph crosses into application code. The path structure and
    /// child branches are preserved; only the leaf errors are transformed.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="graph">The diagnostics graph to map.</param>
    /// <returns>A new <see cref="T:Axial.Validation.Diagnostics`1" /> with every error transformed by <paramref name="mapper" />.</returns>
    /// <example>
    /// <code>
    /// let domainDiagnostics = Diagnostics.map SchemaError.toDomainError schemaDiagnostics
    /// </code>
    /// </example>
    let rec map (mapper: 'error -> 'nextError) (graph: Diagnostics<'error>) : Diagnostics<'nextError> =
        {
            Errors = graph.Errors |> List.map mapper
            Children = graph.Children |> Map.map (fun _ child -> map mapper child)
        }
