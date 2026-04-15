using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PackumentGenerator;

/// <summary>
/// Packages/ 下のディレクトリ名と package.json 内の name の対応。
/// Unity ではディレクトリ名（例: "MyLib"）とパッケージ名（例: "com.example.mylib"）が
/// 一致しないことが多いため、両方を明示的に保持する。
/// </summary>
public record PackageSpec(string DirectoryName, string PackageName);

/// <summary>
/// Unity Editor Script で使う場合は Debug.Log ベースの実装に差し替える。
/// </summary>
public interface ILogger
{
    void Info(string message);
    void Error(string message);
}

public sealed class ConsoleLogger : ILogger
{
    public void Info(string message) => Console.WriteLine(message);
    public void Error(string message) => Console.Error.WriteLine(message);
}

/// <summary>
/// git リポジトリのタグから UPM 互換の packument JSON と tarball を生成する。
/// 
/// 生成物:
///   - packument JSON: npm レジストリプロトコル互換。UPM クライアントが
///     GET /{パッケージ名} で取得するファイルに相当する。拡張子なし。
///   - tarball (.tgz): 各バージョンのパッケージ内容。UPM が dist.tarball の
///     URL からダウンロードしてインストールする。
/// </summary>
public sealed class PackumentBuilder
{
    private readonly IGitService _git;
    private readonly string _baseUrl;
    private readonly string _packumentDir;
    private readonly string _tarballDir;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public PackumentBuilder(IGitService git, string baseUrl, string packumentDir, string tarballDir, ILogger logger)
    {
        _git = git;
        _baseUrl = baseUrl.TrimEnd('/');

        // git は WorkingDirectory（リポジトリルート）基準で動作するため、
        // 相対パスのままだと git archive -o の出力先がずれる。絶対パスに変換して回避する。
        _packumentDir = Path.GetFullPath(packumentDir);
        _tarballDir = Path.GetFullPath(tarballDir);

        _logger = logger;
    }

    public bool Generate(PackageSpec spec)
    {
        _logger.Info($"Processing package: {spec.PackageName} (directory: {spec.DirectoryName})");

        var versionTags = DiscoverVersionTags();
        if (versionTags.Count == 0)
        {
            _logger.Error("No semver tags found.");
            return false;
        }

        Directory.CreateDirectory(_tarballDir);

        var versions = new JsonObject();
        SemVer? latestVersion = null;

        foreach (var (tag, version) in versionTags)
        {
            // git show <tag>:<path> で、特定コミット時点のファイル内容を作業ツリーに触れずに取得する。
            // そのタグ時点でパッケージが存在しない場合は null が返る。
            var packageJsonPath = $"Packages/{spec.DirectoryName}/package.json";
            var packageJsonText = _git.TryGetFileContent(tag, packageJsonPath);
            if (packageJsonText is null)
            {
                _logger.Info($"  {tag}: {packageJsonPath} not found, skipping.");
                continue;
            }

            var packageJson = JsonNode.Parse(packageJsonText);
            if (packageJson is null)
            {
                _logger.Error($"  {tag}: Failed to parse package.json, skipping.");
                continue;
            }

            var versionStr = version!.ToString();
            _logger.Info($"  {tag} -> {versionStr}");

            var tarballFileName = $"{spec.PackageName}-{versionStr}.tgz";
            var tarballPath = Path.Combine(_tarballDir, tarballFileName);

            // tree-ish を <tag>:Packages/<dir> にすることで、サブディレクトリがアーカイブのルートになる。
            // --prefix=package/ と合わせて package/package.json, package/Runtime/... という
            // UPM が期待する tarball 内構造が得られる。
            // もし <tag> -- Packages/<dir>/ の形式にすると package/Packages/<dir>/... になってしまう。
            _git.CreateArchive($"{tag}:Packages/{spec.DirectoryName}", tarballPath);

            // npm レジストリプロトコルでは dist.shasum に SHA-1 ハッシュを使用する。
            var shasum = ComputeSha1(tarballPath);

            // packument の各バージョンエントリは package.json の内容 + dist フィールド。
            // dist.tarball は UPM クライアントがパッケージをダウンロードする URL。
            packageJson["dist"] = new JsonObject
            {
                ["tarball"] = $"{_baseUrl}/{tarballFileName}",
                ["shasum"] = shasum,
            };

            versions[versionStr] = packageJson;

            if (latestVersion is null || version!.CompareTo(latestVersion) > 0)
                latestVersion = version;
        }

        if (latestVersion is null)
        {
            _logger.Info($"  No versions found for {spec.PackageName}, skipping.");
            return false;
        }

        // dist-tags.latest は UPM クライアントがバージョン未指定時に解決するデフォルトバージョン。
        var packument = new JsonObject
        {
            ["name"] = spec.PackageName,
            ["dist-tags"] = new JsonObject
            {
                ["latest"] = latestVersion.ToString(),
            },
            ["versions"] = versions,
        };

        Directory.CreateDirectory(_packumentDir);

        // packument ファイルは拡張子なし。npm レジストリと同様に
        // GET /{パッケージ名} のパスにそのまま対応させるため。
        var packumentPath = Path.Combine(_packumentDir, spec.PackageName);
        File.WriteAllText(packumentPath, packument.ToJsonString(JsonOptions));
        _logger.Info($"  Packument written to: {packumentPath}");

        return true;
    }

    /// <summary>
    /// 全 git タグから semver として解釈可能なものを抽出し、昇順で返す。
    /// "v1.2.3" と "1.2.3" の両形式を認識する。
    /// </summary>
    private List<(string Tag, SemVer Version)> DiscoverVersionTags()
    {
        var tags = _git.GetTags();
        var result = new List<(string Tag, SemVer Version)>();

        foreach (var tag in tags)
        {
            var version = SemVer.TryParse(tag);
            if (version is not null)
                result.Add((tag, version));
        }

        result.Sort((a, b) => a.Version.CompareTo(b.Version));

        _logger.Info($"Found {result.Count} version tag(s): {string.Join(", ", result.Select(t => t.Tag))}");
        return result;
    }

    private static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// semver 2.0.0 のサブセット実装。pre-release ラベル対応。
/// タグ文字列の先頭 "v" は許容する（"v1.2.3" → 1.2.3）。
/// </summary>
public sealed record SemVer(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<SemVer>
{
    private static readonly Regex Pattern = new(@"^v?(\d+)\.(\d+)\.(\d+)(?:-(.+))?$", RegexOptions.Compiled);

    public static SemVer? TryParse(string tag)
    {
        var m = Pattern.Match(tag);
        if (!m.Success) return null;
        return new SemVer(
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            int.Parse(m.Groups[3].Value),
            m.Groups[4].Success ? m.Groups[4].Value : null);
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null) return 1;

        var majorCmp = Major.CompareTo(other.Major);
        if (majorCmp != 0) return majorCmp;

        var minorCmp = Minor.CompareTo(other.Minor);
        if (minorCmp != 0) return minorCmp;

        var patchCmp = Patch.CompareTo(other.Patch);
        if (patchCmp != 0) return patchCmp;

        // semver 仕様: pre-release 付きは同一 M.m.p の正式リリースより優先度が低い。
        // 例: 1.0.0-alpha < 1.0.0
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;
        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public override string ToString() =>
        PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
