using System.Diagnostics;

namespace PackumentGenerator;

/// <summary>
/// git 操作の抽象化。Unity Editor Script で使う場合は
/// LibGit2Sharp ベースの実装などに差し替えられる。
/// </summary>
public interface IGitService {
    IReadOnlyList<string> GetTags();

    /// <summary>
    /// 指定コミットの特定パスのファイル内容を返す。
    /// ファイルが存在しない場合は null。作業ツリーには触れない。
    /// </summary>
    string? TryGetFileContent(string commitish, string path);

    /// <summary>
    /// git archive で tarball を生成する。
    /// treePath は "v1.0.0:Packages/MyLib" のような tree-ish 形式を期待する。
    /// </summary>
    void CreateArchive(string treePath, string outputPath);
}

/// <summary>
/// System.Diagnostics.Process で git コマンドを実行する実装。
/// Unity Editor でも Process は利用可能。
/// </summary>
public sealed class GitService(string repoPath) : IGitService {
    private readonly string _repoPath = Path.GetFullPath(repoPath);

    public IReadOnlyList<string> GetTags() =>
        InvokeGit("tag", "-l").Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    public string? TryGetFileContent(string commitish, string path)
    {
        try {
            return InvokeGit("show", $"{commitish}:{path}");
        }
        catch {
            return null;
        }
    }

    // --prefix=package/ により、tarball 内の全エントリが package/ 以下に配置される。
    // これは npm/UPM の tarball 規約（ルートが package/ ディレクトリ）に従うため。
    public void CreateArchive(string treePath, string outputPath) =>
        InvokeGit("archive", "--format=tar.gz", "--prefix=package/", "-o", outputPath, treePath);

    private string InvokeGit(params string[] arguments)
    {
        ProcessStartInfo psi = new()
        {
            FileName               = "git",
            WorkingDirectory       = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        // Arguments プロパティ（単一文字列）ではなく ArgumentList を使うことで、
        // スペースや特殊文字を含むパスでもシェルエスケープの問題を回避できる。
        foreach (var arg in arguments) {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var       stdout  = process.StandardOutput.ReadToEnd();
        var       stderr  = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed (exit {process.ExitCode}): {stderr}"
            );
        }

        return stdout;
    }
}
