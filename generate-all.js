// generate-all.js
const fs = require('fs');
const path = require('path');

const DOCS = path.join(__dirname, 'docs');
const OUT  = path.join(DOCS, '-', 'all');

function generateAll() {
  const result = {
    _updated: Date.now(),
  };

  const entries = fs.readdirSync(DOCS, { withFileTypes: true });

  for (const entry of entries) {
    if (!entry.isFile()) continue;
    if (!entry.name.startsWith('com.')) continue;

    const filePath = path.join(DOCS, entry.name);
    let packument;
    try {
      packument = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    } catch (e) {
      console.warn(`SKIP (parse error): ${entry.name} - ${e.message}`);
      continue;
    }

    const name = packument.name ?? entry.name;
    result[name] = packument;
    console.log(`  + ${name}`);
  }

  // docs/-/ ディレクトリがなければ作成
  const outDir = path.dirname(OUT);
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  fs.writeFileSync(OUT, JSON.stringify(result), 'utf8');
  console.log(`\nWrote ${OUT}  (${Object.keys(result).length - 1} packages)`);
}

generateAll();
