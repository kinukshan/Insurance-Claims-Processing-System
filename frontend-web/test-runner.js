import { spawn } from 'node:child_process'

// Filter out Jest-specific flags like --watchAll so Vitest executes cleanly
const args = process.argv.slice(2).filter((arg) => !arg.startsWith('--watchAll'))
const cmd = process.platform === 'win32' ? 'npx.cmd' : 'npx'
const child = spawn(cmd, ['vitest', 'run', ...args], {
  stdio: 'inherit',
  shell: true,
})
child.on('close', (code) => process.exit(code ?? 0))
