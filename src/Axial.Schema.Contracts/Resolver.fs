namespace Axial.Schema.Contracts

/// <summary>Semantic validation over parsed contract files: reference resolution, constraint/type
/// compatibility, defaults, and declaration ordering. A file set that resolves cleanly is safe to emit.</summary>
[<RequireQualifiedAccess>]
module Resolver =

    let private fsharpKeywords =
        set
            [ "abstract"; "and"; "as"; "assert"; "base"; "begin"; "class"; "default"; "delegate"; "do"; "done"
              "downcast"; "downto"; "elif"; "else"; "end"; "exception"; "extern"; "false"; "finally"; "fixed"
              "for"; "fun"; "function"; "global"; "if"; "in"; "inherit"; "inline"; "interface"; "internal"
              "lazy"; "let"; "match"; "member"; "module"; "mutable"; "namespace"; "new"; "not"; "null"; "of"
              "open"; "or"; "override"; "private"; "public"; "rec"; "return"; "select"; "static"; "struct"
              "then"; "to"; "true"; "try"; "type"; "upcast"; "use"; "val"; "void"; "when"; "while"; "with"
              "yield"; "const"; "atomic"; "break"; "checked"; "component"; "constraint"; "constructor"
              "continue"; "eager"; "event"; "external"; "functor"; "include"; "method"; "mixin"; "object"
              "parallel"; "process"; "protected"; "pure"; "sealed"; "tailcall"; "trait"; "virtual" ]

    let private pascal (name: string) =
        if name.Length = 0 then name else string (System.Char.ToUpperInvariant name.[0]) + name.Substring 1

    let private camel (name: string) =
        if name.Length = 0 then name else string (System.Char.ToLowerInvariant name.[0]) + name.Substring 1

    /// The kinds a constraint list is judged against.
    type private TypeKind =
        | KText
        | KInt
        | KDecimal
        | KBool
        | KOther of description: string
        | KSized of item: string // list/map
        | KEnum
        | KUnion
        | KReference

    let private kindOf fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> KText
        | Primitive PInt -> KInt
        | Primitive PDecimal -> KDecimal
        | Primitive PBool -> KBool
        | Primitive PDate -> KOther "date"
        | Primitive PDateTime -> KOther "dateTime"
        | Primitive PGuid -> KOther "guid"
        | ListOf _ -> KSized "list"
        | MapOf _ -> KSized "map"
        | LiteralUnion _ -> KEnum
        | UnionBlock _ -> KUnion
        | Reference _ -> KReference

    let private describeKind kind =
        match kind with
        | KText -> "text"
        | KInt -> "int"
        | KDecimal -> "decimal"
        | KBool -> "bool"
        | KOther name -> name
        | KSized name -> name
        | KEnum -> "a literal union"
        | KUnion -> "a union"
        | KReference -> "a contract reference"

    let private literalDescription literal =
        match literal with
        | LString _ -> "a string"
        | LInt _ -> "a whole number"
        | LDecimal _ -> "a decimal number"
        | LBool _ -> "a boolean"

    let private checkNumericLiteral kind constraintName literal =
        match kind, literal with
        | KInt, LInt _ -> None
        | KInt, _ -> Some $"'{constraintName}' on an int field takes a whole number, found {literalDescription literal}"
        | KDecimal, (LInt _ | LDecimal _) -> None
        | KDecimal, _ -> Some $"'{constraintName}' on a decimal field takes a number, found {literalDescription literal}"
        | _ -> Some $"'{constraintName}' applies to int or decimal fields, not {describeKind kind}"

    let private checkConstraint kind (constraint': ConstraintDecl) =
        match constraint' with
        | AtLeast literal -> checkNumericLiteral kind ">=" literal
        | GreaterThan literal -> checkNumericLiteral kind ">" literal
        | AtMost literal -> checkNumericLiteral kind "<=" literal
        | LessThan literal -> checkNumericLiteral kind "<" literal
        | MultipleOf literal -> checkNumericLiteral kind "multipleOf" literal
        | MinSize _
        | MaxSize _ ->
            match kind with
            | KText
            | KSized _ -> None
            | _ -> Some $"'min'/'max' bound the size of text, list, or map fields, not {describeKind kind}"
        | Pattern _ ->
            match kind with
            | KText -> None
            | _ -> Some $"'pattern' applies to text fields, not {describeKind kind}"
        | Distinct ->
            match kind with
            | KSized "list" -> None
            | _ -> Some $"'distinct' applies to list fields, not {describeKind kind}"
        | CheckRef name -> Some $"'check {name}' references are not supported yet; use a refined type or a contextual rule"

    let private duCaseName (text: string) =
        let parts =
            text.Split([| '-'; '_'; ' ' |], System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun part ->
                if part.Length = 0 then part
                else string (System.Char.ToUpperInvariant part.[0]) + part.Substring 1)

        System.String.Concat parts

    let private knownAnnotations = set [ "deprecated"; "strict"; "open"; "example"; "readOnly" ]

    /// <summary>Validates a set of parsed contract files as one resolution unit. Returns every diagnostic
    /// found; an empty list means the set is safe to emit.</summary>
    let resolve (files: ContractFile list) : ContractDiagnostic list =
        let diagnostics = ResizeArray<ContractDiagnostic>()

        let report file line message =
            diagnostics.Add { File = file; Line = line; Message = message }

        // Global contract registry: duplicate names (any version) are rejected — multiple live
        // versions of one contract are Contract-machinery territory, deliberately not generation.
        let registry = System.Collections.Generic.Dictionary<string, string * int * int>()

        for file in files do
            for contract in file.Contracts do
                if contract.ContractName = "_" || fsharpKeywords.Contains contract.ContractName then
                    report file.FilePath contract.ContractLine
                        $"contract name '{contract.ContractName}' cannot safely name a generated F# type and module"

                if List.isEmpty contract.Fields then
                    report file.FilePath contract.ContractLine "contracts need at least one field"

                match registry.TryGetValue contract.ContractName with
                | true, (existingFile, existingLine, existingVersion) ->
                    if existingVersion = contract.Version then
                        report file.FilePath contract.ContractLine
                            $"contract '{contract.ContractName}.v{contract.Version}' is already declared at {existingFile}({existingLine})"
                    else
                        report file.FilePath contract.ContractLine
                            $"multiple versions of contract '{contract.ContractName}' are not supported by generation yet; superseded versions are frozen hand-written code (see the contract versioning sketch)"
                | false, _ ->
                    registry.[contract.ContractName] <- (file.FilePath, contract.ContractLine, contract.Version)

        let resolveReference file line (reference: ContractRef) =
            match registry.TryGetValue reference.RefName with
            | true, (_, _, version) when version = reference.RefVersion -> ()
            | true, (_, _, version) ->
                report file line $"contract '{reference.RefName}' is declared at v{version}, but the reference pins v{reference.RefVersion}"
            | false, _ ->
                report file line $"unknown contract reference '{reference.RefName}.v{reference.RefVersion}'"

        for file in files do
            // Same-file declaration order: a reference must point at a contract declared earlier in
            // its own file, because the emitted F# compiles top to bottom.
            let declaredSoFar = System.Collections.Generic.HashSet<string>()

            for contract in file.Contracts do
                let checkOrder line (reference: ContractRef) =
                    let declaredInFile =
                        file.Contracts |> List.exists (fun candidate -> candidate.ContractName = reference.RefName)

                    let isSelf = reference.RefName = contract.ContractName && reference.RefVersion = contract.Version

                    if declaredInFile && not isSelf && not (declaredSoFar.Contains reference.RefName) then
                        report file.FilePath line
                            $"'{reference.RefName}' must be declared before '{contract.ContractName}' uses it"

                for annotation in contract.Annotations do
                    if not (knownAnnotations.Contains annotation.AnnotationName) then
                        report file.FilePath annotation.AnnotationLine $"unknown annotation '@{annotation.AnnotationName}'"

                let wireNames = System.Collections.Generic.HashSet<string>()
                let fieldNames = System.Collections.Generic.HashSet<string>()
                let generatedFieldNames = System.Collections.Generic.HashSet<string>()
                let generatedBindingNames = System.Collections.Generic.HashSet<string>()

                for field in contract.Fields do
                    if field.FieldName = "_" then
                        report file.FilePath field.FieldLine "field name '_' cannot safely name a generated F# field or binding"

                    if not (fieldNames.Add field.FieldName) then
                        report file.FilePath field.FieldLine $"duplicate field name '{field.FieldName}'"

                    let generatedFieldName = pascal field.FieldName
                    if not (generatedFieldNames.Add generatedFieldName) then
                        report file.FilePath field.FieldLine $"duplicate generated field name '{generatedFieldName}'"

                    let generatedBindingName = camel field.FieldName
                    if not (generatedBindingNames.Add generatedBindingName) then
                        report file.FilePath field.FieldLine $"duplicate generated binding name '{generatedBindingName}'"

                    let wire = FieldDecl.wireName field

                    if not (wireNames.Add wire) then
                        report file.FilePath field.FieldLine $"duplicate wire name '{wire}'"

                    for annotation in field.Annotations do
                        if not (knownAnnotations.Contains annotation.AnnotationName) then
                            report file.FilePath annotation.AnnotationLine $"unknown annotation '@{annotation.AnnotationName}'"

                    // Type-level checks.
                    let rec checkType fieldType =
                        match fieldType with
                        | Primitive _ -> ()
                        | Reference reference ->
                            resolveReference file.FilePath field.FieldLine reference
                            checkOrder field.FieldLine reference
                        | ListOf element
                        | MapOf element -> checkType element
                        | LiteralUnion cases ->
                            if List.isEmpty cases then
                                report file.FilePath field.FieldLine "literal unions need at least one case"

                            if cases |> List.exists System.String.IsNullOrWhiteSpace then
                                report file.FilePath field.FieldLine "literal union cases cannot be blank"

                            let duplicateTags =
                                cases |> List.countBy id |> List.filter (fun (_, count) -> count > 1)

                            for tag, _ in duplicateTags do
                                report file.FilePath field.FieldLine $"duplicate literal union case \"{tag}\""

                            let duplicateCaseNames =
                                cases |> List.countBy duCaseName |> List.filter (fun (_, count) -> count > 1)

                            for name, _ in duplicateCaseNames do
                                report file.FilePath field.FieldLine
                                    $"literal union cases collide on the generated case name '{name}'"
                        | UnionBlock(_, cases) ->
                            if List.isEmpty cases then
                                report file.FilePath field.FieldLine "union blocks need at least one case"

                            let duplicateTags =
                                cases |> List.countBy _.CaseTag |> List.filter (fun (_, count) -> count > 1)

                            for tag, _ in duplicateTags do
                                report file.FilePath field.FieldLine $"duplicate union case tag '{tag}'"

                            let duplicateCaseNames =
                                cases
                                |> List.countBy (fun case -> duCaseName case.CaseTag)
                                |> List.filter (fun (_, count) -> count > 1)

                            for name, _ in duplicateCaseNames do
                                report file.FilePath field.FieldLine
                                    $"union case tags collide on the generated case name '{name}'"

                            for case in cases do
                                resolveReference file.FilePath case.CaseLine case.CaseRef
                                checkOrder case.CaseLine case.CaseRef

                                if case.CaseRef.RefName = contract.ContractName && case.CaseRef.RefVersion = contract.Version then
                                    report file.FilePath case.CaseLine "recursive union-block payloads are not supported; use a recursive field reference"

                    checkType field.FieldType

                    // Constraint compatibility. Constraints on optional fields judge the payload type.
                    let kind = kindOf field.FieldType

                    match field.FieldType, field.Constraints with
                    | (Reference _ | UnionBlock _), (_ :: _) ->
                        report file.FilePath field.FieldLine
                            $"constraints on {describeKind kind} fields are not supported; declare them on the referenced contract's own fields"
                    | (LiteralUnion _), (_ :: _) ->
                        report file.FilePath field.FieldLine
                            "constraints on literal union fields are not supported; the case list is already the constraint"
                    | _ ->
                        for constraint', line in field.Constraints do
                            match checkConstraint kind constraint' with
                            | Some message -> report file.FilePath line message
                            | None -> ()

                    // Defaults.
                    match field.Default with
                    | None -> ()
                    | Some _ when field.Optional ->
                        report file.FilePath field.FieldLine
                            "optional fields cannot carry a default; absence already parses to None"
                    | Some literal ->
                        let defaultProblem =
                            match field.FieldType, literal with
                            | Primitive (PText | PEmail), LString _ -> None
                            | Primitive PInt, LInt _ -> None
                            | Primitive PDecimal, (LInt _ | LDecimal _) -> None
                            | Primitive PBool, LBool _ -> None
                            | LiteralUnion cases, LString value ->
                                if cases |> List.contains value then
                                    None
                                else
                                    Some $"default \"{value}\" is not one of the literal union cases"
                            | Primitive (PText | PEmail), _ -> Some $"a text default must be a string, found {literalDescription literal}"
                            | Primitive PInt, _ -> Some $"an int default must be a whole number, found {literalDescription literal}"
                            | Primitive PDecimal, _ -> Some $"a decimal default must be a number, found {literalDescription literal}"
                            | Primitive PBool, _ -> Some $"a bool default must be true or false, found {literalDescription literal}"
                            | LiteralUnion _, _ -> Some "a literal union default must be one of the quoted cases"
                            | _ -> Some $"defaults on {describeKind kind} fields are not supported"

                        match defaultProblem with
                        | Some message -> report file.FilePath field.FieldLine message
                        | None -> ()

                declaredSoFar.Add contract.ContractName |> ignore

        List.ofSeq diagnostics
