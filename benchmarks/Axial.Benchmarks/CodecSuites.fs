namespace Axial.Benchmarks

open System
open System.Text.Json
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Axial.Schema
open Axial.Codec
open Axial.Validation.Schema

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
        Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
        |> Schema.text "street" _.Street
        |> Schema.text "city" _.City
        |> Schema.build

    let contactSchema =
        Schema.recordFor<Contact, _> (fun label value -> { Label = label; Value = value })
        |> Schema.text "label" _.Label
        |> Schema.text "value" _.Value
        |> Schema.build

    let customerSchema =
        Schema.recordFor<Customer, _> (fun id name age balance newsletter joined lastSeen address contacts scores ->
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
        |> Schema.guid "id" _.Id
        |> Schema.text "name" _.Name
        |> Schema.int "age" _.Age
        |> Schema.decimal "balance" _.Balance
        |> Schema.bool "newsletter" _.Newsletter
        |> Schema.date "joined" _.Joined
        |> Schema.dateTime "lastSeen" _.LastSeen
        |> Schema.nested "address" _.Address addressSchema
        |> Schema.many "contacts" _.Contacts contactSchema
        |> Schema.field "scores" _.Scores (Value.manyOf Value.int)
        |> Schema.build

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

    [<Benchmark(Description = "JsonDocument + RawInput + Input.parse (boundary lane)")>]
    member _.BoundaryParse() =
        use document = JsonDocument.Parse json
        let input = RawInput.ofJsonDocument document
        (Input.parse CodecModel.customerSchema input).Result
