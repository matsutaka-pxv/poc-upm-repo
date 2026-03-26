#!/usr/bin/env node

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// ---- 引数パース ----
const args = process.argv.slice(2);
const get = (flag) => {
  const i = args.indexOf(flag);
  return i !== -1 ? args[i + 1] : null;
};

const packageDir = get('--package');
const registryDir = get('--registry');
const registryUrl = get('--url') ?? 'http://localhost:4873';

if (!packageDir || !registryDir) {
  console.error('Usage: node publish.js --package <packageDir> --registry <registryDir> [--url <registryUrl>]');
  process.exit(1);
}

// ---- package.json を読む ----
const packageJsonPath = path.join(packageDir, 'package.json');
if (!fs.existsSync(packageJsonPath)) {
  console.error(`package.json not found: ${packageJsonPath}`);
  process.exit(1);
}

const pkg = JSON.parse(fs.readFileSync(packageJsonPath, 'utf-8'));
const { name, version } = pkg;

if (!name || !version) {
  console.error('package.json に name または version がありません');
  process.exit(1);
}

console.log(`Publishing ${name}@${version}`);

// ---- npm pack を実行 ----
const tarballName = `${name}-${version}.tgz`;
const tarballsDir = path.join(registryDir, 'tarballs');

if (!fs.existsSync(tarballsDir)) {
  fs.mkdirSync(tarballsDir, { recursive: true });
}

console.log('Running npm pack...');
execSync('npm pack', { cwd: packageDir, stdio: 'inherit' });

const generatedTarball = path.join(packageDir, tarballName);
if (!fs.existsSync(generatedTarball)) {
  console.error(`tarball が生成されませんでした: ${generatedTarball}`);
  process.exit(1);
}

// ---- tarball を registry/tarballs/ に移動 ----
const destTarball = path.join(tarballsDir, tarballName);
fs.copyFileSync(generatedTarball, destTarball);
fs.unlinkSync(generatedTarball);
console.log(`Tarball -> ${destTarball}`);

// ---- packument を更新 ----
const packumentPath = path.join(registryDir, name);
let packument = {
  name,
  versions: {},
  'dist-tags': {},
};

if (fs.existsSync(packumentPath)) {
  packument = JSON.parse(fs.readFileSync(packumentPath, 'utf-8'));
}

const tarballUrl = `${registryUrl}/tarballs/${tarballName}`;

// package.json の全フィールドをベースにしつつ dist を上書き
packument.versions[version] = {
  ...pkg,
  dist: { tarball: tarballUrl },
};

// dist-tags.latest を更新（セマンティックバージョンでソート）
const sortedVersions = Object.keys(packument.versions).sort((a, b) => {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < 3; i++) {
    if (pa[i] !== pb[i]) return pa[i] - pb[i];
  }
  return 0;
});
packument['dist-tags'].latest = sortedVersions[sortedVersions.length - 1];
packument.modified = new Date().toISOString();

fs.writeFileSync(packumentPath, JSON.stringify(packument, null, 2));
console.log(`Packument updated: ${packumentPath}`);
console.log(`dist-tags.latest -> ${packument['dist-tags'].latest}`);
console.log('Done.');
