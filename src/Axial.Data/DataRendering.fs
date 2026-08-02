namespace Axial

open System
open System.Globalization
open System.Text

/// <summary>Internal human-readable and deterministic JSON rendering for structured data.</summary>
[<RequireQualifiedAccess>]
module internal DataRendering =
    let renderText (value: string) =
        let builder = StringBuilder()

        value
        |> Seq.iter (function
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\b' -> builder.Append("\\b") |> ignore
            | '\f' -> builder.Append("\\f") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | character when int character < 0x20 ->
                builder.Append("\\u").Append((int character).ToString("x4", CultureInfo.InvariantCulture)) |> ignore
            | character -> builder.Append(character) |> ignore)

        $"\"{builder}\""

    let renderName (name: string) =
        let isPlainStart character = Char.IsLetter character || character = '_'
        let isPlain character = Char.IsLetterOrDigit character || character = '_' || character = '-'

        if name.Length > 0 && isPlainStart name[0] && (name |> Seq.skip 1 |> Seq.forall isPlain) then
            name
        else
            renderText name

    let jsonRenderCompact input =

        let rec render value =
            match value with
            | Data.Null -> "null"
            | Data.Text text -> renderText text
            | Data.Number token -> token
            | Data.Bool true -> "true"
            | Data.Bool false -> "false"
            | Data.List items -> items |> List.map render |> String.concat "," |> fun body -> $"[{body}]"
            | Data.Object fields ->
                fields
                |> List.map (fun (name, field) -> $"{renderText name}:{render field}")
                |> String.concat ","
                |> fun body -> $"{{{body}}}"

        render input

    let jsonRenderIndented input =
        let compactScalar value =
            match value with
            | Data.List _
            | Data.Object _ -> None
            | scalar -> Some(jsonRenderCompact scalar)

        let rec render level value =
            let indent count = String(' ', count * 2)

            match compactScalar value with
            | Some scalar -> scalar
            | None ->
                match value with
                | Data.List [] -> "[]"
                | Data.List items ->
                    items
                    |> List.map (fun item -> $"{indent (level + 1)}{render (level + 1) item}")
                    |> String.concat ",\n"
                    |> fun body -> $"[\n{body}\n{indent level}]"
                | Data.Object [] -> "{}"
                | Data.Object fields ->
                    fields
                    |> List.map (fun (name, field) ->
                        let encodedName = renderText name
                        $"{indent (level + 1)}{encodedName}: {render (level + 1) field}")
                    |> String.concat ",\n"
                    |> fun body -> $"{{\n{body}\n{indent level}}}"
                | _ -> failwith "Unreachable scalar rendering branch."

        render 0 input

    let renderCompact input =
        let rec render value =
            match value with
            | Data.Null -> "null"
            | Data.Text text -> renderText text
            | Data.Number token -> token
            | Data.Bool true -> "true"
            | Data.Bool false -> "false"
            | Data.List items -> items |> List.map render |> String.concat ", " |> fun body -> $"[{body}]"
            | Data.Object fields ->
                match fields with
                | [] -> "{}"
                | _ ->
                    fields
                    |> List.map (fun (name, field) -> $"{renderName name}: {render field}")
                    |> String.concat ", "
                    |> fun body -> $"{{ {body} }}"

        render input

    let renderIndented input =
        let compactScalar value =
            match value with
            | Data.List _
            | Data.Object _ -> None
            | scalar -> Some(renderCompact scalar)

        let rec render level value =
            let indent count = String(' ', count * 2)

            match compactScalar value with
            | Some scalar -> scalar
            | None ->
                match value with
                | Data.List [] -> "[]"
                | Data.List items ->
                    items
                    |> List.map (fun item -> $"{indent (level + 1)}{render (level + 1) item}")
                    |> String.concat ",\n"
                    |> fun body -> $"[\n{body}\n{indent level}]"
                | Data.Object [] -> "{}"
                | Data.Object fields ->
                    fields
                    |> List.map (fun (name, field) ->
                        $"{indent (level + 1)}{renderName name}: {render (level + 1) field}")
                    |> String.concat ",\n"
                    |> fun body -> $"{{\n{body}\n{indent level}}}"
                | _ -> failwith "Unreachable scalar rendering branch."

        render 0 input

