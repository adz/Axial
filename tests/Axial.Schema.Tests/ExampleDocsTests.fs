module Axial.Tests.ExampleDocsTests

open System
open System.Diagnostics
open System.IO
open System.Threading.Tasks
open Swensen.Unquote
open Xunit

// The Schema half of the runnable-example docs check. The Flow half lives in Axial.Flow.Tests and is
// scoped to the flow product, so each product asserts only its own page and neither project needs the
// other's examples after the repository split.

let private runGenerator (scriptPath: string) (product: string) (environment: (string * string) list) =
    use childProcess =
        new Process(
            StartInfo =
                ProcessStartInfo(
                    FileName = "bash",
                    Arguments = $"\"{scriptPath}\" \"{product}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                )
        )

    for key, value in environment do
        childProcess.StartInfo.EnvironmentVariables[key] <- value

    childProcess.Start() |> ignore

    let standardOutput = childProcess.StandardOutput.ReadToEndAsync()
    let standardError = childProcess.StandardError.ReadToEndAsync()
    let completed = childProcess.WaitForExit(TimeSpan.FromMinutes(5.0))

    if not completed then
        try
            childProcess.Kill(entireProcessTree = true)
        with _ ->
            ()

    Task.WhenAll(standardOutput, standardError).Wait(TimeSpan.FromSeconds(5.0)) |> ignore

    let readOutput (readTask: Task<string>) =
        if readTask.IsCompletedSuccessfully then readTask.Result else ""

    let output = readOutput standardOutput + readOutput standardError

    if completed then
        childProcess.ExitCode, output
    else
        124, output + $"{Environment.NewLine}Timed out waiting for {scriptPath}."

[<Fact>]
let ``Runnable Schema example docs are generated from executable example projects`` () =
    let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
    let schemaDocsPath = Path.Combine(repoRoot, "docs", "schema", "examples.md")
    let generatorPath = Path.Combine(repoRoot, "scripts", "generate-example-docs.sh")
    let generatedSchemaPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-schema.md")

    try
        let exitCode, output =
            runGenerator generatorPath "schema" [ "DOCS_SCHEMA_EXAMPLES_OUTPUT", generatedSchemaPath ]

        if exitCode <> 0 then
            failwithf "generate-example-docs.sh schema failed with exit code %d:%s%s" exitCode Environment.NewLine output

        test <@ File.ReadAllText generatedSchemaPath = File.ReadAllText schemaDocsPath @>
    finally
        if File.Exists generatedSchemaPath then
            File.Delete generatedSchemaPath
