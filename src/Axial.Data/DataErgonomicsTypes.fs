namespace Axial

open System

type internal DataPatternNode =
    | Exact of Data
    | ObjectContaining of DataField list
    | ListContaining of DataPattern list
    | ListInOrder of DataPattern list
    | EveryItem of DataPattern
    | SomeItem of DataPattern
    | Any
    | AnyText
    | AnyNumber
    | OneOf of DataPattern list
    | Predicate of description: string * predicate: (Data -> bool)

/// <summary>An opaque recursive expectation used to match structured data.</summary>
and [<Sealed>] DataPattern internal (node: DataPatternNode) =
    member internal _.Node = node

    static member CreateExact(value: Data) = DataPattern(Exact value)

    static member CreateListContaining(patterns: DataPattern list) = DataPattern(ListContaining patterns)

    static member CreateListInOrder(patterns: DataPattern list) = DataPattern(ListInOrder patterns)

    static member CreateEveryItem(pattern: DataPattern) = DataPattern(EveryItem pattern)

    static member CreateSomeItem(pattern: DataPattern) = DataPattern(SomeItem pattern)

    member this.RequireExact(argumentName: string) =
        match this.Node with
        | Exact value -> value
        | _ -> invalidArg argumentName "This operation requires an exact data value, not a partial pattern."

/// <summary>An opaque object-field instruction shared by data literals and object patterns.</summary>
and [<Sealed>] DataField internal (name: string, pattern: DataPattern, omitted: bool) =
    member internal _.Name = name
    member internal _.Pattern = pattern
    member internal _.Omitted = omitted

    static member Create(name, pattern, omitted) = DataField(name, pattern, omitted)

type internal DataEditNode =
    | Set of DataPath * Data
    | Put of DataPath * Data
    | Remove of DataPath
    | Append of DataPath * Data
    | Prepend of DataPath * Data
    | Insert of DataPath * int * Data
    | Rename of DataPath * string
    | Update of DataPath * (Data -> Data)

/// <summary>An opaque immutable edit applied by <c>Data.tryPatch</c> or <c>patch</c>.</summary>
[<Sealed>]
type DataEdit internal (node: DataEditNode, renderedPath: string) =
    member internal _.Node = node
    /// <summary>The authored path targeted by the edit.</summary>
    member _.Path = renderedPath

    static member CreateSet(path, value) = DataEdit(Set(DataPath.parse path, value), path)

    static member CreatePut(path, value) = DataEdit(Put(DataPath.parse path, value), path)

    static member CreateAppend(path, value) = DataEdit(Append(DataPath.parse path, value), path)

    static member CreatePrepend(path, value) = DataEdit(Prepend(DataPath.parse path, value), path)

    static member CreateInsert(path, index, value) = DataEdit(Insert(DataPath.parse path, index, value), path)

/// <summary>Describes why one immutable data edit could not be applied.</summary>
type DataPatchFailure =
    {
        /// <summary>The zero-based position of the failing edit.</summary>
        EditIndex: int
        /// <summary>The rendered path targeted by the edit.</summary>
        Path: string
        /// <summary>A concise explanation of the incompatible path or shape.</summary>
        Message: string
    }

/// <summary>Raised by authored patch syntax when an edit cannot be applied.</summary>
type DataPatchException(failures: DataPatchFailure list) =
    inherit Exception(
        failures
        |> List.map (fun failure -> $"Edit {failure.EditIndex} at '{failure.Path}': {failure.Message}")
        |> String.concat Environment.NewLine
    )

    /// <summary>The structured patch failures.</summary>
    member _.Failures = failures

/// <summary>A named immutable variation from one baseline value.</summary>
type DataVariation =
    {
        /// <summary>The case name.</summary>
        Name: string
        /// <summary>The edits applied to the baseline.</summary>
        Edits: DataEdit list
    }

/// <summary>A named materialized structured-data case.</summary>
type DataCase =
    {
        /// <summary>The deterministic case name.</summary>
        Name: string
        /// <summary>The materialized value.</summary>
        Value: Data
    }

/// <summary>One independent axis in a bounded Cartesian data matrix.</summary>
type DataDimension =
    {
        /// <summary>The dimension name.</summary>
        Name: string
        /// <summary>The ordered variations in the dimension.</summary>
        Variations: DataVariation list
    }

/// <summary>The reason an exact structural comparison differed.</summary>
[<RequireQualifiedAccess>]
type DataDifferenceCause =
    | Missing
    | Unexpected
    | DifferentValue
    | DifferentShape
    | DifferentFieldName

/// <summary>One focused difference between expected and actual structured data.</summary>
type DataDifference =
    {
        /// <summary>The location of the difference.</summary>
        Path: DataPath
        /// <summary>The expected value at the location, when present.</summary>
        Expected: Data option
        /// <summary>The actual value at the location, when present.</summary>
        Actual: Data option
        /// <summary>The difference category.</summary>
        Cause: DataDifferenceCause
    }

/// <summary>An opaque path-based expectation.</summary>
[<Sealed>]
type DataExpectation internal (path: DataPath, pattern: DataPattern option, renderedPath: string) =
    member internal _.ParsedPath = path
    member internal _.Pattern = pattern
    /// <summary>The authored path.</summary>
    member _.Path = renderedPath

    static member Create(path, pattern) = DataExpectation(DataPath.parse path, Some pattern, path)

/// <summary>One failed selective or recursive data expectation.</summary>
type DataMismatch =
    {
        /// <summary>The zero-based position of the top-level expectation.</summary>
        ExpectationIndex: int
        /// <summary>The full path at which matching failed.</summary>
        Path: DataPath
        /// <summary>A concise description of the expected observation.</summary>
        Expected: string
        /// <summary>The actual value, or <c>None</c> when it was absent.</summary>
        Actual: Data option
    }

/// <summary>Raised by authored matching syntax when one or more expectations fail.</summary>
type DataMatchException(mismatches: DataMismatch list) =
    inherit Exception(
        mismatches
        |> List.map (fun mismatch ->
            let path = DataPath.toString mismatch.Path
            let rendered = if path = "" then "<root>" else path
            $"Expectation {mismatch.ExpectationIndex} failed at '{rendered}': expected {mismatch.Expected}.")
        |> String.concat Environment.NewLine
    )

    /// <summary>The structured mismatches.</summary>
    member _.Mismatches = mismatches
