// generate-registry.js
// 事前に: npm install tar
// 実行:   node generate-registry.js
// 環境変数: REGISTRY_URL (省略時 http://localhost:4873)

const fs     = require('fs');
const path   = require('path');
const tar    = require('tar');
const crypto = require('crypto');

const DOCS         = path.join(__dirname, 'docs');
const TARBALLS_DIR = path.join(DOCS, 'tarballs');
const REGISTRY_URL = process.env.REGISTRY_URL ?? 'http://localhost:4873';

// ── ハッシュ計算 ────────────────────────────────────────────────
function sha1File(filePath) {
  const data = fs.readFileSync(filePath);
  return crypto.createHash('sha1').update(data).digest('hex');
}

// ── ファイル名パース ────────────────────────────────────────────
// com.example.mypkg-0.123.4.tgz → { name, version }
function parseTarballName(filename) {
  const m = filename.replace(/\.tgz$/, '').match(/^(com\..+?)-(\d+\.\d+\.\d+.*)$/);
  return m ? { name: m[1], version: m[2] } : null;
}

// ── 単純な semver 大小比較 ──────────────────────────────────────
function semverGt(a, b) {
  const pa = a.split(/[-.]/).map(x => parseInt(x) || 0);
  const pb = b.split(/[-.]/).map(x => parseInt(x) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d > 0;
  }
  return false;
}

// ── tgz 内の package.json を読む ───────────────────────────────
async function readPackageJson(tgzPath) {
  return new Promise((resolve) => {
    let found = null;
    tar.t({
      file: tgzPath,
      filter: p => p.endsWith('package.json'),
      onentry: (entry) => {
        const chunks = [];
        entry.on('data', c => chunks.push(c));
        entry.on('end', () => {
          if (!found) {
            try { found = JSON.parse(Buffer.concat(chunks).toString()); } catch {}
          }
        });
      },
    })
    .then(()  => resolve(found ?? {}))
    .catch(() => resolve({}));
  });
}

// ── メイン ─────────────────────────────────────────────────────
async function main() {
  const files = fs.readdirSync(TARBALLS_DIR).filter(f => f.endsWith('.tgz'));
  console.log(`Found ${files.length} tarballs\n`);

  // パッケージ名でグループ化
  const grouped = {};
  for (const filename of files) {
    const parsed = parseTarballName(filename);
    if (!parsed) { console.warn(`  SKIP (unrecognized): ${filename}`); continue; }
    (grouped[parsed.name] ??= []).push({ version: parsed.version, filename });
  }

  const now = new Date().toISOString();

  // packument を生成
  for (const [pkgName, entries] of Object.entries(grouped)) {
    console.log(`[${pkgName}]  ${entries.length} version(s)`);
    let latest = null;
    const versions = {};
    const time = {};

    for (const { version, filename } of entries) {
      const tgzPath = path.join(TARBALLS_DIR, filename);
      const pkgJson = await readPackageJson(tgzPath);
      const shasum  = sha1File(tgzPath);

      // 不要フィールドを除外してから展開
      const { keywords, samples, repository, ...restPkgJson } = pkgJson;

      versions[version] = {
        // デフォルト値
        name:         pkgName,
        version,
        displayName:  '',
        description:  '',
        unity:        '',
        dependencies: {},
        // package.json の内容で上書き
        ...restPkgJson,
        // 生成フィールド（上書き禁止）
        _id:          `${pkgName}@${version}`,
        maintainers:  [],
        contributors: [],
        dist: {
          tarball: `${REGISTRY_URL}/tarballs/${filename}`,
          shasum,
        },
      };

      time[version] = now;

      if (!latest || semverGt(version, latest)) latest = version;
      console.log(`  + ${version}`);
    }

    const packument = {
      _id:          pkgName,
      name:         pkgName,
      description:  versions[latest]?.description ?? '',
      'dist-tags':  { latest },
      versions,
      time:         { modified: now, created: now, ...time },
      users:        {},
      readme:       'ERROR: No README data found!',
      _attachments: {},
    };

    const outPath = path.join(DOCS, pkgName);
    fs.writeFileSync(outPath, JSON.stringify(packument, null, 2), 'utf8');
    console.log(`  → wrote ${outPath}\n`);
  }

  // /-/all を生成
  console.log('Generating /-/all ...');
  const allResult = { _updated: Date.now() };
  for (const pkgName of Object.keys(grouped)) {
    allResult[pkgName] = JSON.parse(
      fs.readFileSync(path.join(DOCS, pkgName), 'utf8')
    );
  }
  const allDir = path.join(DOCS, '-');
  if (!fs.existsSync(allDir)) fs.mkdirSync(allDir, { recursive: true });
  fs.writeFileSync(path.join(allDir, 'all'), JSON.stringify(allResult), 'utf8');

  console.log(`Done. ${Object.keys(grouped).length} packages, docs/-/all updated.`);
}

main().catch(e => { console.error(e); process.exit(1); });
