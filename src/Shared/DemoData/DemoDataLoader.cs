using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LocalAiDemos.Shared.DemoData;

/// <summary>
/// Resolves the per-app <c>demo_data/</c> folder. Works both as an unpackaged
/// desktop app (uses <see cref="AppContext.BaseDirectory"/>) and as an MSIX
/// package (uses the install location).
/// </summary>
public sealed class DemoDataLoader
{
    public string Root { get; }

    public DemoDataLoader(string folderName = "demo_data")
    {
        var packagedRoot = TryGetPackagedRoot();
        var baseDir = packagedRoot ?? AppContext.BaseDirectory;
        Root = Path.Combine(baseDir, folderName);
    }

    public string Resolve(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public Task<string> ReadAllTextAsync(string relativePath) =>
        File.ReadAllTextAsync(Resolve(relativePath));

    private static string? TryGetPackagedRoot()
    {
        try { return Package.Current.InstalledLocation.Path; }
        catch { return null; }
    }
}
