using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// Loaded by MSBuild, not into the consumer's compilation.
public class AxialGuardrailsTask : Task
{
    public string Action { get; set; }
    public string Mode { get; set; } = "Fast";
    public string FastAssembly { get; set; }
    public string OtherFlags { get; set; } = "";
    public string OutputType { get; set; } = "Library";
    static IEnumerable<string> SplitArguments(string text)
    {
        var token = new StringBuilder();
        bool quoted = false, started = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\\')
            {
                int start = i;
                while (i < text.Length && text[i] == '\\') i++;
                int count = i - start;
                if (i < text.Length && text[i] == '"')
                {
                    token.Append('\\', count / 2);
                    if (count % 2 != 0) token.Append('"'); else quoted = !quoted;
                }
                else { token.Append('\\', count); i--; }
                started = true;
            }
            else if (ch == '"') { quoted = !quoted; started = true; }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (started) { yield return token.ToString(); token.Clear(); started = false; }
            }
            else { token.Append(ch); started = true; }
        }
        if (quoted) throw new FormatException("Unterminated quote in F# OtherFlags.");
        if (started) yield return token.ToString();
    }
    public string[] Defines { get; set; } = Array.Empty<string>();
    public string LangVersion { get; set; } = "preview";
    static readonly string[] FastRules = { "EffectBoundary", "RaiseInFlow", "Fixture", "ReflectionFormatting", "RawConcurrency", "WaitForFacts", "TaskSurface" };
    static readonly object FastGate = new object();
    static object fastContext;
    static string fastIdentity;
    static Assembly fastAssemblyLoaded;
    static Type fastContextType;
    double fastLoadMilliseconds;

    // Keep the shipped task netstandard-compatible. Emit only the BCL load-context subclass on Core MSBuild;
    // overriding Load (not merely Resolving) prevents fallback to FSharp.Build's already-loaded FSharp.Core.
    static Type IsolatedContextType(Type contextType)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Axial.Guardrails.Isolation"), AssemblyBuilderAccess.RunAndCollect);
        var builder = assembly.DefineDynamicModule("Isolation").DefineType("AxialFastLoadContext", TypeAttributes.Public | TypeAttributes.Sealed, contextType);
        var directory = builder.DefineField("directory", typeof(string), FieldAttributes.Private | FieldAttributes.InitOnly);
        var constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string) });
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, "Axial.Guardrails.Fast"); il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Call, contextType.GetConstructor(new[] { typeof(string), typeof(bool) }));
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, directory); il.Emit(OpCodes.Ret);
        var load = builder.DefineMethod("Load", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig, typeof(Assembly), new[] { typeof(AssemblyName) });
        il = load.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, directory);
        il.Emit(OpCodes.Call, typeof(AxialGuardrailsTask).GetMethod(nameof(LoadFastDependency))); il.Emit(OpCodes.Ret);
        builder.DefineMethodOverride(load, contextType.GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic));
        return builder.CreateTypeInfo().AsType();
    }

    // Only BCL objects cross the boundary. FSharp.Core, FCS and the SDK resolve beside the analyzer,
    // never against FSharp.Build's copies in MSBuild's default context. Bound to one context per node.
    public static Assembly LoadFastDependency(object context, AssemblyName name, string directory)
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? (Assembly)context.GetType().GetMethod("LoadFromAssemblyPath").Invoke(context, new object[] { path }) : null;
    }
    XElement FastRequest(string contextKey) =>
        new XElement("request", new XAttribute("config", Path.GetFullPath(ConfigFile)), new XAttribute("context", contextKey),
            new XAttribute("cache", Path.GetFullPath(ResultFile + ".files")), new XAttribute("langVersion", LangVersion ?? "preview"), new XAttribute("timings", Timings ? "true" : "false"),
            SourcePaths().Select(p => new XElement("file", p)), Defines.Select(d => new XElement("define", d)),
            Defines.Select(d => "--define:" + d).Concat(string.IsNullOrWhiteSpace(LangVersion) ? Array.Empty<string>() : new[] { "--langversion:" + LangVersion })
                .Concat(new[] { "--target:" + (string.Equals(OutputType, "Exe", StringComparison.OrdinalIgnoreCase) || string.Equals(OutputType, "WinExe", StringComparison.OrdinalIgnoreCase) ? "exe" : "library") })
                .Concat(SplitArguments(OtherFlags ?? "").Select(a => a.StartsWith("@", StringComparison.Ordinal)
                    ? throw new NotSupportedException("Fast cannot fingerprint response files in F# OtherFlags; supply parsing flags directly or explicitly use Full.") : a))
                .Select(a => new XElement("argument", a)));

    // One isolated context per task version and node, replaced when anything it may load changes. Files beside the
    // analyzer are its load set: a verified kernel identity proves unchanged contents, otherwise they are rehashed.
    static MethodInfo FastEntry(string fastAssembly, string name)
    {
        var path = Path.GetFullPath(fastAssembly);
        var directory = Path.GetDirectoryName(path);
        var identity = string.Join("\n", Directory.GetFiles(directory, "*.dll").OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => p + "|" + (Identity(p) ?? Digest(p))));
        lock (FastGate)
        {
            if (fastContext == null || fastIdentity != identity)
            {
                if (fastContext != null) fastContext.GetType().GetMethod("Unload").Invoke(fastContext, null);
                fastContext = null; fastAssemblyLoaded = null; fastIdentity = null;
                var type = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Private.CoreLib")
                    ?? Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", true);
                if (fastContextType == null) fastContextType = IsolatedContextType(type);
                var context = Activator.CreateInstance(fastContextType, new object[] { directory });
                try
                {
                    fastAssemblyLoaded = (Assembly)type.GetMethod("LoadFromAssemblyPath").Invoke(context, new object[] { path });
                    fastContext = context; fastIdentity = identity;
                }
                catch { fastAssemblyLoaded = null; type.GetMethod("Unload").Invoke(context, null); throw; }
            }
            return fastAssemblyLoaded.GetType("Axial.Guardrails.FastRunner", true).GetMethod(name) ?? throw new MissingMethodException("FastRunner." + name);
        }
    }

    XElement Fast()
    {
        var paths = SourcePaths();
        var sourceSet = new HashSet<string>(paths);
        var contextKey = string.Join("\n", RequestInputs.Where(i => !sourceSet.Contains(i.GetMetadata("FullPath"))).Select(i => i.GetMetadata("FullPath") + "|" + i.GetMetadata("FileHash")))
            + string.Join("\n", paths) + string.Join(";", Defines) + LangVersion + OtherFlags + OutputType + Digest(FastAssembly);
        using (var sha = SHA256.Create()) contextKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contextKey))).Replace("-", "");
        var request = FastRequest(contextKey).ToString();
        var loadWatch = System.Diagnostics.Stopwatch.StartNew();
        var run = FastEntry(FastAssembly, "Run");
        fastLoadMilliseconds = loadWatch.Elapsed.TotalMilliseconds;
        return XElement.Parse((string)run.Invoke(null, new object[] { request }));
    }

    // Before CoreCompile, so JIT and policy/parser warm-up overlap the compiler. It changes no output or cache: the
    // synchronous Run after Build repeats all work and reports every load, parse and visitor failure.
    void Warm()
    {
        string request, fastAssembly = FastAssembly;
        try { request = FastRequest("").ToString(); } catch (Exception) { return; }
        var thread = new System.Threading.Thread(() =>
        {
            try { FastEntry(fastAssembly, "Warm").Invoke(null, new object[] { request }); }
            catch (Exception) { /* Latency only; see above. */ }
        }) { IsBackground = true, Name = "Axial.Guardrails Fast warm-up" };
        thread.Start();
    }
    public string Project { get; set; } = "";
    public string TargetFramework { get; set; } = "";
    public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();
    public bool Memoize { get; set; }
    public string MemoFile { get; set; }
    [Output] public ITaskItem[] Hashed { get; set; }
    [Output] public ITaskItem[] InventoryFiles { get; set; }
    const string InventorySchema = "axial-guardrails-inventory 3";
    static string Checksum(IEnumerable<string> lines)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).Replace("-", "");
    }
    static bool Protected(string root, string file)
    {
        var name = Path.GetFileName(file);
        var relative = file.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
        return file.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)
            || name == ".editorconfig" || file.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase)
            || (relative.StartsWith("tests/Axial.Guardrails", StringComparison.Ordinal) && (file.EndsWith(".fs", StringComparison.Ordinal) || file.EndsWith(".fsproj", StringComparison.Ordinal)));
    }
    void Inventory()
    {
        var root = Path.GetFullPath(WorkingDirectory);
        // Directory change times detect entry additions/removals, while file contents are still rehashed by the caller.
        // The snapshot is plain checksummed lines: a no-op build validates thousands of directories cheaply.
        // On platforms without verified directory identities, enumerate rather than trusting mtimes.
        var rootIdentity = Identity(root);
        if (rootIdentity != null && !string.IsNullOrEmpty(MemoFile) && File.Exists(MemoFile))
        {
            try
            {
                var lines = File.ReadAllLines(MemoFile);
                var device = string.Join(":", rootIdentity.Split(':').Take(2)) + ":";
                if (lines.Length > 3 && lines[0] == InventorySchema && lines[1] == root && lines[lines.Length - 1] == Checksum(lines.Take(lines.Length - 1)))
                {
                    var files = new List<ITaskItem>();
                    var valid = true;
                    for (int i = 2; valid && i < lines.Length - 1; i++)
                    {
                        var line = lines[i];
                        if (line.StartsWith("F\t", StringComparison.Ordinal)) files.Add(new TaskItem(line.Substring(2)));
                        else
                        {
                            var separator = line.IndexOf('\t', 2);
                            var identity = line.Substring(2, separator - 2);
                            valid = line.StartsWith("D\t", StringComparison.Ordinal) && identity.StartsWith(device, StringComparison.Ordinal)
                                && Identity(line.Substring(separator + 1), true) == identity;
                        }
                    }
                    if (valid) { InventoryFiles = files.ToArray(); return; }
                }
            }
            catch (Exception) { /* A corrupt snapshot is an inventory miss. */ }
        }
        var skipped = new HashSet<string> { ".git", ".livedocs", ".fsdocs", "node_modules", "artifacts", "bin", "obj", "output" };
        var directories = new List<string>();
        var complete = rootIdentity != null;
        var result = new List<ITaskItem>();
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            var before = Identity(directory);
            foreach (var file in Directory.EnumerateFiles(directory))
                if (Protected(root, file)) result.Add(new TaskItem(file));
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (!skipped.Contains(Path.GetFileName(child)) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
            // An identity that changed during enumeration cannot vouch for this listing.
            if (before == null || before != Identity(directory)) complete = false;
            else directories.Add("D\t" + before + "\t" + directory);
        }
        InventoryFiles = result.OrderBy(i => i.ItemSpec, StringComparer.Ordinal).ToArray();
        if (!string.IsNullOrEmpty(MemoFile) && complete)
        {
            var lines = new List<string> { InventorySchema, root };
            lines.AddRange(directories);
            lines.AddRange(InventoryFiles.Select(f => "F\t" + f.ItemSpec));
            lines.Add(Checksum(lines));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(MemoFile)));
            var temporary = MemoFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllLines(temporary, lines); Publish(temporary, MemoFile); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    public string ResultFile { get; set; }
    public string StampFile { get; set; }
    public string Fingerprint { get; set; }
    public string CoverageFile { get; set; }
    public string Command { get; set; }
    public string WorkingDirectory { get; set; }
    public string ConfigFile { get; set; }
    public bool Timings { get; set; }
    readonly List<string> infrastructure = new List<string>();
    public string TimingsFile { get; set; }
    public string[] Rules { get; set; }
    public ITaskItem[] RequestInputs { get; set; } = Array.Empty<ITaskItem>();
    public bool Force { get; set; }
    public double FingerprintMilliseconds { get; set; }
    [Output] public bool CacheHit { get; set; }
    [Output] public bool Entered { get; set; }

    // Linux statx exposes inode change time: unlike mtime, an ordinary writer cannot restore it.
    // Never use mtime/length alone. Unsupported filesystems/platforms simply rehash.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    struct Statx
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(40)] public ulong Size;
        [FieldOffset(96)] public long ChangeSeconds;
        [FieldOffset(104)] public uint ChangeNanoseconds;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    struct FileSystem { [FieldOffset(0)] public long Type; }
    [DllImport("libc", SetLastError = true)]
    static extern int statfs(string path, out FileSystem data);
    [DllImport("libc", SetLastError = true)]
    static extern int statx(int directory, string path, int flags, uint mask, out Statx data);

    static string Identity(string path, bool verifiedFileSystem = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return null;
        try
        {
            FileSystem fileSystem;
            if (IntPtr.Size != 8 || (!verifiedFileSystem && (statfs(path, out fileSystem) != 0 ||
                !new long[] { 0xef53, 0x58465342, 0x9123683e, 0x01021994, 0x794c7630 }.Contains(fileSystem.Type)))) return null;
            Statx value;
            if (statx(-100, path, 0, 0x7ff, out value) != 0 || (value.Mask & 0x380) != 0x380 || value.ChangeNanoseconds == 0) return null;
            return $"{value.DeviceMajor}:{value.DeviceMinor}:{value.Inode}:{value.Size}:{value.ChangeSeconds}:{value.ChangeNanoseconds}";
        }
        catch (EntryPointNotFoundException) { return null; }
        catch (DllNotFoundException) { return null; }
    }
    static string Digest(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static bool IsDigest(string value) => value != null && value.Length == 64 && value.All(c => "0123456789ABCDEF".IndexOf(c) >= 0);
    static void Publish(string temporary, string path)
    {
        // Both writers may observe a missing destination. A losing atomic move retries as replacement.
        try { File.Move(temporary, path); }
        catch (IOException) when (File.Exists(path)) { File.Replace(temporary, path, null); }
    }
    static void Atomic(string path, XDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { document.Save(temporary); Publish(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    void Hash()
    {
        var memo = new Dictionary<string, XElement>(StringComparer.Ordinal);
        if (Memoize && File.Exists(MemoFile))
        {
            try { foreach (var entry in XDocument.Load(MemoFile).Root.Elements("file")) memo[(string)entry.Attribute("path")] = entry; }
            catch { memo.Clear(); }
        }
        var updated = new XElement("digests", new XAttribute("schema", "1"));
        Hashed = Files.Select(item => {
            var path = item.GetMetadata("FullPath");
            var identity = Memoize ? Identity(path) : null;
            XElement old;
            string digest;
            if (identity != null && memo.TryGetValue(path, out old) && (string)old.Attribute("identity") == identity && IsDigest((string)old.Attribute("hash")))
                digest = (string)old.Attribute("hash");
            else
            {
                digest = Digest(path);
                // A concurrent replacement cannot acquire the digest of the previous file.
                if (identity != null && identity != Identity(path)) identity = null;
            }
            if (identity != null) updated.Add(new XElement("file", new XAttribute("path", path), new XAttribute("identity", identity), new XAttribute("hash", digest)));
            var result = new TaskItem(item);
            result.SetMetadata("FileHash", digest);
            if (identity != null) result.SetMetadata("VerifiedIdentity", identity);
            return (ITaskItem)result;
        }).ToArray();
        if (Memoize)
        {
            var document = new XDocument(updated);
            bool unchanged;
            try { unchanged = File.Exists(MemoFile) && XNode.DeepEquals(XDocument.Load(MemoFile), document); }
            catch { unchanged = false; }
            if (!unchanged) Atomic(MemoFile, document);
        }
    }
    string[] SourcePaths() => Files.Select(f => f.GetMetadata("FullPath")).Distinct().ToArray();
    static HashSet<(string, string)> Coverage(XElement root) => new HashSet<(string, string)>(
        root.Elements("coverage").Select(e => ((string)e.Attribute("file"), (string)e.Attribute("rule"))));
    void MissingCoverage(XElement root, string reason)
    {
        var entries = Coverage(root);
        foreach (var path in SourcePaths())
        foreach (var rule in Rules)
            if (entries.Add((path, rule)))
                root.Add(new XElement("coverage", new XAttribute("file", path), new XAttribute("rule", rule), new XAttribute("status", "skipped"), new XAttribute("reason", reason)));
    }
    bool Complete(XElement root)
    {
        var entries = root.Elements("coverage").ToArray();
        var ran = Coverage(root);
        var paths = SourcePaths();
        return entries.Length == paths.Length * Rules.Length && ran.Count == entries.Length &&
            entries.All(e => (string)e.Attribute("status") == (Mode == "Fast" && !FastRules.Contains((string)e.Attribute("rule")) ? "deferred to full" : "ran")) &&
            paths.All(path => Rules.All(rule => ran.Contains((path, rule))));
    }
    bool LoadCache(out XDocument cached)
    {
        cached = null;
        if (Force || !File.Exists(StampFile) || !File.Exists(ResultFile)) return false;
        try
        {
            var stamp = File.ReadAllLines(StampFile);
            if (stamp.Length != 2 || stamp[0] != Fingerprint || stamp[1] != Digest(ResultFile)) return false;
            cached = XDocument.Load(ResultFile);
            return (string)cached.Root.Attribute("schema") == "1" && (string)cached.Root.Attribute("fingerprint") == Fingerprint &&
                (string)cached.Root.Attribute("mode") == Mode &&
                (string)cached.Root.Attribute("complete") == "true" && Complete(cached.Root);
        }
        catch { return false; }
    }
    void Findings(XElement root)
    {
        // A skipped source is visible on every build, cache replays included; the report carries the record.
        foreach (var skip in root.Descendants("skip").Select(e => ((string)e.Attribute("file"), (string)e.Attribute("reason"))).Distinct())
            Log.LogWarning(null, "AXGINF002", null, skip.Item1, 0, 0, 0, 0,
                $"Axial.Guardrails skipped this source: {skip.Item2}. No rule checked it; fix the path or exclude the file.");
        foreach (var entry in root.Elements("coverage"))
        foreach (var finding in entry.Elements("finding"))
        {
            var file = (string)entry.Attribute("file");
            var code = (string)finding.Attribute("code");
            var line = (int)finding.Attribute("line");
            var column = (int)finding.Attribute("column") + 1;
            switch ((string)finding.Attribute("severity"))
            {
                case "Error": Log.LogError(null, code, null, file, line, column, 0, 0, finding.Value); break;
                case "Warning": Log.LogWarning(null, code, null, file, line, column, 0, 0, finding.Value); break;
                default: Log.LogMessage(MessageImportance.High, $"{file}({line},{column}): {(string)finding.Attribute("severity")} {code}: {finding.Value}"); break;
            }
        }
    }
    // cmd requires raw nested quotes, not ArgumentList's Windows CRT escaping.
    // Unix Arguments uses .NET argv escaping, not shell escaping: $ and backticks reach sh unchanged.
    // cmd paths containing % or ^ are unsupported; quoted & is supported.
    static string QuoteArgument(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\') { slashes++; continue; }
            result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
            result.Append(ch);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    public static ProcessStartInfo CreateStartInfo(string command, bool windows) => new ProcessStartInfo {
        FileName = windows ? "cmd.exe" : "/bin/sh",
        Arguments = windows ? "/d /s /c \"" + command + "\"" : "-c " + QuoteArgument(command),
        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
    };
    static void Stop(Process child)
    {
        // Preserve tree termination on modern hosts while keeping the task loadable by Framework MSBuild.
        var killTree = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
        if (killTree != null) killTree.Invoke(child, new object[] { true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using (var killer = Process.Start(new ProcessStartInfo("taskkill.exe", $"/pid {child.Id} /t /f") { UseShellExecute = false, CreateNoWindow = true }))
                killer.WaitForExit();
        }
        else child.Kill();
    }
    bool InputsUnchanged()
    {
        try
        {
            return !RequestInputs.Any(item => {
                var path = item.GetMetadata("FullPath");
                var identity = item.GetMetadata("VerifiedIdentity");
                return !(identity != "" && identity == Identity(path)) && Digest(path) != item.GetMetadata("FileHash");
            });
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    // The stamp names the result's digest, so a replay never observes a half-published concurrent run.
    bool Replay()
    {
        XDocument cached;
        if (!LoadCache(out cached)) return false;
        CacheHit = true;
        Findings(cached.Root);
        WriteTimings(cached.Root, null);
        return true;
    }
    void Run()
    {
        if (Replay()) return;
        if (File.Exists(StampFile)) File.Delete(StampFile);
        Log.LogMessage(MessageImportance.High, "Running Axial.Guardrails analysis.");
        var runDirectory = Path.GetFullPath(ResultFile + "." + Guid.NewGuid().ToString("N") + ".run");
        Directory.CreateDirectory(runDirectory);
        var root = new XElement("result", new XAttribute("schema", "1"), new XAttribute("fingerprint", Fingerprint),
            new XAttribute("mode", Mode), new XAttribute("project", Project), new XAttribute("targetFramework", TargetFramework));
        var phases = new Dictionary<string, double>();
        var watch = Timings ? Stopwatch.StartNew() : null;
        double loaded = 0, checking = 0, checkedAt = 0, output = 0;
        var exit = -1;
        var lines = new List<string>();
        try
        {
            if (Mode == "Fast")
            {
                var fast = Fast();
                if (Timings)
                {
                    phases["fast-total"] = watch.Elapsed.TotalMilliseconds;
                    phases["fast-load"] = fastLoadMilliseconds;
                    foreach (var phase in fast.Attributes().Where(a => a.Name.LocalName.EndsWith("Milliseconds", StringComparison.Ordinal)))
                        phases["fast-" + phase.Name.LocalName.Substring(0, phase.Name.LocalName.Length - "Milliseconds".Length)] = (double)phase;
                }
                root.SetAttributeValue("isolated", (string)fast.Attribute("isolated") ?? "false");
                root.Add(fast.Elements("coverage"));
                root.Add(fast.Elements("skip"));
                infrastructure.AddRange(fast.Elements("failure").Select(e => $"Fast parse/visitor failed for {(string)e.Attribute("file")}: {e.Value}"));
                foreach (var path in SourcePaths())
                foreach (var rule in Rules.Where(r => !FastRules.Contains(r)))
                    root.Add(new XElement("coverage", new XAttribute("file", path), new XAttribute("rule", rule), new XAttribute("status", "deferred to full")));
                exit = 0;
            }
            else
            {
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var start = CreateStartInfo(Command + (Timings ? " --verbosity diagnostic" : ""), windows);
            start.WorkingDirectory = WorkingDirectory;
            start.EnvironmentVariables["AXIAL_GUARDRAILS_RUN"] = runDirectory;
            start.EnvironmentVariables["AXIAL_GUARDRAILS_CONFIG"] = Path.GetFullPath(ConfigFile);
            start.EnvironmentVariables["AXIAL_GUARDRAILS_TIMINGS"] = Timings ? "true" : "false";
            start.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "LatestMajor";
            using (var child = new Process { StartInfo = start })
            {
                Action<string> receive = line => {
                    if (line == null) return;
                    lock (lines)
                    {
                        lines.Add(line);
                        if (watch == null) return;
                        var now = watch.Elapsed.TotalMilliseconds;
                        if (loaded == 0 && line.Contains("Registered ")) loaded = now;
                        if (checking == 0 && line.Contains("Checking project ")) checking = now;
                        if (checkedAt == 0 && line.Contains("Checking file ")) checkedAt = now;
                        if (output == 0 && (line.Contains(" AXG") || line.Contains("No messages found"))) output = now;
                    }
                };
                child.OutputDataReceived += (_, args) => receive(args.Data);
                child.ErrorDataReceived += (_, args) => receive(args.Data);
                child.Start(); child.BeginOutputReadLine(); child.BeginErrorReadLine();
                if (!child.WaitForExit(600000)) { Stop(child); infrastructure.Add("Analyzer process timed out; coverage is incomplete."); }
                child.WaitForExit(); exit = child.ExitCode;
            }
            }
            infrastructure.AddRange(lines.Where(line => line.StartsWith("Axial.Guardrails infrastructure:", StringComparison.Ordinal)));
            foreach (var path in Directory.GetFiles(runDirectory, "*.xml")) root.Add(XElement.Load(path));
            MissingCoverage(root, Mode == "Fast" ? "Fast parser/visitor did not report this rule/file" : "Analyzer process did not report this rule/file");
            Findings(root);
            var complete = infrastructure.Count == 0 && Complete(root) && (exit == 0 || exit == 254 || exit == -2);
            // 254 is ambiguous in the upstream CLI: only findings plus complete rule/file coverage establish success.
            if (exit != 0 && !(complete && root.Descendants("finding").Any(f => (string)f.Attribute("severity") == "Error")))
            {
                complete = false;
                infrastructure.Add($"Analyzer process failed (exit code {exit}); coverage is incomplete. {string.Join(" ", lines.Skip(Math.Max(0, lines.Count - 4)))}");
            }
            if (complete && !InputsUnchanged())
            {
                complete = false;
                infrastructure.Add("Analysis inputs changed during execution; discard this coverage and rerun.");
            }
            if (!complete && infrastructure.Count == 0) infrastructure.Add("Analyzer coverage is incomplete.");
            foreach (var entry in root.Elements("coverage")) entry.SetAttributeValue("mode", Mode);
            root.SetAttributeValue("complete", complete ? "true" : "false");
            root.SetAttributeValue("exit", exit);
            Atomic(ResultFile, new XDocument(root));
            if (!string.IsNullOrEmpty(CoverageFile)) Atomic(CoverageFile, new XDocument(root));
            if (complete)
            {
                var temporary = StampFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllLines(temporary, new[] { Fingerprint, Digest(ResultFile) });
                Publish(temporary, StampFile);
            }
            if (watch != null)
            {
                phases["startup"] = loaded;
                phases["project-load"] = Math.Max(0, checking - loaded);
                phases["fcs-check"] = Math.Max(0, checkedAt - checking);
                phases["output"] = output == 0 ? 0 : watch.Elapsed.TotalMilliseconds - output;
            }
            WriteTimings(root, phases);
        }
        catch (Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            infrastructure.Add("Analyzer infrastructure failed; coverage is incomplete: " + error.Message);
            root.SetAttributeValue("complete", "false");
            root.SetAttributeValue("failure", error.Message);
            MissingCoverage(root, error.Message);
            foreach (var entry in root.Elements("coverage")) entry.SetAttributeValue("mode", Mode);
            Atomic(ResultFile, new XDocument(root));
            if (!string.IsNullOrEmpty(CoverageFile)) Atomic(CoverageFile, new XDocument(root));
        }
        finally
        {
            CleanupRunDirectory(runDirectory, path => Directory.Delete(path, true),
                message => Log.LogMessage(MessageImportance.Low, message));
        }
        if (infrastructure.Count != 0)
        {
            var skipped = root.Elements("coverage").Where(e => (string)e.Attribute("status") != "ran" && (string)e.Attribute("status") != "deferred to full").ToArray();
            var examples = string.Join("; ", skipped.Take(3).Select(e => $"{(string)e.Attribute("rule")} skipped {(string)e.Attribute("file")}: {(string)e.Attribute("reason")}"));
            var message = string.Join(" ", infrastructure) + $" {skipped.Length} skipped rule/file checks. " + examples;
            Log.LogError(null, "AXGINF001", null, null, 0, 0, 0, 0, message.Replace('\r', ' ').Replace('\n', ' '));
        }
    }
    static void CleanupRunDirectory(string path, Action<string> remove, Action<string> diagnostic)
    {
        try { remove(path); }
        catch (IOException error) { diagnostic("Could not remove analyzer run directory: " + error.Message); }
        catch (UnauthorizedAccessException error) { diagnostic("Could not remove analyzer run directory: " + error.Message); }
    }
    void WriteTimings(XElement root, Dictionary<string, double> phases)
    {
        if (!Timings) return;
        var result = new XElement("timings", new XAttribute("cacheHit", CacheHit), new XElement("fingerprint", new XAttribute("milliseconds", FingerprintMilliseconds)));
        if (phases != null) foreach (var phase in phases) result.Add(new XElement(phase.Key, new XAttribute("milliseconds", phase.Value)));
        foreach (var entry in root.Elements("coverage").Where(e => !CacheHit && e.Attribute("milliseconds") != null))
            result.Add(new XElement("rule", new XAttribute("name", (string)entry.Attribute("rule")), new XAttribute("file", (string)entry.Attribute("file")), new XAttribute("milliseconds", (string)entry.Attribute("milliseconds"))));
        Atomic(TimingsFile, new XDocument(result));
    }
    public override bool Execute()
    {
        Entered = true;
        try
        {
            if (Action == "Warm") Warm();
            else if (Action == "Inventory") Inventory();
            else if (Action == "Hash") Hash();
            else if (!Replay())
            {
                // Serialize report/stamp publication across independent builds of this project/TFM.
                // TFM-specific result paths keep unrelated frameworks independent.
                string key;
                using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(ResultFile)))).Replace("-", "");
                using (var mutex = new System.Threading.Mutex(false, "AxialGuardrails-" + key))
                {
                    bool owned;
                    try { owned = mutex.WaitOne(600000); }
                    catch (System.Threading.AbandonedMutexException) { owned = true; }
                    if (!owned) throw new TimeoutException("Waiting for concurrent Guardrails analysis timed out.");
                    try { Run(); } finally { mutex.ReleaseMutex(); }
                }
            }
        }
        catch (Exception error) { Log.LogError(null, "AXGINF001", null, Project, 0, 0, 0, 0, "Guardrails task failed: " + error.Message.Replace('\r', ' ').Replace('\n', ' ')); }
        return !Log.HasLoggedErrors;
    }
}
