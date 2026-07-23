module Axial.Schema.Http.Tests.BoundaryTests

open Axial

open System.Text.Json
open Xunit
open Swensen.Unquote
open Axial.Validation
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Http.Tests.Fixtures
open Axial.Schema.Syntax

[<Fact>]
let ``json pointers render names, keys, and indexes`` () =
    let path =
        [ PathSegment.Name "address"
          PathSegment.Key "some/key~x"
          PathSegment.Index 2 ]

    test <@ JsonPointer.ofPath path = "/address/some~1key~0x/2" @>

[<Fact>]
let ``the empty path renders as the whole-document pointer`` () =
    test <@ JsonPointer.ofPath [] = "" @>

[<Fact>]
let ``form pairs nest through dotted names and group repeats`` () =
    let raw =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"
              "age", "36"
              "address.street", "12 Analytical Way"
              "address.city", "London"
              "tags", "vip"
              "tags", "founder" ]

    let parsed = Schema.parseRetainingInput (signupSchema ()) raw
    test <@ parsed.IsValid @>
    test <@ parsed.Value.Address.City = "London" @>
    test <@ parsed.Value.Tags = [ "vip"; "founder" ] @>

[<Fact>]
let ``indexed form names become ordered collections`` () =
    let raw =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"
              "age", "36"
              "address.street", "12 Analytical Way"
              "address.city", "London"
              "tags.1", "founder"
              "tags.0", "vip" ]

    let parsed = Schema.parseRetainingInput (signupSchema ()) raw
    test <@ parsed.IsValid @>
    test <@ parsed.Value.Tags = [ "vip"; "founder" ] @>

[<Fact>]
let ``query pairs parse flat models`` () =
    let schema =
        SchemaCE.schema<{| Page: int; Terms: string list |}> {
            SchemaCE.field "page" (fun (value: {| Page: int; Terms: string list |}) -> value.Page)
            SchemaCE.field "terms" (fun (value: {| Page: int; Terms: string list |}) -> value.Terms) {
                withSchema (Schema.listWith Schema.text)
            }
            SchemaCE.construct (fun page terms -> {| Page = page; Terms = terms |})
        }

    let parsed =
        Schema.parseRetainingInput schema (BoundaryInput.ofQuery [ "page", "3"; "terms", "one"; "terms", "two" ])

    test <@ parsed.IsValid @>
    test <@ parsed.Value.Page = 3 @>
    test <@ parsed.Value.Terms = [ "one"; "two" ] @>

[<Fact>]
let ``failed parses render problem details with json pointers`` () =
    use document = JsonDocument.Parse invalidJson
    let parsed = Schema.parseRetainingInput (signupSchema ()) (Data.ofJsonDocument document)

    let details =
        match ProblemDetails.ofParsed parsed with
        | Some details -> details
        | None -> failwith "expected a failed parse"

    test <@ details.Status = 400 @>
    test <@ details.Errors |> List.exists (fun error -> error.Pointer = "/address/city") @>
    test <@ details.Errors |> List.exists (fun error -> error.Pointer = "/tags") @>

    let json = ProblemDetails.toJson details
    use body = JsonDocument.Parse json
    test <@ body.RootElement.GetProperty("status").GetInt32() = 400 @>
    test <@ body.RootElement.GetProperty("errors").GetArrayLength() > 0 @>

[<Fact>]
let ``successful parses produce no problem details`` () =
    use document = JsonDocument.Parse validJson
    let parsed = Schema.parseRetainingInput (signupSchema ()) (Data.ofJsonDocument document)
    test <@ ProblemDetails.ofParsed parsed = None @>

[<Fact>]
let ``openapi documents embed generated schemas per endpoint`` () =
    let spec =
        Endpoint.post "/signups"
        |> Endpoint.summary "Create a signup"
        |> Endpoint.accepts (signupSchema ())
        |> Endpoint.returnsJson 201 "The trusted signup that was parsed." (signupSchema ())
        |> Endpoint.returnsProblemDetails

    let document = OpenApi.document (OpenApi.info "Signup API" "1.0.0") [ spec ]

    use parsed = JsonDocument.Parse document
    let root = parsed.RootElement
    Assert.Equal("3.1.0", root.GetProperty("openapi").GetString())

    let operation = root.GetProperty("paths").GetProperty("/signups").GetProperty("post")
    Assert.Equal("Create a signup", operation.GetProperty("summary").GetString())

    let requestSchema =
        operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")

    Assert.True(requestSchema.GetProperty("properties").TryGetProperty("name") |> fst)

    let responses = operation.GetProperty "responses"
    Assert.True(responses.TryGetProperty("201") |> fst)

    let problem = responses.GetProperty("400").GetProperty("content")
    Assert.True(problem.TryGetProperty(ProblemDetails.ContentType) |> fst)
