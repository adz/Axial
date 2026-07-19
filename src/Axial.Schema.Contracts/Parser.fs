namespace Axial.Schema.Contracts

open System

/// <summary>Parses <c>.contract</c> source text into the contract AST with line-precise diagnostics.</summary>
[<RequireQualifiedAccess>]
module Parser =

    type private Token =
        | TIdent of string
        | TString of string
        | TNumber of string
        | TPunct of string

    let private describeToken token =
        match token with
        | TIdent name -> $"identifier '{name}'"
        | TString value -> $"string \"{value}\""
        | TNumber value -> $"number {value}"
        | TPunct value -> $"'{value}'"

    let private tokenizeLine (line: string) : Result<Token list, string> =
        let mutable index = 0
        let tokens = ResizeArray<Token>()
        let mutable error = None

        let isIdentStart c = Char.IsLetter c || c = '_'
        let isIdentChar c = Char.IsLetterOrDigit c || c = '_'

        while error.IsNone && index < line.Length do
            let c = line.[index]

            if Char.IsWhiteSpace c then
                index <- index + 1
            elif c = '"' then
                let builder = Text.StringBuilder()
                let mutable cursor = index + 1
                let mutable closed = false

                while not closed && cursor < line.Length do
                    match line.[cursor] with
                    | '"' ->
                        closed <- true
                        cursor <- cursor + 1
                    | '\\' when cursor + 1 < line.Length ->
                        let escaped =
                            match line.[cursor + 1] with
                            | 'n' -> "\n"
                            | 't' -> "\t"
                            | '\\' -> "\\"
                            | '"' -> "\""
                            | other -> string other
                        builder.Append escaped |> ignore
                        cursor <- cursor + 2
                    | other ->
                        builder.Append other |> ignore
                        cursor <- cursor + 1

                if closed then
                    tokens.Add(TString(builder.ToString()))
                    index <- cursor
                else
                    error <- Some "unterminated string literal"
            elif isIdentStart c then
                let start = index
                while index < line.Length && isIdentChar line.[index] do
                    index <- index + 1
                tokens.Add(TIdent(line.Substring(start, index - start)))
            elif Char.IsDigit c || (c = '-' && index + 1 < line.Length && Char.IsDigit line.[index + 1]) then
                let start = index
                index <- index + 1
                let mutable seenDot = false
                while index < line.Length && (Char.IsDigit line.[index] || (line.[index] = '.' && not seenDot && index + 1 < line.Length && Char.IsDigit line.[index + 1])) do
                    if line.[index] = '.' then seenDot <- true
                    index <- index + 1
                tokens.Add(TNumber(line.Substring(start, index - start)))
            elif c = '>' || c = '<' then
                if index + 1 < line.Length && line.[index + 1] = '=' then
                    tokens.Add(TPunct(string c + "="))
                    index <- index + 2
                else
                    tokens.Add(TPunct(string c))
                    index <- index + 1
            elif ":?[]{}=,|.".IndexOf c >= 0 then
                tokens.Add(TPunct(string c))
                index <- index + 1
            else
                error <- Some $"unexpected character '{c}'"

        match error with
        | Some message -> Error message
        | None -> Ok(List.ofSeq tokens)

    let private parseLiteral tokens =
        match tokens with
        | TString value :: rest -> Ok(LString value, rest)
        | TNumber value :: rest when value.Contains "." ->
            match Decimal.TryParse(value, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture) with
            | true, parsed -> Ok(LDecimal parsed, rest)
            | false, _ -> Error $"numeric literal '{value}' is out of range for decimal"
        | TNumber value :: rest ->
            match Int32.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture) with
            | true, parsed -> Ok(LInt parsed, rest)
            | false, _ -> Error $"numeric literal '{value}' is out of range for int"
        | TIdent "true" :: rest -> Ok(LBool true, rest)
        | TIdent "false" :: rest -> Ok(LBool false, rest)
        | token :: _ -> Error $"expected a literal, found {describeToken token}"
        | [] -> Error "expected a literal"

    let private parseVersion (ident: string) =
        if ident.Length > 1 && ident.[0] = 'v' && ident |> Seq.skip 1 |> Seq.forall Char.IsDigit then
            match Int32.TryParse(ident.Substring 1, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, version -> Ok version
            | false, _ -> Error $"version '{ident}' is out of range for int"
        else
            Error $"expected a version like v1, found '{ident}'"

    let private primitiveOf name =
        match name with
        | "text" -> Some PText
        | "int" -> Some PInt
        | "decimal" -> Some PDecimal
        | "bool" -> Some PBool
        | "date" -> Some PDate
        | "dateTime" -> Some PDateTime
        | "guid" -> Some PGuid
        | "email" -> Some PEmail
        | _ -> None

    /// Parses a non-union type expression; used for field types and for list/map element types.
    let rec private parseTypeExpr tokens =
        match tokens with
        | TIdent "list" :: rest ->
            parseTypeExpr rest |> Result.map (fun (element, remaining) -> ListOf element, remaining)
        | TIdent "map" :: rest ->
            parseTypeExpr rest |> Result.map (fun (element, remaining) -> MapOf element, remaining)
        | TString first :: rest ->
            let rec collect acc tokens =
                match tokens with
                | TPunct "|" :: TString case :: rest -> collect (case :: acc) rest
                | TPunct "|" :: token :: _ -> Error $"expected a string literal union case, found {describeToken token}"
                | TPunct "|" :: [] -> Error "expected a string literal union case"
                | remaining -> Ok(List.rev acc, remaining)

            collect [ first ] rest
            |> Result.map (fun (cases, remaining) -> LiteralUnion cases, remaining)
        | TIdent name :: TPunct "." :: TIdent versionText :: rest ->
            match parseVersion versionText with
            | Ok version -> Ok(Reference { RefName = name; RefVersion = version }, rest)
            | Error message -> Error message
        | TIdent name :: rest ->
            match primitiveOf name with
            | Some primitive -> Ok(Primitive primitive, rest)
            | None -> Error $"unknown type '{name}' (contract references need a version, like {name}.v1)"
        | token :: _ -> Error $"expected a type, found {describeToken token}"
        | [] -> Error "expected a type"

    let private parseConstraint tokens =
        match tokens with
        | TPunct ">=" :: rest -> parseLiteral rest |> Result.map (fun (lit, remaining) -> AtLeast lit, remaining)
        | TPunct ">" :: rest -> parseLiteral rest |> Result.map (fun (lit, remaining) -> GreaterThan lit, remaining)
        | TPunct "<=" :: rest -> parseLiteral rest |> Result.map (fun (lit, remaining) -> AtMost lit, remaining)
        | TPunct "<" :: rest -> parseLiteral rest |> Result.map (fun (lit, remaining) -> LessThan lit, remaining)
        | TIdent "min" :: TNumber value :: rest when not (value.Contains "." || value.StartsWith "-") ->
            match Int32.TryParse(value, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, parsed -> Ok(MinSize parsed, rest)
            | false, _ -> Error $"size literal '{value}' is out of range for int"
        | TIdent "min" :: _ -> Error "'min' takes a non-negative whole number (it bounds the size of the type; use >= for value bounds)"
        | TIdent "max" :: TNumber value :: rest when not (value.Contains "." || value.StartsWith "-") ->
            match Int32.TryParse(value, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, parsed -> Ok(MaxSize parsed, rest)
            | false, _ -> Error $"size literal '{value}' is out of range for int"
        | TIdent "max" :: _ -> Error "'max' takes a non-negative whole number (it bounds the size of the type; use <= for value bounds)"
        | TIdent "pattern" :: TString value :: rest -> Ok(Pattern value, rest)
        | TIdent "pattern" :: _ -> Error "'pattern' takes a quoted regular expression"
        | TIdent "multipleOf" :: rest -> parseLiteral rest |> Result.map (fun (lit, remaining) -> MultipleOf lit, remaining)
        | TIdent "distinct" :: rest -> Ok(Distinct, rest)
        | TIdent "check" :: TIdent name :: rest -> Ok(CheckRef name, rest)
        | TIdent "check" :: _ -> Error "'check' takes the name of an F# check"
        | token :: _ -> Error $"expected a constraint, found {describeToken token}"
        | [] -> Error "expected a constraint"

    let private parseConstraintList line tokens =
        let rec collect acc tokens =
            match parseConstraint tokens with
            | Error message -> Error message
            | Ok(constraint', remaining) ->
                let acc = (constraint', line) :: acc

                match remaining with
                | TPunct "," :: rest -> collect acc rest
                | TPunct "]" :: rest -> Ok(List.rev acc, rest)
                | token :: _ -> Error $"expected ',' or ']' in the constraint list, found {describeToken token}"
                | [] -> Error "unterminated constraint list; expected ']'"

        collect [] tokens

    type private FieldHead =
        { HeadName: string
          HeadWire: string option
          HeadOptional: bool }

    let private parseFieldHead tokens =
        match tokens with
        | TIdent name :: rest ->
            let optionalAfterName, rest =
                match rest with
                | TPunct "?" :: tail -> true, tail
                | _ -> false, rest

            let wire, rest =
                match rest with
                | TIdent "as" :: TString wire :: tail -> Some wire, tail
                | _ -> None, rest

            let optionalAfterWire, rest =
                match rest with
                | TPunct "?" :: tail -> true, tail
                | _ -> false, rest

            match rest with
            | TPunct ":" :: tail ->
                Ok({ HeadName = name; HeadWire = wire; HeadOptional = optionalAfterName || optionalAfterWire }, tail)
            | token :: _ -> Error $"expected ':' after the field name, found {describeToken token}"
            | [] -> Error "expected ':' after the field name"
        | token :: _ -> Error $"expected a field name, found {describeToken token}"
        | [] -> Error "expected a field name"

    /// The outcome of parsing one field line: either a complete field, or the head of a union block whose
    /// cases follow on subsequent lines.
    type private FieldLine =
        | CompleteField of FieldDecl
        | OpenUnion of head: FieldHead * discriminator: string

    let private parseFieldLine line doc annotations tokens =
        parseFieldHead tokens
        |> Result.bind (fun (head, rest) ->
            match rest with
            | TIdent "union" :: TIdent discriminator :: TPunct "{" :: [] -> Ok(OpenUnion(head, discriminator))
            | TIdent "union" :: TIdent _ :: TPunct "{" :: extra ->
                Error $"unexpected tokens after the union opener, starting with {describeToken (List.head extra)}"
            | TIdent "union" :: _ -> Error "expected 'union <discriminator> {'"
            | _ ->
                parseTypeExpr rest
                |> Result.bind (fun (fieldType, rest) ->
                    let constraintsResult =
                        match rest with
                        | TPunct "[" :: tail -> parseConstraintList line tail
                        | _ -> Ok([], rest)

                    constraintsResult
                    |> Result.bind (fun (constraints, rest) ->
                        let defaultResult =
                            match rest with
                            | TPunct "=" :: tail ->
                                parseLiteral tail
                                |> Result.map (fun (literal, remaining) -> Some literal, remaining)
                            | _ -> Ok(None, rest)

                        defaultResult
                        |> Result.bind (fun (defaultValue, rest) ->
                            match rest with
                            | [] ->
                                Ok(
                                    CompleteField
                                        { FieldName = head.HeadName
                                          WireName = head.HeadWire
                                          Optional = head.HeadOptional
                                          FieldType = fieldType
                                          Constraints = constraints
                                          Default = defaultValue
                                          Doc = doc
                                          Annotations = annotations
                                          FieldLine = line }
                                )
                            | token :: _ -> Error $"unexpected {describeToken token} at the end of the field line"))))

    let private parseUnionCaseLine line tokens =
        match tokens with
        | TIdent tag :: TPunct ":" :: TIdent name :: TPunct "." :: TIdent versionText :: [] ->
            match parseVersion versionText with
            | Ok version -> Ok { CaseTag = tag; CaseRef = { RefName = name; RefVersion = version }; CaseLine = line }
            | Error message -> Error message
        | TIdent _ :: TPunct ":" :: _ -> Error "union cases must reference a contract at a pinned version, like Circle.v1"
        | token :: _ -> Error $"expected a union case like 'tag: Contract.v1', found {describeToken token}"
        | [] -> Error "expected a union case like 'tag: Contract.v1'"

    let private parseAnnotation line tokens =
        match tokens with
        | TIdent name :: [] -> Ok { AnnotationName = name; AnnotationValue = None; AnnotationLine = line }
        | TIdent name :: rest ->
            parseLiteral rest
            |> Result.bind (fun (literal, remaining) ->
                match remaining with
                | [] -> Ok { AnnotationName = name; AnnotationValue = Some literal; AnnotationLine = line }
                | token :: _ -> Error $"unexpected {describeToken token} after the annotation value")
        | token :: _ -> Error $"expected an annotation name after '@', found {describeToken token}"
        | [] -> Error "expected an annotation name after '@'"

    let private parseContractHeader tokens =
        match tokens with
        | TIdent "contract" :: TIdent name :: TPunct "." :: TIdent versionText :: TPunct "{" :: [] ->
            match parseVersion versionText with
            | Ok version -> Ok(name, version)
            | Error message -> Error message
        | TIdent "contract" :: _ -> Error "expected a contract header like 'contract Name.v1 {'"
        | token :: _ -> Error $"expected 'contract', found {describeToken token}"
        | [] -> Error "expected 'contract'"

    /// <summary>Parses one contract source file. Returns every contract in declaration order, or every
    /// line-precise diagnostic found.</summary>
    let parse (filePath: string) (source: string) : Result<ContractFile, ContractDiagnostic list> =
        let lines = source.Replace("\r\n", "\n").Split '\n'
        let errors = ResizeArray<ContractDiagnostic>()
        let contracts = ResizeArray<ContractDecl>()

        let mutable pendingDoc: string list = []
        let mutable pendingAnnotations: Annotation list = []

        // (name, version, doc, annotations, headerLine, fields-so-far)
        let mutable openContract: (string * int * string list * Annotation list * int * ResizeArray<FieldDecl>) option = None

        // (field head, discriminator, doc, annotations, fieldLine, cases-so-far)
        let mutable openUnion: (FieldHead * string * string list * Annotation list * int * ResizeArray<UnionCaseDecl>) option = None

        let report line message =
            errors.Add { File = filePath; Line = line; Message = message }

        let takeDoc () =
            let doc = List.rev pendingDoc
            pendingDoc <- []
            doc

        let takeAnnotations () =
            let annotations = List.rev pendingAnnotations
            pendingAnnotations <- []
            annotations

        for lineIndex in 0 .. lines.Length - 1 do
            let lineNumber = lineIndex + 1
            let trimmed = lines.[lineIndex].Trim()

            if trimmed = "" then
                ()
            elif trimmed.StartsWith "///" then
                pendingDoc <- trimmed.Substring(3).Trim() :: pendingDoc
            elif trimmed.StartsWith "//" then
                ()
            elif trimmed.StartsWith "@" then
                match tokenizeLine (trimmed.Substring 1) with
                | Error message -> report lineNumber message
                | Ok tokens ->
                    match parseAnnotation lineNumber tokens with
                    | Error message -> report lineNumber message
                    | Ok annotation -> pendingAnnotations <- annotation :: pendingAnnotations
            else
                match tokenizeLine trimmed with
                | Error message -> report lineNumber message
                | Ok tokens ->
                    match openUnion, openContract with
                    | Some(head, discriminator, doc, annotations, fieldLine, cases), Some(name, version, contractDoc, contractAnnotations, headerLine, fields) ->
                        match tokens with
                        | [ TPunct "}" ] ->
                            fields.Add
                                { FieldName = head.HeadName
                                  WireName = head.HeadWire
                                  Optional = head.HeadOptional
                                  FieldType = UnionBlock(discriminator, List.ofSeq cases)
                                  Constraints = []
                                  Default = None
                                  Doc = doc
                                  Annotations = annotations
                                  FieldLine = fieldLine }

                            openUnion <- None
                            ignore (name, version, contractDoc, contractAnnotations, headerLine)
                        | _ ->
                            pendingDoc <- []
                            pendingAnnotations <- []

                            match parseUnionCaseLine lineNumber tokens with
                            | Error message -> report lineNumber message
                            | Ok case -> cases.Add case
                    | Some _, None ->
                        report lineNumber "internal parser state error: union block outside a contract"
                        openUnion <- None
                    | None, Some(name, version, doc, annotations, headerLine, fields) ->
                        match tokens with
                        | [ TPunct "}" ] ->
                            contracts.Add
                                { ContractName = name
                                  Version = version
                                  Doc = doc
                                  Annotations = annotations
                                  Fields = List.ofSeq fields
                                  OwnsType = true
                                  ExternalTypeName = None
                                  Constructor = None
                                  ContractLine = headerLine }

                            openContract <- None
                        | _ ->
                            let fieldDoc = takeDoc ()
                            let fieldAnnotations = takeAnnotations ()

                            match parseFieldLine lineNumber fieldDoc fieldAnnotations tokens with
                            | Error message -> report lineNumber message
                            | Ok(CompleteField field) -> fields.Add field
                            | Ok(OpenUnion(head, discriminator)) ->
                                openUnion <- Some(head, discriminator, fieldDoc, fieldAnnotations, lineNumber, ResizeArray())
                    | None, None ->
                        let doc = takeDoc ()
                        let annotations = takeAnnotations ()

                        match parseContractHeader tokens with
                        | Error message -> report lineNumber message
                        | Ok(name, version) ->
                            openContract <- Some(name, version, doc, annotations, lineNumber, ResizeArray())

        match openUnion with
        | Some(head, _, _, _, fieldLine, _) ->
            report fieldLine $"union block for field '{head.HeadName}' is never closed; expected '}}'"
        | None -> ()

        match openContract with
        | Some(name, _, _, _, headerLine, _) ->
            report headerLine $"contract '{name}' is never closed; expected '}}'"
        | None -> ()

        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            Ok { FilePath = filePath; Namespace = None; Contracts = List.ofSeq contracts }
