const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 4873;
const ROOT = path.join(__dirname, 'docs');
const MIME = {
  '.tgz': 'application/octet-stream',
  '.gz':  'application/octet-stream',
  '':     'application/json',
};

http.createServer((req, res) => {
  const urlPath = req.url.split('?')[0];
  const filePath = path.join(ROOT, urlPath);

  fs.readFile(filePath, (err, data) => {
    const status = err ? 404 : 200;
    const ext = path.extname(filePath);
    const contentType = err ? 'text/plain' : (MIME[ext] ?? 'application/octet-stream');

    // ログ: タイムスタンプ / メソッド / URL / ステータス / ヘッダー
    console.log([
      new Date().toISOString(),
      req.method,
      req.url,
      `→ ${status}`,
    ].join('  '));

    // リクエストヘッダーの詳細（Unityが何を送ってくるか確認用）
    console.log('  Headers:', JSON.stringify(req.headers, null, 2));

    if (err) {
      res.writeHead(404);
      res.end('Not found: ' + req.url);
      return;
    }

    res.writeHead(200, { 'Content-Type': contentType });
    res.end(data);
  });
}).listen(PORT, () => {
  console.log(`Registry running at http://localhost:${PORT}`);
});
