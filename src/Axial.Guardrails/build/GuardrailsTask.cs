using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// Loaded by MSBuild, not into the consumer's compilation.
public class AxialGuardrailsTask : Task
{
    public string Action { get; set; }
    public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();
    public bool Memoize { get; set; }
    public string MemoFile { get; set; }
    [Output] public ITaskItem[] Hashed { get; set; }
    public string ResultFile { get; set; }
    public string StampFile { get; set; }
    public string Fingerprint { get; set; }
    public string CoverageFile { get; set; }
    public string Command { get; set; }
    public string WorkingDirectory { get; set; }
    public string ConfigFile { get; set; }
    public bool Timings { get; set; }
    public string TimingsFile { get; set; }
    public string[] Rules { get; set; }
    public ITaskItem[] RequestInputs { get; set; } = Array.Empty<ITaskItem>();
    public bool Force { get; set; }
    public double FingerprintMilliseconds { get; set; }
    [Output] public bool CacheHit { get; set; }

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

    static string Identity(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return null;
        try
        {
            FileSystem fileSystem;
            if (IntPtr.Size != 8 || statfs(path, out fileSystem) != 0 ||
                !new long[] { 0xef53, 0x58465342, 0x9123683e, 0x01021994, 0x794c7630 }.Contains(fileSystem.Type)) return null;
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
    static bool IsDigest(string value) => value != null && value.Length == 64 && value.All(c => "0123456789ABCDEF".Contains(c));
    static void Publish(string temporary, string path)
    {
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
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
            var path = Path.GetFullPath(item.ItemSpec);
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
        if (Memoize) Atomic(MemoFile, new XDocument(updated));
    }
    string[] SourcePaths() => Files.Select(f => Path.GetFullPath(f.ItemSpec)).Distinct().ToArray();
    bool Complete(XElement root)
    {
        var entries = root.Elements("coverage").ToArray();
        var paths = SourcePaths();
        return entries.Length == paths.Length * Rules.Length &&
            paths.All(path => Rules.All(rule => entries.Count(e => (string)e.Attribute("file") == path && (string)e.Attribute("rule") == rule && (string)e.Attribute("status") == "ran") == 1));
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
                (string)cached.Root.Attribute("complete") == "true" && Complete(cached.Root);
        }
        catch { return false; }
    }
    void Infrastructure(string message)
    {
        Log.LogWarning(null, "AXGINF001", null, null, 0, 0, 0, 0, message);
    }
    void Findings(XElement root)
    {
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
    void Run()
    {
        XDocument cached;
        if (LoadCache(out cached))
        {
            CacheHit = true;
            Findings(cached.Root);
            WriteTimings(cached.Root, null);
            return;
        }
        if (File.Exists(StampFile)) File.Delete(StampFile);
        Log.LogMessage(MessageImportance.High, "Running Axial.Guardrails analysis.");
        var runDirectory = Path.GetFullPath(ResultFile + "." + Guid.NewGuid().ToString("N") + ".run");
        Directory.CreateDirectory(runDirectory);
        var root = new XElement("result", new XAttribute("schema", "1"), new XAttribute("fingerprint", Fingerprint));
        var phases = new Dictionary<string, double>();
        var watch = Timings ? Stopwatch.StartNew() : null;
        double loaded = 0, checking = 0, checkedAt = 0, output = 0;
        var exit = -1;
        var lines = new List<string>();
        try
        {
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var start = new ProcessStartInfo(windows ? "cmd.exe" : "/bin/sh") {
                WorkingDirectory = WorkingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(windows ? "/c" : "-c");
            start.ArgumentList.Add((windows ? "" : "exec ") + Command + (Timings ? " --verbosity diagnostic" : ""));
            start.Environment["AXIAL_GUARDRAILS_RUN"] = runDirectory;
            start.Environment["AXIAL_GUARDRAILS_CONFIG"] = Path.GetFullPath(ConfigFile);
            start.Environment["AXIAL_GUARDRAILS_TIMINGS"] = Timings ? "true" : "false";
            start.Environment["DOTNET_ROLL_FORWARD"] = "LatestMajor";
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
                if (!child.WaitForExit(600000)) { child.Kill(true); Infrastructure("Analyzer process timed out; coverage is incomplete."); }
                child.WaitForExit(); exit = child.ExitCode;
            }
            foreach (var path in Directory.GetFiles(runDirectory, "*.xml")) root.Add(XElement.Load(path));
            foreach (var path in SourcePaths())
            foreach (var rule in Rules)
                if (!root.Elements("coverage").Any(e => (string)e.Attribute("file") == path && (string)e.Attribute("rule") == rule))
                    root.Add(new XElement("coverage", new XAttribute("file", path), new XAttribute("rule", rule), new XAttribute("status", "skipped"), new XAttribute("reason", "Analyzer process did not report this rule/file")));
            foreach (var entry in root.Elements("coverage").Where(e => (string)e.Attribute("status") != "ran"))
                Infrastructure($"{(string)entry.Attribute("rule")} skipped {(string)entry.Attribute("file")}: {(string)entry.Attribute("reason")}");
            Findings(root);
            var complete = Complete(root) && (exit == 0 || exit == 254 || exit == -2);
            // 254 is ambiguous in the upstream CLI: only findings plus complete rule/file coverage establish success.
            if (exit != 0 && !(complete && root.Descendants("finding").Any(f => (string)f.Attribute("severity") == "Error")))
            {
                complete = false;
                Infrastructure($"Analyzer process failed (exit code {exit}); coverage is incomplete. {string.Join(" ", lines.Skip(Math.Max(0, lines.Count - 4)))}");
            }
            if (complete && RequestInputs.Any(item => {
                var path = Path.GetFullPath(item.ItemSpec);
                var identity = item.GetMetadata("VerifiedIdentity");
                return !(identity != "" && identity == Identity(path)) && Digest(path) != item.GetMetadata("FileHash");
            }))
            {
                complete = false;
                Infrastructure("Analysis inputs changed during execution; discard this coverage and rerun.");
            }
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
            Infrastructure("Analyzer infrastructure failed; coverage is incomplete: " + error.Message);
            root.SetAttributeValue("complete", "false");
            root.SetAttributeValue("failure", error.Message);
            foreach (var path in SourcePaths())
            foreach (var rule in Rules)
                if (!root.Elements("coverage").Any(e => (string)e.Attribute("file") == path && (string)e.Attribute("rule") == rule))
                    root.Add(new XElement("coverage", new XAttribute("file", path), new XAttribute("rule", rule), new XAttribute("status", "skipped"), new XAttribute("reason", error.Message)));
            Atomic(ResultFile, new XDocument(root));
            if (!string.IsNullOrEmpty(CoverageFile)) Atomic(CoverageFile, new XDocument(root));
        }
        finally
        {
            try { Directory.Delete(runDirectory, true); }
            catch (IOException error) { Infrastructure("Could not remove analyzer run directory: " + error.Message); }
        }
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
        if (Action == "Hash") Hash(); else Run();
        return !Log.HasLoggedErrors;
    }
}
