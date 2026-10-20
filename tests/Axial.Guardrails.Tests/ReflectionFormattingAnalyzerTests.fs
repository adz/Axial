namespace Axial.Guardrails.Tests

open FSharp.Analyzers.SDK.Testing
open Axial.Guardrails
open Swensen.Unquote
open Xunit

module ReflectionFormattingAnalyzerTests =
    let private findings (body: string) =
        task {
            let source =
                "module M\n"
                + "type U = A | B of int\n"
                + "type R = { X: int }\n"
                + "type Shown = C | D\n"
                + "    with override this.ToString() = match this with C -> \"c\" | D -> \"d\"\n"
                + body
            let! ctx = AnalyzerTestContext.create source
            return ReflectionFormattingAnalyzer.analyze ctx |> List.map (fun message -> message.Range.StartLine - 5, message.Code)
        }

    let private nativeAotFindings (body: string) =
        task {
            let! ctx = AnalyzerTestContext.create ("module M\n" + body)
            return ReflectionFormattingAnalyzer.analyzeWithNativeAot true ctx |> List.map (fun message -> message.Range.StartLine - 1, message.Code, message.Message)
        }

    [<Fact>]
    let ``flags printf family and printf-lowered interpolation in NativeAOT projects`` () =
        task {
            let! found =
                nativeAotFindings (
                    "let a = sprintf \"count %d\" 1\n"
                    + "let b () : unit = failwithf \"bad %d\" 2\n"
                    + "let c = $\"count {3}.\"\n")

            test <@ found |> List.map (fun (line, code, _) -> line, code) = [ 1, "AXG006"; 2, "AXG006"; 3, "AXG006" ] @>
            test <@ found |> List.forall (fun (_, _, message) -> message.Contains("Convert the hole with `string` or use String.Concat.")) @>
        }

    [<Fact>]
    let ``does not flag printf lowering outside NativeAOT projects`` () =
        task {
            let! ctx = AnalyzerTestContext.create "module M\nlet value = $\"count {1}.\"\n"
            test <@ ReflectionFormattingAnalyzer.analyzeWithNativeAot false ctx = [] @>
        }

    [<Fact>]
    let ``does not flag all-string-hole interpolation in NativeAOT projects`` () =
        task {
            let! found = nativeAotFindings "let name = \"Axial\"\nlet value = $\"hello {name}\"\n"
            test <@ found = [] @>
        }

    [<Fact>]
    let ``flags interpolation, string, ToString and %A on unions and records`` () =
        task {
            let! found =
                findings (
                    "let u = B 1\n"
                    + "let r = { X = 1 }\n"
                    + "let a = $\"{u}\"\n"
                    + "let b = string u\n"
                    + "let c = u.ToString()\n"
                    + "let d = sprintf \"%A\" r\n"
                    + "let e = System.String.Concat(\"x\", box r)\n"
                    + "let f = string (Some 1)\n")

            test <@ found = [ 3, "AXG006"; 4, "AXG006"; 5, "AXG006"; 6, "AXG006"; 7, "AXG006"; 8, "AXG006" ] @>
        }

    [<Fact>]
    let ``flags %A even on types with their own ToString, but not %A on primitives`` () =
        task {
            let! found = findings "let a = sprintf \"%A\" C\nlet b = sprintf \"%A %d\" (1, 2) 3\nlet c = sprintf \"%A\" 42\nlet d = sprintf \"%O\" C\n"
            test <@ found = [ 1, "AXG006"; 2, "AXG006" ] @>
        }

    [<Fact>]
    let ``flags formatting a type parameter`` () =
        task {
            let! found = findings "let render (value: 'T) = string value\nlet render2 (value: 'T) = $\"{value}\"\n"
            test <@ found = [ 1, "AXG006"; 2, "AXG006" ] @>
        }

    [<Fact>]
    let ``looks through box to the formatted value`` () =
        task {
            let! found =
                findings (
                    "let render (value: 'T) = string (box value)\n"
                    + "let u = B 1\n"
                    + "let a = (box u).ToString()\n"
                    + "let b = string (box 42)\n")

            test <@ found = [ 1, "AXG006"; 3, "AXG006" ] @>
        }

    [<Fact>]
    let ``allows primitives, fields, own ToString overrides and suppressed lines`` () =
        task {
            let! found =
                findings (
                    "let r = { X = 1 }\n"
                    + "let a = $\"{r.X} {42} {System.DateTimeOffset.MinValue}\"\n"
                    + "let b = string C\n"
                    + "let c = sprintf \"%d %s\" r.X \"x\"\n"
                    + "let d = System.String.Concat(\"x\", box 3)\n"
                    + "// axial-allow-reflection-format\n"
                    + "let e = string r\n"
                    + "let f = string System.DayOfWeek.Monday\n"
                    + "let g = System.String.Join(\", \", [ \"a\"; \"b\" ])\n")

            test <@ found = [] @>
        }
