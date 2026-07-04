open System
open Axial.Flow
open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation

type ProbeFailure(message: string) =
    inherit Exception(message)

module Assert =
    let equal<'value when 'value : equality> (expected: 'value) (actual: 'value) =
        if actual <> expected then
            raise (ProbeFailure(sprintf "Expected %+A but got %+A." expected actual))

type Address =
    {
        Street: string
        City: string
    }

type Line =
    {
        Name: string
        Quantity: int
    }

type User =
    {
        Username: string
        Address: Address
        Lines: Line list
    }

type ProbeError =
    | UsernameRequired
    | UsernameTooShort
    | UsernameTooLong
    | CityRequired
    | LineNameRequired of index: int
    | LineQuantityInvalid of index: int

type SchemaContact =
    {
        Name: string
        Age: int
    }

type SchemaFieldSummary =
    {
        Order: int
        ExternalName: string
    }

type SummaryChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
    interface IFieldChainResult<'model, 'constructorIn, 'constructorOut> with
        member _.Value = value

type SummaryFactory<'model>() =
    interface IFieldChainFactory<'model, SchemaFieldSummary list> with
        member _.OnEnd() =
            SummaryChainResult<'model, 'constructor, 'constructor>(box ([]: SchemaFieldSummary list))
            :> IFieldChainResult<_, _, _>

        member _.OnField(order, field: Axial.Schema.Field<'model, 'field>, head) =
            let fields = head.Value :?> SchemaFieldSummary list
            let name = Axial.Schema.Field.externalName field |> ExternalFieldName.value
            let fieldSummary = { Order = order; ExternalName = name }

            SummaryChainResult<'model, 'constructorIn, 'next>(box (fields @ [ fieldSummary ]))
            :> IFieldChainResult<_, _, _>

        member _.OnComplete(_, chain) =
            chain.Value :?> SchemaFieldSummary list

type Field<'root, 'value> =
    {
        Path: PathSegment list
        Get: 'root -> 'value
    }

module Field =
    let ofGetter (path: PathSegment list) (getter: 'root -> 'value) : Field<'root, 'value> =
        { Path = path; Get = getter }

    let renderPath (path: PathSegment list) =
        let folder state = function
            | PathSegment.Key key
            | PathSegment.Name key ->
                if state = "" then key else $"{state}.{key}"
            | PathSegment.Index index ->
                if state = "" then $"[{index}]" else $"{state}[{index}]"

        path |> List.fold folder "" |> function
            | "" -> "<root>"
            | rendered -> rendered

type Rule<'root, 'error> = 'root -> Validation<'root, 'error>

module Rule =
    let private success (root: 'root) = Validation.ok root

    let private failAt (path: PathSegment list) (error: 'error) =
        Validation.error (Diagnostics.singleton error)
        |> Validation.at path

    let private lift (path: PathSegment list) (root: 'root) (result: Result<'value, 'error>) =
        match result with
        | Ok _ -> success root
        | Error error -> failAt path error

    let all (rules: Rule<'root, 'error> list) : Rule<'root, 'error> =
        fun root ->
            rules
            |> List.fold (fun acc rule -> Validation.map2 (fun _ _ -> root) acc (rule root)) (success root)

    let whenNotBlank (error: 'error) (field: Field<'root, string>) : Rule<'root, 'error> =
        fun root ->
            field.Get root
            |> Result.notBlank
            |> Result.mapError (fun _ -> error)
            |> lift field.Path root

    let whenMinLength (minimum: int) (error: 'error) (field: Field<'root, string>) : Rule<'root, 'error> =
        fun root ->
            field.Get root
            |> Result.minLength minimum
            |> Result.mapError (fun _ -> error)
            |> lift field.Path root

    let whenMaxLength (maximum: int) (error: 'error) (field: Field<'root, string>) : Rule<'root, 'error> =
        fun root ->
            field.Get root
            |> Result.maxLength maximum
            |> Result.mapError (fun _ -> error)
            |> lift field.Path root

    let whenPositive (error: 'error) (field: Field<'root, int>) : Rule<'root, 'error> =
        fun root ->
            field.Get root
            |> Result.greaterThan 0
            |> Result.mapError (fun _ -> error)
            |> lift field.Path root

    let sub (field: Field<'root, 'child>) (rule: Rule<'child, 'error>) : Rule<'root, 'error> =
        fun root ->
            rule (field.Get root)
            |> Validation.at field.Path
            |> Validation.map (fun _ -> root)

    let each<'root, 'collection, 'item, 'error when 'collection :> seq<'item>>
        (field: Field<'root, 'collection>)
        (rule: int -> Rule<'item, 'error>)
        : Rule<'root, 'error> =
        fun root ->
            field.Get root
            |> Seq.mapi (fun index item ->
                rule index item
                |> Validation.at [ PathSegment.Index index ]
                |> Validation.ignore)
            |> Validation.sequence
            |> Validation.ignore
            |> Validation.at field.Path
            |> Validation.map (fun _ -> root)

module Form =
    let private renderValue (value: 'value) =
        match box value with
        | null -> "<null>"
        | :? string as text -> $"\"{text}\""
        | _ -> string value

    let renderField (prefix: PathSegment list) (root: 'root) (field: Field<'root, 'value>) =
        $"{Field.renderPath (prefix @ field.Path)} = {renderValue (field.Get root)}"

let username = Field.ofGetter [ PathSegment.Name "Username" ] (fun (u: User) -> u.Username)
let address = Field.ofGetter [ PathSegment.Name "Address" ] (fun (u: User) -> u.Address)
let city = Field.ofGetter [ PathSegment.Name "City" ] (fun (a: Address) -> a.City)
let lines = Field.ofGetter [ PathSegment.Name "Lines" ] (fun (u: User) -> u.Lines)
let lineName = Field.ofGetter [ PathSegment.Name "Name" ] (fun (l: Line) -> l.Name)
let lineQuantity = Field.ofGetter [ PathSegment.Name "Quantity" ] (fun (l: Line) -> l.Quantity)

let validateUser : Rule<User, ProbeError> =
    Rule.all
        [
            Rule.whenNotBlank UsernameRequired username
            Rule.whenMinLength 3 UsernameTooShort username
            Rule.whenMaxLength 20 UsernameTooLong username
            Rule.sub address (Rule.whenNotBlank CityRequired city)
            Rule.each lines (fun index ->
                Rule.all
                    [
                        Rule.whenNotBlank (LineNameRequired index) lineName
                        Rule.whenPositive (LineQuantityInvalid index) lineQuantity
                    ])
        ]

let renderUserForm user =
    [
        yield Form.renderField [] user username
        yield Form.renderField [ PathSegment.Name "Address" ] (address.Get user) city
        for (index, line) in user.Lines |> List.indexed do
            let prefix = [ PathSegment.Name "Lines"; PathSegment.Index index ]
            yield Form.renderField prefix line lineName
            yield Form.renderField prefix line lineQuantity
    ]

let probeSchemaBuilder () =
    let schema =
        Schema.record (fun name age -> { Name = name; Age = age })
        |> Schema.field "name" (fun (contact: SchemaContact) -> contact.Name) Value.text
        |> Schema.field "age" (fun (contact: SchemaContact) -> contact.Age) Value.``int``
        |> Schema.build

    Schema.specialize (SummaryFactory<SchemaContact>()) schema

let probe () =
    let user =
        {
            Username = "ab"
            Address = { Street = "1 Main"; City = "" }
            Lines =
                [
                    { Name = ""; Quantity = 1 }
                    { Name = "Widget"; Quantity = 0 }
                ]
        }

    let validation = validateUser user

    let expected =
        [
            { Path = [ PathSegment.Name "Address"; PathSegment.Name "City" ]; Error = CityRequired }
            { Path = [ PathSegment.Name "Lines"; PathSegment.Index 0; PathSegment.Name "Name" ]; Error = LineNameRequired 0 }
            { Path = [ PathSegment.Name "Lines"; PathSegment.Index 1; PathSegment.Name "Quantity" ]; Error = LineQuantityInvalid 1 }
            { Path = [ PathSegment.Name "Username" ]; Error = UsernameTooShort }
        ]

    validation
    |> Validation.toResult
    |> Result.mapError Diagnostics.flatten
    |> Assert.equal (Error expected)

    renderUserForm user
    |> Assert.equal
        [
            "Username = \"ab\""
            "Address.City = \"\""
            "Lines[0].Name = \"\""
            "Lines[0].Quantity = 1"
            "Lines[1].Name = \"Widget\""
            "Lines[1].Quantity = 0"
        ]

    probeSchemaBuilder ()
    |> Assert.equal
        [
            { Order = 0; ExternalName = "name" }
            { Order = 1; ExternalName = "age" }
        ]

[<EntryPoint>]
let main _ =
    probe ()
    0
