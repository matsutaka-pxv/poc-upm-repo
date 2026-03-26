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

// GitHub Pages が誤認識する Content-Type を再現するファイル名
const CONTENT_TYPE_OVERRIDES = {
  '/com.vrmc.gltf': 'model/gltf+json',
};

http.createServer((req, res) => {
  const urlPath = req.url.split('?')[0];
  const filePath = path.join(ROOT, urlPath);

  fs.readFile(filePath, (err, data) => {
    const status = err ? 404 : 200;
    const ext = path.extname(filePath);
    const defaultContentType = err ? 'text/plain' : (MIME[ext] ?? 'application/octet-stream');
    const contentType = CONTENT_TYPE_OVERRIDES[urlPath] ?? defaultContentType;

    console.log([
      new Date().toISOString(),
      req.method,
      req.url,
      `→ ${status}`,
      `(${contentType})`,
    ].join('  '));

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
