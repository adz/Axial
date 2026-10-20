using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Axial.Build.Tests;

public sealed class ShippedAssemblyTests
{
    private static readonly (string Assembly, string[] Frameworks)[] ShippedAssemblies =
    [
        ("Axial", ["net8.0", "netstandard2.1"]),
        ("Axial.Console", ["net8.0", "netstandard2.1"]),
        ("Axial.ElmishFlows", ["net8.0", "netstandard2.1"]),
        ("Axial.Layers", ["net8.0", "netstandard2.1"]),
        ("Axial.FileSystem", ["net8.0", "netstandard2.1"]),
        ("Axial.Files", ["net8.0", "netstandard2.1"]),
        ("Axial.HttpClient", ["net8.0", "netstandard2.1"]),
        ("Axial.WebSocket", ["net8.0", "netstandard2.1"]),
        ("Axial.Process", ["net8.0", "netstandard2.1"]),
        ("Axial.PlatformService", ["net8.0", "netstandard2.1"]),
        ("Axial.Hosting", ["net8.0", "netstandard2.1"]),
        ("Axial.Hosting.AspNetCore", ["net8.0"]),
        ("Axial.Hosting.GenHttp", ["net8.0"]),
        ("Axial.Hosting.Node", ["netstandard2.1"]),
        ("Axial.Hosting.Browser", ["netstandard2.1"]),
        ("Axial.Telemetry", ["net8.0", "netstandard2.1"]),
        ("Axial.Telemetry.JavaScript", ["netstandard2.1"])
    ];

    private static readonly Dictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    [Fact]
    public void Shipped_runtime_assemblies_do_not_reference_FSharp_Core_printf()
    {
        var artifacts = FindArtifactsRoot();
        var findings = new List<string>();

        foreach (var (assemblyName, frameworks) in ShippedAssemblies)
        foreach (var framework in frameworks)
        {
            var path = AssemblyPath(artifacts, assemblyName, framework, frameworks.Length == 1);
            Assert.True(File.Exists(path), $"Expected shipped artifact is missing: {path}");
            findings.AddRange(FindPrintfReferences(path));
        }

        Assert.True(findings.Count == 0,
            $"Found {findings.Count} method(s) in shipped runtime assemblies that reference FSharp.Core printf:\n" +
            string.Join("\n", findings.Order()));
    }

    private static string FindArtifactsRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.Name != "artifacts") directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find artifacts directory.");
    }

    private static string AssemblyPath(string artifacts, string assemblyName, string framework, bool singleTarget) =>
        Path.Combine(artifacts, "bin", assemblyName, singleTarget ? "release" : "release_" + framework, assemblyName + ".dll");

    [Fact]
    public void Scanner_resolves_generic_printf_calls_through_method_specifications()
    {
        var artifacts = FindArtifactsRoot();
        var path = Path.Combine(artifacts, "bin", "Axial.PrintfFixture", "release", "Axial.PrintfFixture.dll");
        Assert.True(File.Exists(path), $"Expected deliberately bad fixture is missing: {path}");
        Assert.Contains(FindPrintfReferences(path), finding => finding.EndsWith("Axial.PrintfFixture.BadPrintf.render"));

        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var methodSpecifications = Enumerable.Range(1, metadata.GetTableRowCount(TableIndex.MethodSpec))
            .Select(MetadataTokens.MethodSpecificationHandle);
        Assert.Contains(methodSpecifications, handle => ResolvesToPrintfReference(metadata, handle));
    }

    private static IEnumerable<string> FindPrintfReferences(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var methodHandle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0) continue;
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            if (ReferencedTokens(il).Any(token => ResolvesToPrintfReference(metadata, MetadataTokens.EntityHandle(token))))
                yield return Path.GetFileNameWithoutExtension(path) + ": " + MethodFullName(metadata, methodHandle);
        }
    }

    private static bool ResolvesToPrintfReference(MetadataReader metadata, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.MemberReference => IsPrintfReference(metadata, (MemberReferenceHandle)handle),
        HandleKind.MethodSpecification => ResolvesToPrintfReference(
            metadata, metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method),
        _ => false
    };

    private static bool IsPrintfReference(MetadataReader metadata, MemberReferenceHandle handle)
    {
        var member = metadata.GetMemberReference(handle);
        var memberName = metadata.GetString(member.Name);
        var typeName = DeclaringTypeName(metadata, member.Parent);
        if (typeName.StartsWith("Microsoft.FSharp.Core.PrintfFormat`", StringComparison.Ordinal))
            return memberName == ".ctor";
        if (typeName == "Microsoft.FSharp.Core.PrintfModule") return true;
        if (typeName != "Microsoft.FSharp.Core.ExtraTopLevelOperators") return false;
        return memberName.StartsWith("PrintFormat", StringComparison.Ordinal)
            || memberName is "sprintf" or "failwithf" or "printf" or "printfn" or "eprintf" or "eprintfn" or "fprintf" or "fprintfn" or "kprintf" or "ksprintf";
    }

    private static string DeclaringTypeName(MetadataReader metadata, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeReference => TypeReferenceName(metadata, (TypeReferenceHandle)handle),
        HandleKind.TypeDefinition => TypeDefinitionName(metadata, (TypeDefinitionHandle)handle),
        HandleKind.TypeSpecification => metadata.GetTypeSpecification((TypeSpecificationHandle)handle)
            .DecodeSignature(new TypeNameProvider(metadata), null),
        _ => ""
    };

    private static string TypeReferenceName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var type = metadata.GetTypeReference(handle);
        return Join(metadata.GetString(type.Namespace), metadata.GetString(type.Name));
    }

    private static string TypeDefinitionName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        return Join(metadata.GetString(type.Namespace), metadata.GetString(type.Name));
    }

    private static string Join(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;

    private static string MethodFullName(MetadataReader metadata, MethodDefinitionHandle handle)
    {
        var method = metadata.GetMethodDefinition(handle);
        var type = metadata.GetTypeDefinition(method.GetDeclaringType());
        return Join(metadata.GetString(type.Namespace), metadata.GetString(type.Name)) + "." + metadata.GetString(method.Name);
    }

    private static IEnumerable<int> ReferencedTokens(byte[] il)
    {
        for (var offset = 0; offset < il.Length;)
        {
            short value = il[offset++];
            if (value == 0xFE) value = (short)(0xFE00 | il[offset++]);
            if (!OpCodesByValue.TryGetValue(value, out var opCode)) yield break;
            var size = OperandSize(opCode.OperandType, il, offset);
            if (opCode.OperandType == OperandType.InlineMethod && offset + 4 <= il.Length)
                yield return BitConverter.ToInt32(il, offset);
            offset += size;
        }
    }

    private static int OperandSize(OperandType type, byte[] il, int offset) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod
            or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
            or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
        _ => throw new InvalidDataException($"Unknown IL operand type {type}.")
    };

    private sealed class TypeNameProvider(MetadataReader metadata) : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string elementType, ArrayShape shape) => elementType;
        public string GetByReferenceType(string elementType) => elementType;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "";
        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) => genericType;
        public string GetGenericMethodParameter(object? genericContext, int index) => "";
        public string GetGenericTypeParameter(object? genericContext, int index) => "";
        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => elementType;
        public string GetPointerType(string elementType) => elementType;
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSZArrayType(string elementType) => elementType;
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeDefinitionName(metadata, handle);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeReferenceName(metadata, handle);
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
