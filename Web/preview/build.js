// Inlines engine.js and game.js into the page. Outputs:
//   dist/next-xi-preview.html  - page body (published as an artifact; the host adds <html>/<head>)
//   dist/standalone.html       - full HTML document to open locally in a browser
const fs = require('fs');
const path = require('path');
const src = f => fs.readFileSync(path.join(__dirname, 'src', f), 'utf8');
const page = src('page.html')
  .replace('/*__ENGINE__*/', () => src('engine.js'))
  .replace('/*__GAME__*/', () => src('game.js'));
fs.mkdirSync(path.join(__dirname, 'dist'), { recursive: true });
fs.writeFileSync(path.join(__dirname, 'dist', 'next-xi-preview.html'), page);
fs.writeFileSync(path.join(__dirname, 'dist', 'standalone.html'),
  '<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n</head>\n<body>\n' + page + '\n</body>\n</html>\n');
console.log('built', (page.length / 1024).toFixed(0) + ' KB');
