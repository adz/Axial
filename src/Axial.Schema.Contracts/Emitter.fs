namespace Axial.Schema.Contracts

open System
open System.Text

/// <summary>Emits the checked-in F# for a resolved contract file: one public record, schema, validate,
/// parse, and Fields module per contract, in declaration order.</summary>
[<RequireQualifiedAccess>]
module Emitter =

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

    let private escapeIdent (name: string) =
        if fsharpKeywords.Contains name then $"``{name}``" else name

    let private pascal (name: string) =
        if name.Length = 0 then name
        else string (Char.ToUpperInvariant name.[0]) + name.Substring 1

    let private camel (name: string) =
        if name.Length = 0 then name
        else string (Char.ToLowerInvariant name.[0]) + name.Substring 1

    let private duCaseName (text: string) =
        text.Split([| '-'; '_'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map pascal
        |> String.Concat

    let private escapeString (value: string) =
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t")

    /// The generated DU name for a literal-union or union-block field.
    let private caseTypeName contractName fieldName = contractName + pascal fieldName

    let private renderNumericLiteral (kind: PrimitiveType) literal =
        let suffix =
            match kind with
            | PDecimal -> "m"
            | _ -> ""

        match literal with
        | LInt value -> string value + suffix
        | LDecimal value -> value.ToString(Globalization.CultureInfo.InvariantCulture) + "m"
        | LString value -> $"\"{escapeString value}\""
        | LBool value -> if value then "true" else "false"

    let rec private numericKind fieldType =
        match fieldType with
        | Primitive PDecimal -> PDecimal
        | _ -> PInt

    /// Renders one Constraint expression for the constraint list.
    let private renderConstraint fieldType constraint' =
        let sized minName maxName size =
            match constraint' with
            | MinSize n -> $"Constraint.{minName} ({n})"
            | MaxSize n -> $"Constraint.{maxName} ({n})"
            | _ -> size

        match constraint' with
        | AtLeast literal -> $"Constraint.atLeast ({renderNumericLiteral (numericKind fieldType) literal})"
        | GreaterThan literal -> $"Constraint.greaterThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | AtMost literal -> $"Constraint.atMost ({renderNumericLiteral (numericKind fieldType) literal})"
        | LessThan literal -> $"Constraint.lessThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | MultipleOf literal -> $"Constraint.multipleOf ({renderNumericLiteral (numericKind fieldType) literal})"
        | MinSize _
        | MaxSize _ ->
            match fieldType with
            | ListOf _
            | MapOf _ -> sized "minCount" "maxCount" ""
            | _ -> sized "minLength" "maxLength" ""
        | Pattern value -> $"Constraint.pattern (\"{escapeString value}\")"
        | Distinct -> "Constraint.distinct"
        | CheckRef name -> failwith $"check reference '{name}' should have been rejected by the resolver"

    /// The F# type of a field as written in the record and FieldRef declarations.
    let rec private fsType contractName fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "string"
        | Primitive PInt -> "int"
        | Primitive PDecimal -> "decimal"
        | Primitive PBool -> "bool"
        | Primitive PDate -> "System.DateOnly"
        | Primitive PDateTime -> "System.DateTimeOffset"
        | Primitive PGuid -> "System.Guid"
        | Reference reference -> reference.RefName
        | ListOf element -> $"{fsType contractName fieldName element} list"
        | MapOf element -> $"Map<string, {fsType contractName fieldName element}>"
        | LiteralUnion _
        | UnionBlock _ -> caseTypeName contractName fieldName

    /// The base Schema.* expression for a field's type, before decorations.
    let rec private baseValueExpr contractName fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "Schema.text"
        | Primitive PInt -> "Schema.int"
        | Primitive PDecimal -> "Schema.decimal"
        | Primitive PBool -> "Schema.bool"
        | Primitive PDate -> "Schema.date"
        | Primitive PDateTime -> "Schema.dateTime"
        | Primitive PGuid -> "Schema.guid"
        | Reference reference when reference.RefName = contractName -> "Schema.defer (fun () -> schema)"
        | Reference reference -> $"{reference.RefName}.schema"
        | ListOf element -> $"Schema.list {parenthesize (baseValueExpr contractName fieldName element)}"
        | MapOf element -> $"Schema.map {parenthesize (baseValueExpr contractName fieldName element)}"
        | LiteralUnion _ -> $"Schema.enum {camel fieldName}Cases"
        | UnionBlock(discriminator, _) -> $"Schema.inlineUnion \"{escapeString discriminator}\" {camel fieldName}Cases"

    and private parenthesize (expression: string) =
        if expression.Contains " " then $"({expression})" else expression

    let private joinedDoc (doc: string list) = String.Join(" ", doc)

    let private renderDefault fieldType literal =
        match fieldType, literal with
        | LiteralUnion _, LString value -> failwith $"literal union defaults are rendered by the caller, got \"{value}\""
        | _, LString value -> $"\"{escapeString value}\""
        | _, LBool value -> (if value then "true" else "false")
        | _, (LInt _ | LDecimal _) -> renderNumericLiteral (numericKind fieldType) literal

    /// Renders the complete value expression for one field's schema pipe (excluding field-level constraints).
    let private valueExpr contractName (field: FieldDecl) =
        let mutable expression = baseValueExpr contractName field.FieldName field.FieldType

        if not (List.isEmpty field.Constraints) then
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "Constraint.email" ]
                | _ -> []

            let rendered =
                emailPrefix @ (field.Constraints |> List.map (fun (constraint', _) -> renderConstraint field.FieldType constraint'))

            let joined = String.Join("; ", rendered)
            expression <- $"{expression} |> Schema.constrainAll [ {joined} ]"
        else
            match field.FieldType with
            | Primitive PEmail -> expression <- $"{expression} |> Schema.constrainAll [ Constraint.email ]"
            | _ -> ()

        match field.Doc with
        | [] -> ()
        | doc -> expression <- $"{expression} |> Schema.describe \"{escapeString (joinedDoc doc)}\""

        match field.Default with
        | None -> ()
        | Some literal ->
            let renderedDefault =
                match field.FieldType, literal with
                | LiteralUnion _, LString value -> $"{caseTypeName contractName field.FieldName}.{duCaseName value}"
                | _ -> renderDefault field.FieldType literal

            expression <- $"{expression} |> Schema.withDefault {renderedDefault}"

        if field.Optional then
            $"Schema.option {parenthesize expression}"
        else
            expression

    let private fieldLevelConstraints (field: FieldDecl) =
        if field.Optional then
            []
        else
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "Constraint.email" ]
                | _ -> []

            emailPrefix
            @ (field.Constraints |> List.map (fun (constraint', _) -> renderConstraint field.FieldType constraint'))

    let private fieldTypeOf (field: FieldDecl) =
        let inner = field.FieldType
        inner

    /// Emits one contract file as F# source text.
    let emit (namespaceName: string) (file: ContractFile) : string =
        let builder = StringBuilder()
        let line (text: string) = builder.AppendLine text |> ignore
        let fileName = IO.Path.GetFileName file.FilePath

        let versionList =
            file.Contracts
            |> List.map (fun contract -> $"{contract.ContractName}.v{contract.Version}")
            |> fun names -> String.Join(", ", names)

        line "// <auto-generated>"
        line $"//   Generated by axial schemagen from {fileName} ({versionList})."
        line "//   Do not edit directly; edit the contract source and regenerate."
        line "// </auto-generated>"
        line $"namespace {namespaceName}"
        line ""
        line "open Axial.Validation"
        line "open Axial.Schema"

        for contract in file.Contracts do
            let contractRef = $"{fileName}, {contract.ContractName}.v{contract.Version}"

            let rec hasSelfReference fieldType =
                match fieldType with
                | Reference reference -> reference.RefName = contract.ContractName && reference.RefVersion = contract.Version
                | ListOf element
                | MapOf element -> hasSelfReference element
                | UnionBlock(_, cases) ->
                    cases |> List.exists (fun case -> case.CaseRef.RefName = contract.ContractName && case.CaseRef.RefVersion = contract.Version)
                | Primitive _
                | LiteralUnion _ -> false

            let caseFields =
                contract.Fields
                |> List.filter (fun field ->
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _ -> true
                    | _ -> false)

            // Case DUs come before the record that uses them.
            for field in caseFields do
                line ""
                line $"/// The \"{field.FieldName}\" cases of {contract.ContractName} ({contractRef})."
                line "[<RequireQualifiedAccess>]"
                line $"type {caseTypeName contract.ContractName field.FieldName} ="

                match field.FieldType with
                | LiteralUnion cases ->
                    for case in cases do
                        line $"    | {duCaseName case}"
                | UnionBlock(_, cases) ->
                    for case in cases do
                        line $"    | {duCaseName case.CaseTag} of {case.CaseRef.RefName}"
                | _ -> ()

            line ""

            for doc in contract.Doc do
                line $"/// {doc}"

            line $"type {contract.ContractName} ="
            line "    {"

            for field in contract.Fields do
                for doc in field.Doc do
                    line $"        /// {doc}"

                let optionSuffix = if field.Optional then " option" else ""
                line $"        {escapeIdent (pascal field.FieldName)}: {fsType contract.ContractName field.FieldName (fieldTypeOf field)}{optionSuffix}"

            line "    }"
            line ""
            line $"/// Schema and boundary functions for {contract.ContractName} ({contractRef})."
            line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {contract.ContractName} ="

            for field in caseFields do
                line ""
                line $"    let private {camel field.FieldName}Cases ="

                match field.FieldType with
                | LiteralUnion cases ->
                    let du = caseTypeName contract.ContractName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case}\" {du}.{duCaseName case}{closer}")
                | UnionBlock(_, cases) ->
                    let du = caseTypeName contract.ContractName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""

                        let extractor =
                            if List.length cases = 1 then
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload)"
                            else
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload | _ -> None)"

                        line
                            $"        {opener}UnionCase.create \"{escapeString case.CaseTag}\" {du}.{duCaseName case.CaseTag} {extractor} {case.CaseRef.RefName}.schema{closer}")
                | _ -> ()

            line ""
            line $"    /// The schema declared by {fileName} ({contract.ContractName}.v{contract.Version})."
            let recursion = if contract.Fields |> List.exists (fun field -> hasSelfReference field.FieldType) then " rec" else ""
            line $"    let{recursion} schema : Schema<{contract.ContractName}> ="

            let parameters =
                contract.Fields
                |> List.map (fun field -> escapeIdent (camel field.FieldName))
                |> fun names -> String.Join(" ", names)

            line $"        Schema.recordFor<{contract.ContractName}, _> (fun {parameters} ->"

            contract.Fields
            |> List.iteri (fun index field ->
                let opener = if index = 0 then "{ " else "  "
                let closer = if index = List.length contract.Fields - 1 then " })" else ""
                line $"            {opener}{escapeIdent (pascal field.FieldName)} = {escapeIdent (camel field.FieldName)}{closer}")

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let getter = $"_.{escapeIdent (pascal field.FieldName)}"
                let value = valueExpr contract.ContractName field

                line $"        |> Schema.field \"{escapeString wire}\" {getter} {parenthesize value}"

            line "        |> Schema.build"

            match contract.Doc with
            | [] -> ()
            | doc -> line $"        |> Schema.describe \"{escapeString (joinedDoc doc)}\""

            line ""
            line "    /// Checks a draft built with an ordinary record literal."
            line $"    let validate (draft: {contract.ContractName}) : Result<{contract.ContractName}, Diagnostics<SchemaError>> ="
            line "        Schema.check schema draft"
            line ""
            line "    /// Parses raw boundary input through the schema."
            line $"    let parse (input: RawInput) : ParsedInput<{contract.ContractName}, SchemaError> ="
            line "        Schema.parse schema input"
            line ""
            line "    /// Typed field references for rules, redisplay, and UI binding."
            line "    [<RequireQualifiedAccess>]"
            line "    module Fields ="

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let optionSuffix = if field.Optional then " option" else ""
                let fieldType = $"{fsType contract.ContractName field.FieldName (fieldTypeOf field)}{optionSuffix}"

                line
                    $"        let {escapeIdent (camel field.FieldName)} : FieldRef<{contract.ContractName}, {fieldType}> = {{ Name = \"{escapeString wire}\"; Get = _.{escapeIdent (pascal field.FieldName)}; Set = fun draft value -> {{ draft with {escapeIdent (pascal field.FieldName)} = value }} }}"

        builder.ToString().Replace("\r\n", "\n")
