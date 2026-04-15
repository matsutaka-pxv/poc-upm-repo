using System.Text.Json;
using System.Text.Json.Nodes;

var parsed = ParseArgs(args);

if (parsed.ShowHelp || ! parsed.IsValid) {
    if (! parsed.ShowHelp && parsed.Error is not null)
        Console.Error.WriteLine($"Error: {parsed.Error}\n");

    Console.Error.WriteLine(
        """
        Usage: AllFileGenerator [options]

        packument-dir 内の packument ファイルを読み取り、
        npm レジストリの /-/all エンドポイントに相当する一覧ファイルを生成する。

        Options:
          --packument-dir=<dir>    packument ファイルが格納されたディレクトリ (required)
          --help                   Show this help

        出力先: <packument-dir>/-/all

        Examples:
          AllFileGenerator --packument-dir=docs
        """
    );
    return parsed.ShowHelp ? 0 : 1;
}

var packumentDir = Path.GetFullPath(parsed.PackumentDir!);

if (! Directory.Exists(packumentDir)) {
    Console.Error.WriteLine($"Directory not found: {packumentDir}");
    return 1;
}

// packument ファイル名は "com.example.mylib" のようにドットを含むため、
// Path.GetExtension では判別できない。
// 代わりに、明らかに packument ではない拡張子のファイルを除外し、
// 残りを JSON パースして name / dist-tags の有無で判定する。
var excludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".tgz", ".gz", ".tar", ".zip",
    ".json", ".md", ".txt", ".yml", ".yaml",
    ".html", ".css", ".js",
    ".gitkeep", ".gitignore",
};

List<string> candidates = Directory.EnumerateFiles(packumentDir)
    .Where(f => ! excludedExtensions.Contains(Path.GetExtension(f)))
    .ToList();

if (candidates.Count == 0) {
    Console.Error.WriteLine($"No packument files found in: {packumentDir}");
    return 1;
}

var allEntries = new JsonObject();

foreach (var filePath in candidates) {
    JsonNode? packument;
    try {
        var text = File.ReadAllText(filePath);
        packument = JsonNode.Parse(text);
    }
    catch {
        Console.WriteLine($"  Skipping (invalid JSON): {Path.GetFileName(filePath)}");
        continue;
    }

    var name     = packument?["name"]?.GetValue<string>();
    var distTags = packument?["dist-tags"];

    if (name is null || distTags is null) {
        Console.WriteLine($"  Skipping (missing name or dist-tags): {Path.GetFileName(filePath)}");
        continue;
    }

    // /-/all の各エントリには name と dist-tags のみを含める。
    // versions の全情報は含めない。クライアントは個別の packument を取得して詳細を得る。
    allEntries[name] = new JsonObject
    {
        ["name"]      = name,
        ["dist-tags"] = distTags.DeepClone(),
    };

    var latest = distTags["latest"]?.GetValue<string>() ?? "?";
    Console.WriteLine($"  {name} (latest: {latest})");
}

if (allEntries.Count == 0) {
    Console.Error.WriteLine("No valid packument files found.");
    return 1;
}

// npm レジストリの /-/all に対応するパス。
// 静的ホスティングでは <packument-dir>/-/all というファイルとして配置する。
var outputDir = Path.Combine(packumentDir, "-");
Directory.CreateDirectory(outputDir);

var outputPath  = Path.Combine(outputDir, "all");
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(outputPath, allEntries.ToJsonString(jsonOptions));

Console.WriteLine($"\nGenerated: {outputPath} ({allEntries.Count} package(s))");
return 0;

// ---------------------------------------------------------------------------

static ParsedArgs ParseArgs(string[] args)
{
    string? packumentDir = null;
    var     showHelp     = false;

    for (var i = 0; i < args.Length; i++) {
        if (TryGetValue(args, ref i, "--packument-dir", out var v))
            packumentDir = v;
        else if (args[i] == "--help" || args[i] == "-h")
            showHelp = true;
        else
            return new ParsedArgs { Error = $"Unknown argument: {args[i]}" };
    }

    string? error = null;
    if (! showHelp) {
        if (packumentDir is null)
            error = "--packument-dir is required.";
    }

    return new ParsedArgs
    {
        PackumentDir = packumentDir,
        ShowHelp     = showHelp,
        Error        = error,
    };
}

// --key=value と --key value の両形式に対応する。
// マッチした場合は i を消費済み位置まで進める。
static bool TryGetValue(string[] args, ref int i, string name, out string value)
{
    var arg = args[i];

    if (arg.StartsWith(name + "=", StringComparison.Ordinal)) {
        value = arg[(name.Length + 1)..];
        return true;
    }

    if (arg == name && i + 1 < args.Length) {
        value = args[++i];
        return true;
    }

    value = "";
    return false;
}

internal record ParsedArgs {
    public string? PackumentDir { get; init; }
    public bool ShowHelp { get; init; }
    public string? Error { get; init; }
    public bool IsValid => Error is null && ! ShowHelp;
}
