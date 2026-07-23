namespace Axial.Schema.Benchmarks

open System
open System.Text.Json
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Axial.Schema
open Axial.Schema.Json
open Axial.Schema.Syntax
open Axial

/// The shared benchmark model: a realistic aggregate with primitives, a nested record, and collections.
module CodecModel =
    type Address = { Street: string; City: string }

    type Contact = { Label: string; Value: string }

    type Customer =
        { Id: Guid
          Name: string
          Age: int
          Balance: decimal
          Newsletter: bool
          Joined: DateOnly
          LastSeen: DateTimeOffset
          Address: Address
          Contacts: Contact list
          Scores: int list }

    let addressSchema =
        SchemaCE.schema<Address> {
            SchemaCE.field "street" _.Street
            SchemaCE.field "city" _.City
            SchemaCE.construct (fun street city -> { Street = street; City = city })
        }

    let contactSchema =
        SchemaCE.schema<Contact> {
            SchemaCE.field "label" _.Label
            SchemaCE.field "value" _.Value
            SchemaCE.construct (fun label value -> { Label = label; Value = value })
        }

    let customerSchema =
        SchemaCE.schema<Customer> {
            SchemaCE.field "id" _.Id
            SchemaCE.field "name" _.Name
            SchemaCE.field "age" _.Age
            SchemaCE.field "balance" _.Balance
            SchemaCE.field "newsletter" _.Newsletter
            SchemaCE.field "joined" _.Joined
            SchemaCE.field "lastSeen" _.LastSeen
            SchemaCE.field "address" _.Address {
                withSchema addressSchema
            }
            SchemaCE.field "contacts" _.Contacts {
                withSchema (Schema.listWith contactSchema)
            }
            SchemaCE.field "scores" _.Scores {
                withSchema (Schema.listWith Schema.int)
            }
            SchemaCE.construct (fun id name age balance newsletter joined lastSeen address contacts scores ->
                { Id = id
                  Name = name
                  Age = age
                  Balance = balance
                  Newsletter = newsletter
                  Joined = joined
                  LastSeen = lastSeen
                  Address = address
                  Contacts = contacts
                  Scores = scores })
        }

    let sample =
        { Id = Guid.Parse "7d9a2f5e-95c8-4f2b-b1e3-2f6d3a1c9b42"
          Name = "Ada Lovelace"
          Age = 36
          Balance = 1234.56m
          Newsletter = true
          Joined = DateOnly(2024, 3, 15)
          LastSeen = DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero)
          Address = { Street = "12 Analytical Way"; City = "London" }
          Contacts =
            [ { Label = "email"; Value = "ada@example.com" }
              { Label = "phone"; Value = "+44 20 7946 0000" } ]
          Scores = [ 3; 1; 4; 1; 5 ] }

/// Compares the compiled schema codec against System.Text.Json on the same model.
[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type JsonCodecBenchmarks() =
    let codec = Json.compile CodecModel.customerSchema

    let serializerOptions =
        let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        options

    let json = Json.serialize codec CodecModel.sample
    let jsonBytes = Json.serializeBytes codec CodecModel.sample

    [<Benchmark(Baseline = true, Description = "Axial Json.serialize")>]
    member _.AxialSerialize() = Json.serialize codec CodecModel.sample

    [<Benchmark(Description = "System.Text.Json Serialize")>]
    member _.SystemTextJsonSerialize() =
        JsonSerializer.Serialize(CodecModel.sample, serializerOptions)

    [<Benchmark(Description = "Axial Json.deserialize")>]
    member _.AxialDeserialize() = Json.deserialize codec json

    [<Benchmark(Description = "Axial Json.deserializeBytes")>]
    member _.AxialDeserializeBytes() = Json.deserializeBytes codec jsonBytes

    [<Benchmark(Description = "System.Text.Json Deserialize")>]
    member _.SystemTextJsonDeserialize() =
        JsonSerializer.Deserialize<CodecModel.Customer>(json, serializerOptions)

/// Compares the trusted codec lane against boundary parsing with full path-aware diagnostics.
[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type BoundaryParseBenchmarks() =
    let codec = Json.compile CodecModel.customerSchema
    let json = Json.serialize codec CodecModel.sample

    [<Benchmark(Baseline = true, Description = "Axial Json.deserialize (trusted lane)")>]
    member _.CodecDeserialize() = Json.deserialize codec json

    [<Benchmark(Description = "JsonDocument + Data + Schema.parse (boundary lane)")>]
    member _.BoundaryParse() =
        use document = JsonDocument.Parse json
        let input = Data.ofJsonDocument document
        Schema.parse CodecModel.customerSchema input
