import console from 'node:console';
import { spawnSync } from 'node:child_process';
import process from 'node:process';
import { fileURLToPath, URL } from 'node:url';

const scriptPath = fileURLToPath(
  new URL('./copy-curated-assets.py', import.meta.url),
);
const pythonCommand = process.platform === 'win32' ? 'python' : 'python3';
const result = spawnSync(pythonCommand, [scriptPath], { stdio: 'inherit' });

if (result.error) {
  console.error(`Could not start ${pythonCommand}: ${result.error.message}`);
  process.exitCode = 1;
} else {
  process.exitCode = result.status ?? 1;
}
