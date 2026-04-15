using PackumentGenerator;

var parsed = ParseArgs(args);

if (parsed.ShowHelp || !parsed.IsValid)
{
    if (!parsed.ShowHelp && parsed.Error is not null) {
        Console.Error.WriteLine($"Error: {parsed.Error}\n");
    }

    Console.Error.WriteLine("""
                            Usage: PackumentGenerator [options]

                            Options:
                              --repo-path=<path>         Path to the git repository (default: current directory)
                              --base-url=<url>           Base URL for tarball downloads (required)
                              --packument-dir=<dir>      Output directory for packument files (required)
                              --tarball-dir=<dir>        Output directory for tarball files (required)
                              --package-dir=<dir>        Directory name under Packages/ (repeatable, paired with --package-name)
                              --package-name=<n>      Package name in package.json (repeatable, paired with --package-dir)
                              --help                     Show this help

                              --package-dir and --package-name must appear in pairs, in the same order.

                            Examples:
                              PackumentGenerator --base-url=https://example.com/tarballs \
                                --packument-dir=docs --tarball-dir=docs/tarballs \
                                --package-dir=my-lib --package-name=com.example.mylib

                              PackumentGenerator --repo-path=/path/to/repo \
                                --base-url=https://user.github.io/repo/tarballs \
                                --packument-dir=docs --tarball-dir=docs/tarballs \
                                --package-dir=my-lib   --package-name=com.example.mylib \
                                --package-dir=my-utils --package-name=com.example.utils
                            """);
    return parsed.ShowHelp ? 0 : 1;
}

var git = new GitService(parsed.RepoPath!);
var logger = new ConsoleLogger();
var builder = new PackumentBuilder(git, parsed.BaseUrl!, parsed.PackumentDir!, parsed.TarballDir!, logger);

var allSucceeded = true;
foreach (var spec in parsed.Packages)
{
    if (!builder.Generate(spec)) {
        allSucceeded = false;
    }
}

return allSucceeded ? 0 : 1;

// ---------------------------------------------------------------------------

static ParsedArgs ParseArgs(string[] args)
{
    string? repoPath = null;
    string? baseUrl = null;
    string? packumentDir = null;
    string? tarballDir = null;
    var packageDirs = new List<string>();
    var packageNames = new List<string>();
    var showHelp = false;

    for (var i = 0; i < args.Length; i++)
    {
        if (TryGetValue(args, ref i, "--repo-path", out var v)) {
            repoPath = v;
        } else if (TryGetValue(args, ref i, "--base-url", out v)) {
            baseUrl = v;
        } else if (TryGetValue(args, ref i, "--packument-dir", out v)) {
            packumentDir = v;
        } else if (TryGetValue(args, ref i, "--tarball-dir", out v)) {
            tarballDir = v;
        } else if (TryGetValue(args, ref i, "--package-dir", out v)) {
            packageDirs.Add(v);
        } else if (TryGetValue(args, ref i, "--package-name", out v)) {
            packageNames.Add(v);
        } else if (args[i] == "--help" || args[i] == "-h") {
            showHelp = true;
        } else {
            return new ParsedArgs { Error = $"Unknown argument: {args[i]}" };
        }
    }

    repoPath ??= Directory.GetCurrentDirectory();

    string? error = null;
    if (!showHelp)
    {
        if (baseUrl is null) {
            error = "--base-url is required.";
        } else if (packumentDir is null) {
            error = "--packument-dir is required.";
        } else if (tarballDir is null) {
            error = "--tarball-dir is required.";
        } else if (packageDirs.Count == 0 && packageNames.Count == 0) {
            error = "At least one --package-dir / --package-name pair is required.";
        } else if (packageDirs.Count != packageNames.Count) {
            error = $"--package-dir ({packageDirs.Count}) and --package-name ({packageNames.Count}) must appear in equal numbers.";
        }
    }

    // --package-dir と --package-name は出現順でペアリングされる。
    // 例: --package-dir=A --package-dir=B --package-name=X --package-name=Y
    //     → (A, X), (B, Y) ※順序が交互でなくても正しくペアになる
    var packages = packageDirs
        .Zip(packageNames, (dir, name) => new PackageSpec(dir, name))
        .ToList();

    return new ParsedArgs
    {
        RepoPath = repoPath,
        BaseUrl = baseUrl,
        PackumentDir = packumentDir,
        TarballDir = tarballDir,
        Packages = packages,
        ShowHelp = showHelp,
        Error = error,
    };
}

// --key=value と --key value の両形式に対応する。
// マッチした場合は i を消費済み位置まで進める。
static bool TryGetValue(string[] args, ref int i, string name, out string value)
{
    var arg = args[i];

    if (arg.StartsWith(name + "=", StringComparison.Ordinal))
    {
        value = arg[(name.Length + 1)..];
        return true;
    }

    if (arg == name && i + 1 < args.Length)
    {
        value = args[++i];
        return true;
    }

    value = "";
    return false;
}

internal record ParsedArgs
{
    public string? RepoPath { get; init; }
    public string? BaseUrl { get; init; }
    public string? PackumentDir { get; init; }
    public string? TarballDir { get; init; }
    public List<PackageSpec> Packages { get; init; } = [];
    public bool ShowHelp { get; init; }
    public string? Error { get; init; }
    public bool IsValid => Error is null && !ShowHelp;
}
