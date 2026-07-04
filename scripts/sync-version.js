const fs = require('fs');
const path = require('path');

function syncVersion() {
  const currentDir = __dirname;
  const configPath = path.join(currentDir, '..', 'package.json');
  const manifestPath = path.join(currentDir, '..', 'manifest.json');

  const configData = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  const newVersion = configData.version;

  if (!newVersion) {
    console.error('未在 package.json 中找到版本号！');
    process.exit(1);
  }

  const manifestData = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  manifestData.Version = newVersion;

  fs.writeFileSync(manifestPath, JSON.stringify(manifestData, null, 2) + '\n', 'utf8');
  console.log(`成功同步版本号 ${newVersion} 到 manifest.json`);
}

syncVersion();
