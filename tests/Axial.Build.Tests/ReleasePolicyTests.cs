using Xunit;

namespace Axial.Build.Tests;

public sealed class ReleasePolicyTests
{
    [Fact]
    public void Release_reviews_tag_policy_before_packaging()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, ".github/workflows/release.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var workflow = File.ReadAllText(Path.Combine(root!.FullName, ".github/workflows/release.yml"));
        var checkout = workflow.IndexOf("ref: ${{ steps.version.outputs.tag }}", StringComparison.Ordinal);
        var build = workflow.IndexOf("dotnet build src/Axial.Tool/Axial.Tool.fsproj", StringComparison.Ordinal);
        var check = workflow.IndexOf("policy review --check", StringComparison.Ordinal);
        var pack = workflow.IndexOf("- name: Pack libraries", StringComparison.Ordinal);
        Assert.True(checkout >= 0 && checkout < build, "Build the tool from the resolved release tag, including resumed releases.");
        Assert.True(build < check && check < pack, "Review must succeed before packaging or publication.");
        var reviewStep = workflow[workflow.LastIndexOf("      - name:", check, StringComparison.Ordinal)..workflow.IndexOf("      - name:", check, StringComparison.Ordinal)];
        Assert.DoesNotContain("continue-on-error", reviewStep);
        Assert.DoesNotContain("|| true", reviewStep);
        Assert.Contains("dotnet --roll-forward Major artifacts/bin/Axial.Tool/release/axial.dll policy review --check", reviewStep);
    }
}
