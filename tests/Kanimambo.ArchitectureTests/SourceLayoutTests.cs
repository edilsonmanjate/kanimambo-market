using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kanimambo.ArchitectureTests;

/// <summary>
/// Source layout rule: one top-level type (class, record, struct, interface, enum, delegate) per file, and the file is
/// named after it. Nested types stay with their parent. When a generic and a non-generic type share a name, the
/// generic one's file carries its type parameters (<c>Result{TValue}.cs</c>). EF Core migrations are generated and exempt.
/// </summary>
public sealed class SourceLayoutTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string[] SourceFolders = ["src", "tests"];

    public static TheoryData<string> SourceFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in SourceFolders
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(Root, dir), "*.cs", SearchOption.AllDirectories))
            .Where(f => !IsGenerated(f))
            .Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(Root, file).Replace('\\', '/'));
        }

        return data;
    }

    [Fact]
    public void Source_files_are_found() => SourceFiles().Count.ShouldBeGreaterThan(500);

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Each_file_holds_one_top_level_type_named_like_the_file(string file)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Root, file)), cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
        var members = root.Members.OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Members).Concat(root.Members);
        var types = members.Where(m => m is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).ToList();

        types.Count.ShouldBeLessThanOrEqualTo(1, $"{file} declares {string.Join(", ", types.Select(Name))}; move each type to its own file.");
        if (types is [var type])
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            (fileName == Name(type) || fileName.StartsWith(Name(type) + "{", StringComparison.Ordinal))
                .ShouldBeTrue($"{file} declares {Name(type)}; name the file {Name(type)}.cs.");
        }
    }

    private static string Name(Microsoft.CodeAnalysis.SyntaxNode type) => type switch
    {
        BaseTypeDeclarationSyntax t => t.Identifier.Text,
        DelegateDeclarationSyntax d => d.Identifier.Text,
        _ => string.Empty,
    };

    private static bool IsGenerated(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("obj") || parts.Contains("bin") || parts.Contains("Migrations");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kanimambo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kanimambo.slnx not found above the test output.");
    }
}
