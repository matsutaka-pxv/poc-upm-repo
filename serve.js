// 以下のコマンドで起動してください
// cd registry
// node serve.js

const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 4873;
const ROOT = __dirname;

const MIME = {
  '.tgz': 'application/octet-stream',
  '.gz':  'application/octet-stream',
  '':     'application/json',   // 拡張子なし → packument
};

http.createServer((req, res) => {
  const filePath = path.join(ROOT, req.url.split('?')[0]);

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end('Not found: ' + req.url);
      return;
    }

    const ext = path.extname(filePath);
    const contentType = MIME[ext] ?? 'application/octet-stream';

    res.writeHead(200, { 'Content-Type': contentType });
    res.end(data);
  });
}).listen(PORT, () => {
  console.log(`Registry running at http://localhost:${PORT}`);
});
