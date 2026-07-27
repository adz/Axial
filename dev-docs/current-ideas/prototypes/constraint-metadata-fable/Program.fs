module ConstraintMetadataFablePrototype

type ConstraintArgument =
    | Text of string
    | Integer of int64
    | Decimal of decimal
    | Boolean of bool
    | List of ConstraintArgument list

type ConstraintDetails = { Code: string; Arguments: Map<string, ConstraintArgument> }

let sample =
    { Code = "affordable"
      Arguments = Map [ "currency", Text "GBP"; "maximum", Decimal 100M; "count", Integer 2L ] }

let structurallyEqual = sample = sample
