namespace Axial.Schema.Contracts

open System
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Xml

/// How field names and nullary union case names become wire names when no [<WireName>] override is present.
[<RequireQualifiedAccess>]
type WireNaming =
    | CamelCase
    | SnakeCase
    | Verbatim

/// <summary>The record frontend: parses F# source with the F# compiler's syntax tree (no type checking) and
/// lowers <c>[&lt;WireSchema&gt;]</c>-marked records into the same <c>ContractDecl</c> AST the .contract
/// parser produces, so both entry points share the resolver and emitter. Attributes and literals are read
/// from source text; nothing runs at runtime.</summary>
[<RequireQualifiedAccess>]
module Records =

    let private checker = lazy (FSharpChecker.Create())

    let private wireName naming (name: string) =
        match naming with
        | WireNaming.Verbatim -> name
        | WireNaming.CamelCase ->
            if name.Length = 0 then name
            else string (Char.ToLowerInvariant name.[0]) + name.Substring 1
        | WireNaming.SnakeCase ->
            let builder = Text.StringBuilder()

            for index in 0 .. name.Length - 1 do
                let c = name.[index]

                if Char.IsUpper c then
                    if index > 0 then builder.Append '_' |> ignore
                    builder.Append(Char.ToLowerInvariant c) |> ignore
                else
                    builder.Append c |> ignore

            builder.ToString()

    let private identText (SynLongIdent(ids, _, _)) =
        ids |> List.map _.idText |> String.concat "."

    let private lastIdent (SynLongIdent(ids, _, _)) = (List.last ids).idText

    let private docLines (xmlDoc: PreXmlDoc) =
        xmlDoc.ToXmlDoc(false, None).UnprocessedLines
        |> Array.map _.Trim()
        |> Array.filter (fun line -> line <> "")
        |> List.ofArray

    /// All attributes on a declaration, with the "Attribute" suffix stripped from the name.
    let private attributesOf (attributeLists: SynAttributes) =
        attributeLists
        |> List.collect _.Attributes
        |> List.map (fun attribute ->
            let name = lastIdent attribute.TypeName
            let name = if name.EndsWith "Attribute" then name.Substring(0, name.Length - 9) else name
            name, attribute)

    /// Extracts the positional literal arguments and named arguments of one attribute.
    let private attributeArgs (source: ISourceText) (attribute: SynAttribute) =
        let literalOf (expr: SynExpr) =
            match expr with
            | SynExpr.Const(constant, range) ->
                match constant with
                | SynConst.String(text, _, _) -> Some(LString text)
                | SynConst.Bool value -> Some(LBool value)
                | SynConst.Int32 value -> Some(LInt value)
                | SynConst.Double _
                | SynConst.Decimal _ ->
                    // Read the literal from source text so decimal precision is exact.
                    let text = source.GetSubTextFromRange range

                    match Decimal.TryParse(text, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture) with
                    | true, value -> Some(LDecimal value)
                    | false, _ -> None
                | _ -> None
            | _ -> None

        let namedArg (expr: SynExpr) =
            match expr with
            | SynExpr.App(_, _, SynExpr.App(_, true, SynExpr.LongIdent(_, operator, _, _), SynExpr.Ident name, _), value, _) when
                lastIdent operator = "op_Equality"
                ->
                Some(name.idText, value)
            | SynExpr.App(_, _, SynExpr.App(_, true, SynExpr.Ident operator, SynExpr.Ident name, _), value, _) when
                operator.idText = "op_Equality"
                ->
                Some(name.idText, value)
            | _ -> None

        let elements =
            match attribute.ArgExpr with
            | SynExpr.Paren(SynExpr.Tuple(_, elements, _, _), _, _, _) -> elements
            | SynExpr.Paren(single, _, _, _) -> [ single ]
            | SynExpr.Const(SynConst.Unit, _) -> []
            | other -> [ other ]

        let positional = elements |> List.filter (fun element -> namedArg element |> Option.isNone) |> List.choose literalOf
        let named = elements |> List.choose namedArg
        positional, named

    // ---- pass 1: catalog the file's marked records and its unions ----

    type private RecordInfo =
        { FsName: string
          Chain: string
          Version: int
          RecordDoc: string list
          RecordFields: SynField list
          RecordLine: int }

    type private UnionInfo =
        { UnionFsName: string
          Discriminator: string option
          UnionCases: SynUnionCase list
          UnionLine: int }

    let private chainOf (source: ISourceText) (attribute: SynAttribute) (fsName: string) (markedNames: Set<string>) =
        let _, named = attributeArgs source attribute

        let explicitChain =
            named
            |> List.tryPick (fun (name, value) ->
                match name, value with
                | "Chain", SynExpr.Const(SynConst.String(text, _, _), _) -> Some text
                | _ -> None)

        let explicitVersion =
            named
            |> List.tryPick (fun (name, value) ->
                match name, value with
                | "Version", SynExpr.Const(SynConst.Int32 value, _) -> Some value
                | _ -> None)

        match explicitChain, explicitVersion with
        | Some chain, Some version -> chain, version
        | Some chain, None -> chain, 1
        | None, Some version -> fsName, version
        | None, None ->
            // XxxVn is a superseded version of chain Xxx only when the bare chain name is also marked.
            let m: Match = Regex.Match(fsName, @"^(.+?)V([0-9]+)$")

            if m.Success && markedNames.Contains m.Groups.[1].Value then
                m.Groups.[1].Value, int m.Groups.[2].Value
            else
                fsName, 0 // resolved to latest+0 sentinel below

    /// <summary>Parses one F# source file and lowers its marked records. Returns a contract file whose
    /// <c>Contracts</c> list is empty when the file declares no <c>[&lt;WireSchema&gt;]</c> records.</summary>
    let parse (naming: WireNaming) (filePath: string) (sourceText: string) : Result<ContractFile, ContractDiagnostic list> =
        let source = SourceText.ofString sourceText
        let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| filePath |] }

        let parseResults =
            checker.Value.ParseFile(filePath, source, parsingOptions) |> Async.RunSynchronously

        let syntaxErrors =
            parseResults.Diagnostics
            |> Array.filter (fun diagnostic -> diagnostic.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            |> Array.map (fun diagnostic ->
                { File = filePath
                  Line = diagnostic.StartLine
                  Message = diagnostic.Message })
            |> List.ofArray

        if not (List.isEmpty syntaxErrors) then
            Error syntaxErrors
        else

        let diagnostics = ResizeArray<ContractDiagnostic>()
        let report line message = diagnostics.Add { File = filePath; Line = line; Message = message }

        // ---- walk the tree ----

        let mutable namespaceName: string option = None
        let records = ResizeArray<SynAttribute * SynComponentInfo * SynField list * PreXmlDoc * int>()
        let unions = Collections.Generic.Dictionary<string, UnionInfo>()

        let inspectTypeDefn (SynTypeDefn(componentInfo, typeRepr, _, _, range, _)) =
            let (SynComponentInfo(attributes, typeParams, _, longId, xmlDoc, _, accessibility, _)) = componentInfo
            let fsName = (List.last longId).idText
            let attrs = attributesOf attributes
            let wireSchema = attrs |> List.tryFind (fun (name, _) -> name = "WireSchema") |> Option.map snd
            let wireUnion = attrs |> List.tryPick (fun (name, attribute) -> if name = "WireUnion" then Some attribute else None)

            match typeRepr with
            | SynTypeDefnRepr.Simple(SynTypeDefnSimpleRepr.Record(reprAccess, fields, _), _) ->
                match wireSchema with
                | None -> ()
                | Some attribute ->
                    match typeParams with
                    | Some(SynTyparDecls.PostfixList(_ :: _, _, _))
                    | Some(SynTyparDecls.PrefixList(_ :: _, _)) ->
                        report range.StartLine $"wire DTO '{fsName}' cannot be generic"
                    | _ ->

                    match accessibility, reprAccess with
                    | Some _, _
                    | _, Some _ -> report range.StartLine $"wire DTO '{fsName}' must be public; wire records carry no invariants to protect"
                    | None, None -> records.Add(attribute, componentInfo, fields, xmlDoc, range.StartLine)
            | SynTypeDefnRepr.Simple(SynTypeDefnSimpleRepr.Union(_, cases, _), _) ->
                unions.[fsName] <-
                    { UnionFsName = fsName
                      Discriminator =
                        wireUnion
                        |> Option.bind (fun attribute ->
                            match attributeArgs source attribute with
                            | [ LString discriminator ], _ -> Some discriminator
                            | _ -> None)
                      UnionCases = cases
                      UnionLine = range.StartLine }

                if wireSchema.IsSome then
                    report range.StartLine
                        $"'{fsName}' is a union; [<WireSchema>] marks records — unions participate as field types (nullary cases as an enum, [<WireUnion>] for tagged payloads)"
            | _ ->
                if wireSchema.IsSome then
                    report range.StartLine $"[<WireSchema>] applies to record types; '{fsName}' is not a record"

        let rec inspectDecl nested (decl: SynModuleDecl) =
            match decl with
            | SynModuleDecl.Types(typeDefns, _) ->
                if nested then
                    for SynTypeDefn(SynComponentInfo(attributes, _, _, longId, _, _, _, _), _, _, _, range, _) in typeDefns do
                        if attributesOf attributes |> List.exists (fun (name, _) -> name = "WireSchema") then
                            report range.StartLine
                                $"wire DTO '{(List.last longId).idText}' must be declared at namespace level, not inside a module"
                else
                    List.iter inspectTypeDefn typeDefns
            | SynModuleDecl.NestedModule(_, _, decls, _, _, _) -> List.iter (inspectDecl true) decls
            | _ -> ()

        match parseResults.ParseTree with
        | ParsedInput.SigFile _ -> ()
        | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
            for SynModuleOrNamespace(longId, _, kind, decls, _, _, _, range, _) in modules do
                match kind with
                | SynModuleOrNamespaceKind.DeclaredNamespace
                | SynModuleOrNamespaceKind.GlobalNamespace ->
                    let thisNamespace = longId |> List.map _.idText |> String.concat "."

                    match namespaceName with
                    | None ->
                        namespaceName <- Some thisNamespace
                        List.iter (inspectDecl false) decls
                    | Some first when first = thisNamespace -> List.iter (inspectDecl false) decls
                    | Some first ->
                        // The generated sibling file carries one namespace; marked records outside the
                        // file's first namespace would silently emit into the wrong one.
                        for decl in decls do
                            match decl with
                            | SynModuleDecl.Types(typeDefns, _) ->
                                for SynTypeDefn(SynComponentInfo(attributes, _, _, longId, _, _, _, _), _, _, _, typeRange, _) in typeDefns do
                                    if attributesOf attributes |> List.exists (fun (name, _) -> name = "WireSchema") then
                                        report typeRange.StartLine
                                            $"wire DTO '{(List.last longId).idText}' is in namespace '{thisNamespace}', but this file's wire schemas generate into '{first}'; keep one namespace per wire file"
                            | _ -> ()
                | SynModuleOrNamespaceKind.NamedModule
                | SynModuleOrNamespaceKind.AnonModule ->
                    let hasMarked =
                        decls
                        |> List.exists (fun decl ->
                            match decl with
                            | SynModuleDecl.Types(typeDefns, _) ->
                                typeDefns
                                |> List.exists (fun (SynTypeDefn(SynComponentInfo(attributes, _, _, _, _, _, _, _), _, _, _, _, _)) ->
                                    attributesOf attributes |> List.exists (fun (name, _) -> name = "WireSchema"))
                            | _ -> false)

                    if hasMarked then
                        report range.StartLine
                            "wire DTO files use a namespace declaration, not a top-level module, so the generated sibling file can share the namespace"

        // ---- pass 2: lower marked records in declaration order ----

        let markedNames =
            records |> Seq.map (fun (_, SynComponentInfo(longId = longId), _, _, _) -> (List.last longId).idText) |> Set.ofSeq

        let chains =
            records
            |> Seq.map (fun (attribute, SynComponentInfo(longId = longId), _, _, _) ->
                let fsName = (List.last longId).idText
                fsName, chainOf source attribute fsName markedNames)
            |> Map.ofSeq

        // A bare marked record whose chain has explicit or convention-derived siblings is the current
        // version: one past the highest sibling. A record alone in its chain is version 1.
        let resolvedChains =
            chains
            |> Map.map (fun _ (chain, version) ->
                if version > 0 then
                    chain, version
                else
                    let highestSibling =
                        chains
                        |> Map.toSeq
                        |> Seq.filter (fun (_, (siblingChain, siblingVersion)) -> siblingChain = chain && siblingVersion > 0)
                        |> Seq.map (fun (_, (_, siblingVersion)) -> siblingVersion)
                        |> Seq.fold max 0

                    chain, highestSibling + 1)

        let referenceTo line (name: string) : ContractRef option =
            match Map.tryFind name resolvedChains with
            | Some(chain, version) -> Some { RefName = chain; RefVersion = version }
            | None ->
                report line $"'{name}' is not a [<WireSchema>] record in this file; wire references stay within one file"
                None

        let rec lowerType line (synType: SynType) : FieldType option =
            match synType with
            | SynType.LongIdent longIdent ->
                match identText longIdent with
                | "string" -> Some(Primitive PText)
                | "int" -> Some(Primitive PInt)
                | "decimal" -> Some(Primitive PDecimal)
                | "bool" -> Some(Primitive PBool)
                | "DateOnly"
                | "System.DateOnly" -> Some(Primitive PDate)
                | "DateTimeOffset"
                | "System.DateTimeOffset" -> Some(Primitive PDateTime)
                | "Guid"
                | "System.Guid" -> Some(Primitive PGuid)
                | "float"
                | "double"
                | "single"
                | "float32" ->
                    report line "wire numbers are 'decimal' in the wire vocabulary; float fields are not supported"
                    None
                | "int64" | "uint64" | "int16" | "uint16" | "byte" | "sbyte" | "uint" | "uint32" ->
                    report line "wire integers are 'int' in the wire vocabulary"
                    None
                | name ->
                    let shortName = lastIdent longIdent

                    match unions.TryGetValue shortName with
                    | true, union -> lowerUnion line union
                    | false, _ ->
                        if markedNames.Contains shortName then
                            referenceTo line shortName |> Option.map Reference
                        else
                            report line $"unknown wire field type '{name}'"
                            None
            | SynType.App(typeName = SynType.LongIdent head; typeArgs = args) ->
                match lastIdent head, args with
                | "list", [ element ] -> lowerType line element |> Option.map ListOf
                | "option", [ _ ] ->
                    report line "nested options are not supported; '?' absence is one axis (mark the field itself optional)"
                    None
                | "Map", [ SynType.LongIdent key; value ] when identText key = "string" ->
                    lowerType line value |> Option.map MapOf
                | "Map", _ ->
                    report line "wire map keys are always 'string' (JSON object keys are strings)"
                    None
                | name, _ ->
                    report line $"unknown wire field type '{name}'"
                    None
            | SynType.Array _ ->
                report line "arrays are not supported in the wire vocabulary; use 'list'"
                None
            | _ ->
                report line "unsupported wire field type; the vocabulary is primitives, list, Map<string, _>, option, marked records, and unions"
                None

        and lowerUnion line (union: UnionInfo) : FieldType option =
            let caseName (SynUnionCase(ident = SynIdent(ident, _))) = ident.idText

            let caseWireName (SynUnionCase(attributes = attributes)) fallback =
                attributesOf attributes
                |> List.tryPick (fun (name, attribute) ->
                    if name = "WireName" then
                        match attributeArgs source attribute with
                        | [ LString wire ], _ -> Some wire
                        | _ -> None
                    else
                        None)
                |> Option.defaultValue fallback

            let allNullary =
                union.UnionCases
                |> List.forall (fun (SynUnionCase(caseType = caseType)) ->
                    match caseType with
                    | SynUnionCaseKind.Fields [] -> true
                    | _ -> false)

            match union.Discriminator with
            | None when allNullary ->
                let cases =
                    union.UnionCases
                    |> List.map (fun case ->
                        let fsCase = caseName case
                        { EnumTag = caseWireName case (wireName naming fsCase); EnumFsCase = fsCase })

                Some(ExternalEnum(union.UnionFsName, cases))
            | None ->
                report line
                    $"union '{union.UnionFsName}' has payload cases; mark it [<WireUnion \"discriminator\">] with one marked-record payload per case, or make every case nullary for an enum"
                None
            | Some discriminator ->
                let cases =
                    union.UnionCases
                    |> List.choose (fun case ->
                        let (SynUnionCase(caseType = caseType; range = caseRange)) = case
                        let fsCase = caseName case

                        match caseType with
                        | SynUnionCaseKind.Fields [ SynField(fieldType = SynType.LongIdent payload) ] when
                            markedNames.Contains(lastIdent payload)
                            ->
                            referenceTo caseRange.StartLine (lastIdent payload)
                            |> Option.map (fun reference ->
                                { ExtTag = caseWireName case (wireName naming fsCase)
                                  ExtFsCase = fsCase
                                  ExtRef = reference
                                  ExtLine = caseRange.StartLine })
                        | _ ->
                            report caseRange.StartLine
                                $"case '{fsCase}' of wire union '{union.UnionFsName}' must carry exactly one [<WireSchema>] record payload"
                            None)

                Some(ExternalUnion(union.UnionFsName, discriminator, cases))

        let lowerField (field: SynField) : FieldDecl option =
            let (SynField(attributes, _, idOpt, fieldType, _, xmlDoc, _, range, _)) = field
            let line = range.StartLine

            match idOpt with
            | None ->
                report line "wire record fields need names"
                None
            | Some ident ->
                let fieldName = ident.idText
                let attrs = attributesOf attributes

                let optional, payloadType =
                    match fieldType with
                    | SynType.App(typeName = SynType.LongIdent head; typeArgs = [ inner ]) when lastIdent head = "option" ->
                        true, inner
                    | other -> false, other

                match lowerType line payloadType with
                | None -> None
                | Some lowered ->
                    let firstLiteral (attribute: SynAttribute) =
                        match attributeArgs source attribute with
                        | literal :: _, _ -> Some literal
                        | [], _ -> None

                    let constraints =
                        attrs
                        |> List.choose (fun (name, attribute) ->
                            let literal = firstLiteral attribute

                            match name, literal with
                            | "Pattern", Some(LString pattern) -> Some(Pattern pattern)
                            | "Min", Some(LInt size) -> Some(MinSize size)
                            | "Max", Some(LInt size) -> Some(MaxSize size)
                            | "AtLeast", Some literal -> Some(AtLeast literal)
                            | "GreaterThan", Some literal -> Some(GreaterThan literal)
                            | "AtMost", Some literal -> Some(AtMost literal)
                            | "LessThan", Some literal -> Some(LessThan literal)
                            | "MultipleOf", Some literal -> Some(MultipleOf literal)
                            | "Distinct", _ -> Some Distinct
                            | ("Pattern" | "Min" | "Max" | "AtLeast" | "GreaterThan" | "AtMost" | "LessThan" | "MultipleOf"), _ ->
                                report line $"could not read the literal argument of [<{name}>]"
                                None
                            | _ -> None)
                        |> List.map (fun constraint' -> constraint', line)

                    let emailConstraint =
                        attrs |> List.exists (fun (name, _) -> name = "Email")

                    let lowered =
                        if emailConstraint then
                            match lowered with
                            | Primitive PText -> Primitive PEmail
                            | other ->
                                report line "[<Email>] applies to string fields"
                                other
                        else
                            lowered

                    let wireOverride =
                        attrs
                        |> List.tryPick (fun (name, attribute) ->
                            if name = "WireName" then
                                match attributeArgs source attribute with
                                | [ LString wire ], _ -> Some wire
                                | _ -> None
                            else
                                None)

                    let defaultValue =
                        attrs
                        |> List.tryPick (fun (name, attribute) -> if name = "Default" then firstLiteral attribute else None)

                    Some
                        { FieldName = fieldName
                          WireName = Some(wireOverride |> Option.defaultValue (wireName naming fieldName))
                          Optional = optional
                          FieldType = lowered
                          Constraints = constraints
                          Default = defaultValue
                          Doc = docLines xmlDoc
                          Annotations = []
                          FieldLine = line }

        let contracts =
            records
            |> Seq.map (fun (_, SynComponentInfo(longId = longId), fields, xmlDoc, headerLine) ->
                let fsName = (List.last longId).idText
                let chain, version = Map.find fsName resolvedChains

                { ContractName = chain
                  Version = version
                  Doc = docLines xmlDoc
                  Annotations = []
                  Fields = fields |> List.choose lowerField
                  OwnsType = false
                  ExternalTypeName = Some fsName
                  ContractLine = headerLine })
            |> List.ofSeq

        if diagnostics.Count > 0 then
            Error(List.ofSeq diagnostics)
        else
            Ok
                { FilePath = filePath
                  Namespace = namespaceName
                  Contracts = contracts }
