namespace Axial.FileSystem.Tests

open System
open System.Diagnostics
open System.IO
open System.Threading.Tasks
open Axial
open Axial.FileSystem
open Xunit

module ConsumerMatrixTests =
    let private matrixBody (first: string) (second: string) = $"""
open {first}
open {second}
open Axial.FileSystem

type MatrixEnv =
    {{ FS: IFileSystem }}
    interface IHasFileSystem with
        member this.FileSystem = this.FS

let qualifiedBcl () =
    let id : global.System.Guid = global.System.Guid.NewGuid()
    let empty = global.System.Guid.Empty
    let parsed = global.System.Guid.Parse "00000000-0000-0000-0000-000000000000"
    let random = new global.System.Random(1)
    id, empty, parsed, random.Next()

let axialOperations (env: #IHasFileSystem) =
    let workflow = flow {{
        let! generated = Guid.newGuid
        let! qualified = Axial.Guid.newGuid
        let seeded = Random.seeded 1
        let! random = Random.next |> Flow.withRandom seeded
        let! now = Clock.now
        do! Log.info "matrix"
        return generated, random, now
    }}
    let mapped = workflow |> Flow.map id
    let platform = TestPlatform.create ()
    if false then
        mapped |> Flow.run env |> ignore
        mapped |> Flow.runTest env |> ignore
        mapped |> Flow.runTestWith platform env |> ignore
    let sink = LogSink.none
    sink, mapped

let fileOperations (env: #IHasFileSystem) =
    let atomic = Files.writeAtomic "matrix.txt" [||]
    let log = AppendLog.openLog "matrix.log" 1024L
    let annotated : AppendLog = Unchecked.defaultof<AppendLog>
    if false then
        atomic |> Flow.run env |> ignore
        AppendLog.append [||] annotated |> Flow.run env |> ignore
    log, annotated

qualifiedBcl () |> ignore
printfn "CONSUMER MATRIX OK"
"""

    let private unqualifiedBody (first: string) (second: string) = $"""
open {first}
open {second}
let id : Guid = Guid.NewGuid()
let random = new Random(1)
printfn "UNQUALIFIED ACCEPTED %%O %%d" id (random.Next())
"""

    let private compileConsumer target first second body =
        let root = Path.Combine(Path.GetTempPath(), $"axial-compiled-consumer-{global.System.Guid.NewGuid():N}")
        Directory.CreateDirectory root |> ignore
        let configuration = DirectoryInfo(Path.GetDirectoryName(typeof<Flow<unit, string, int>>.Assembly.Location)).Name.Split('_')[0]
        let assembly name = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, $"../../artifacts/bin/{name}/{configuration}_{target}/{name}.dll"))
        for name in [ "Axial"; "Axial.FileSystem" ] do
            Assert.True(File.Exists(assembly name), $"Build {name} for {target} before running the consumer matrix")
        let project = Path.Combine(root, "Consumer.fsproj")
        File.WriteAllText(Path.Combine(root, "Consumer.fs"), "module Consumer\n" + body)
        File.WriteAllText(project, $"""<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>{target}</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup><Compile Include="Consumer.fs" />
<Reference Include="Axial"><HintPath>{assembly "Axial"}</HintPath></Reference>
<Reference Include="Axial.FileSystem"><HintPath>{assembly "Axial.FileSystem"}</HintPath></Reference>
</ItemGroup></Project>""")
        try
            use child = new Process(StartInfo = ProcessStartInfo(
                "dotnet", $"build \"{project}\" --nologo -v q -p:RestoreIgnoreFailedSources=true",
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false))
            child.Start() |> ignore
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            if not (child.WaitForExit(120_000)) then
                child.Kill(entireProcessTree = true)
                failwith $"Consumer compilation timed out: {target}, {first} then {second}"
            let text = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult()
            let recordRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../artifacts/verify/consumer-matrix"))
            Directory.CreateDirectory recordRoot |> ignore
            let kind = if body.Contains("UNQUALIFIED") then "probe" else "surface"
            File.WriteAllText(Path.Combine(recordRoot, $"{target}-{first}-{second}-{kind}.txt"),
                $"exit={child.ExitCode}\n" + text)
            child.ExitCode, text
        finally
            Directory.Delete(root, true)

    let private requireMatrix (first, second) =
        for target in [ "netstandard2.1"; "net8.0" ] do
            let code, output = compileConsumer target first second (matrixBody first second)
            Assert.True((code = 0), $"{target}, {first} then {second}:{Environment.NewLine}{output}")

    [<Fact>]
    let ``compiled consumer matrix builds with System opened before Axial`` () =
        requireMatrix ("System", "Axial")

    [<Fact>]
    let ``compiled consumer matrix builds with Axial opened before System`` () =
        requireMatrix ("Axial", "System")

    [<Fact>]
    let ``unqualified BCL probes record compiler acceptance in both open orders`` () =
        for first, second in [ "System", "Axial"; "Axial", "System" ] do
            for target in [ "netstandard2.1"; "net8.0" ] do
                let code, output = compileConsumer target first second (unqualifiedBody first second)
                // F# resolves the BCL type/member fallback in both orders. This is recorded, not the migration contract.
                Assert.True((code = 0), $"{target}, {first} then {second} unexpectedly rejected BCL fallback: {output}")
