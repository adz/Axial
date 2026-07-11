namespace Axial.Schema.Contracts

/// <summary>A literal value in a contract declaration. Literals are the only values the grammar can express;
/// everything with semantics is a name resolved against F# code at generation time.</summary>
type Literal =
    | LString of string
    | LInt of int
    | LDecimal of decimal
    | LBool of bool

/// <summary>The primitive type heads the grammar knows. <c>Email</c> is a format type: it lowers to text plus the
/// <c>email</c> constraint.</summary>
type PrimitiveType =
    | PText
    | PInt
    | PDecimal
    | PBool
    | PDate
    | PDateTime
    | PGuid
    | PEmail

/// <summary>A reference to another contract at a pinned version, e.g. <c>Geo.v1</c>.</summary>
type ContractRef =
    { RefName: string
      RefVersion: int }

/// <summary>One case of an inline tagged union block.</summary>
type UnionCaseDecl =
    { CaseTag: string
      CaseRef: ContractRef
      CaseLine: int }

/// <summary>A field's declared type.</summary>
type FieldType =
    | Primitive of PrimitiveType
    | Reference of ContractRef
    | ListOf of FieldType
    | MapOf of FieldType
    | LiteralUnion of string list
    | UnionBlock of discriminator: string * cases: UnionCaseDecl list

/// <summary>One constraint from a field's <c>[ ... ]</c> list. Comparisons bound the value; <c>min</c>/<c>max</c>
/// bound the natural size of the type (text length, list/map count).</summary>
type ConstraintDecl =
    | AtLeast of Literal
    | GreaterThan of Literal
    | AtMost of Literal
    | LessThan of Literal
    | MinSize of int
    | MaxSize of int
    | Pattern of string
    | MultipleOf of Literal
    | Distinct
    | CheckRef of string

/// <summary>An <c>@name literal?</c> annotation attached to a contract or field.</summary>
type Annotation =
    { AnnotationName: string
      AnnotationValue: Literal option
      AnnotationLine: int }

/// <summary>One declared contract field.</summary>
type FieldDecl =
    { FieldName: string
      WireName: string option
      Optional: bool
      FieldType: FieldType
      Constraints: (ConstraintDecl * int) list
      Default: Literal option
      Doc: string list
      Annotations: Annotation list
      FieldLine: int }

/// <summary>One declared contract at one version.</summary>
type ContractDecl =
    { ContractName: string
      Version: int
      Doc: string list
      Annotations: Annotation list
      Fields: FieldDecl list
      ContractLine: int }

/// <summary>A parsed contract source file.</summary>
type ContractFile =
    { FilePath: string
      Contracts: ContractDecl list }

/// <summary>A line-precise parse or resolution problem.</summary>
type ContractDiagnostic =
    { File: string
      Line: int
      Message: string }

    override this.ToString() = $"{this.File}({this.Line}): error: {this.Message}"

module FieldDecl =
    /// The external (wire) name: the explicit `as "..."` rename when present, the field name otherwise.
    let wireName (field: FieldDecl) =
        field.WireName |> Option.defaultValue field.FieldName
