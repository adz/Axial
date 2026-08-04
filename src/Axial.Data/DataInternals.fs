namespace Axial.Data

open System

type PatternConversion =
    static member ToPattern(value: Data) = DataPattern(Exact value)
    static member ToPattern(value: DataPattern) = value
    static member ToPattern(value: string) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: bool) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: int) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: int64) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: decimal) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: float) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: Guid) = DataPattern(Exact(Data.From value))
    static member ToPattern(value: DateTimeOffset) = DataPattern(Exact(Data.From value))
#if NET8_0_OR_GREATER
    static member ToPattern(value: DateOnly) = DataPattern(Exact(Data.From value))
#endif

    static member private ExactList(values: Data list) =
        if isNull (box values) then DataPattern.CreateExact Data.Null
        else DataPattern.CreateExact(Data.List values)

    static member private ExactListOf(values: 'value list, convert: 'value -> Data) =
        if isNull (box values) then DataPattern.CreateExact Data.Null
        else values |> List.map convert |> PatternConversion.ExactList

    static member ToPattern(values: Data list) = PatternConversion.ExactList values
    static member ToPattern(values: string list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: bool list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: int list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: int64 list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: decimal list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: float list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: Guid list) = PatternConversion.ExactListOf(values, Data.From)
    static member ToPattern(values: DateTimeOffset list) = PatternConversion.ExactListOf(values, Data.From)
#if NET8_0_OR_GREATER
    static member ToPattern(values: DateOnly list) = PatternConversion.ExactListOf(values, Data.From)
#endif

    static member ToPattern(fields: DataField list) =
        let values =
            fields
            |> List.choose (fun field ->
                if field.Omitted then
                    None
                else
                    match field.Pattern.Node with
                    | Exact value -> Some(field.Name, value)
                    | _ ->
                        invalidArg
                            (nameof fields)
                            "A data literal can contain only exact values. Use containing for partial object patterns.")

        DataPattern(Exact(Data.Object values))

    static member inline ToPattern(values: ^value list) =
        if isNull (box values) then
            DataPattern.CreateExact Data.Null
        else
            let inline convert (witness: ^w) (value: ^v) =
                ((^w or ^v): (static member ToPattern: ^v -> DataPattern) value)

            let items =
                values
                |> List.map (convert Unchecked.defaultof<PatternConversion>)
                |> List.map (fun pattern -> pattern.RequireExact(nameof values))

            DataPattern.CreateExact(Data.List items)

[<AutoOpen>]
module DataErgonomicsHelpers =
    let inline toPatternWith (witness: ^witness) (value: ^value) : DataPattern =
        ((^witness or ^value): (static member ToPattern: ^value -> DataPattern) value)

    let inline toPattern (value: ^value) : DataPattern =
        toPatternWith Unchecked.defaultof<PatternConversion> value

    let exactValue argumentName (pattern: DataPattern) = pattern.RequireExact argumentName

    let ensureText argumentName (value: string) =
        if isNull value then nullArg argumentName
        value

    let ensureNonEmptyText argumentName (value: string) =
        ensureText argumentName value |> ignore
        if value = "" then invalidArg argumentName "The value cannot be empty."
        value

    let isJsonNumberToken (token: string) =
        let length = token.Length
        let isDigitAt index = index < length && token[index] >= '0' && token[index] <= '9'
        let rec consumeDigits index = if isDigitAt index then consumeDigits (index + 1) else index

        let afterSign = if length > 0 && token[0] = '-' then 1 else 0

        let afterInteger =
            if afterSign < length && token[afterSign] = '0' then
                Some(afterSign + 1)
            elif afterSign < length && token[afterSign] >= '1' && token[afterSign] <= '9' then
                Some(consumeDigits (afterSign + 1))
            else
                None

        match afterInteger with
        | None -> false
        | Some integerEnd ->
            let afterFraction =
                if integerEnd < length && token[integerEnd] = '.' then
                    let fractionStart = integerEnd + 1
                    if isDigitAt fractionStart then Some(consumeDigits fractionStart) else None
                else
                    Some integerEnd

            match afterFraction with
            | None -> false
            | Some fractionEnd when fractionEnd < length && (token[fractionEnd] = 'e' || token[fractionEnd] = 'E') ->
                let exponentStart = fractionEnd + 1
                let digitsStart =
                    if exponentStart < length && (token[exponentStart] = '+' || token[exponentStart] = '-') then
                        exponentStart + 1
                    else
                        exponentStart

                isDigitAt digitsStart && consumeDigits digitsStart = length
            | Some fractionEnd -> fractionEnd = length

    let appendPath segment path = path @ [ segment ]

    let shapeName value =
        match value with
        | Data.Null -> "null"
        | Data.Text _ -> "text"
        | Data.Number _ -> "number"
        | Data.Bool _ -> "Boolean"
        | Data.List _ -> "list"
        | Data.Object _ -> "object"

    let tryResolveDetailed (path: DataPath) (input: Data) =
        let rec loop traversed remaining current =
            match remaining with
            | [] -> Ok current
            | DataPathSegment.Name name :: rest ->
                match current with
                | Data.Object fields ->
                    match fields |> List.tryFindIndexBack (fun (fieldName, _) -> fieldName = name) with
                    | Some index -> loop (appendPath (DataPathSegment.Name name) traversed) rest (snd fields[index])
                    | None -> Error(traversed, $"Object field '{name}' does not exist.")
                | actual -> Error(traversed, $"Expected an object but found {shapeName actual}.")
            | DataPathSegment.Index index :: rest ->
                match current with
                | Data.List items when index < items.Length ->
                    loop (appendPath (DataPathSegment.Index index) traversed) rest items[index]
                | Data.List items -> Error(traversed, $"List index {index} is outside the list of {items.Length} items.")
                | actual -> Error(traversed, $"Expected a list but found {shapeName actual}.")

        loop [] path input
