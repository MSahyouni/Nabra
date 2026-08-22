#!/usr/bin/env node
/**
 * Run Tauri with CUDA, ensuring CUDA_PATH is set for whisper-rs-sys.
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const command = process.argv[2];
if (!command || !['dev', 'build'].includes(command)) {
  console.error('Usage: node tauri-cuda.js [dev|build]');
  process.exit(1);
}

function findVcvars() {
  const candidates = [
    'C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\VC\\Auxiliary\\Build\\vcvars64.bat',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\VC\\Auxiliary\\Build\\vcvars64.bat',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\VC\\Auxiliary\\Build\\vcvars64.bat',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\VC\\Auxiliary\\Build\\vcvars64.bat',
  ];
  return candidates.find((candidate) => fs.existsSync(candidate));
}

function findWindowsSdkBin() {
  const kitsBin = 'C:\\Program Files (x86)\\Windows Kits\\10\\bin';
  if (!fs.existsSync(kitsBin)) return null;
  const versions = fs.readdirSync(kitsBin).sort().reverse();
  for (const version of versions) {
    const dir = path.join(kitsBin, version, 'x64');
    if (fs.existsSync(path.join(dir, 'rc.exe'))) {
      return dir;
    }
  }
  return null;
}

function findCudaPath() {
  const nvccName = process.platform === 'win32' ? 'nvcc.exe' : 'nvcc';
  if (process.env.CUDA_PATH) {
    if (fs.existsSync(path.join(process.env.CUDA_PATH, 'bin', nvccName))) {
      return process.env.CUDA_PATH;
    }
  }

  if (process.platform !== 'win32') {
    return null;
  }

  const versions = [
    'v12.8', 'v12.6', 'v12.5', 'v12.4', 'v12.3', 'v12.2', 'v12.1', 'v12.0', 'v11.8',
  ];
  const base = 'C:\\Program Files\\NVIDIA GPU Computing Toolkit\\CUDA';
  for (const version of versions) {
    const root = path.join(base, version);
    if (fs.existsSync(path.join(root, 'bin', nvccName))) {
      return root;
    }
  }
  return null;
}

function findLibclangPath() {
  const candidates = [
    'C:\\Program Files\\LLVM\\bin',
    'C:\\LLVM\\bin',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\VC\\Tools\\Llvm\\x64\\bin',
    'C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\VC\\Tools\\Llvm\\x64\\bin',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\VC\\Tools\\Llvm\\x64\\bin',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\VC\\Tools\\Llvm\\x64\\bin',
  ];
  if (process.env.LIBCLANG_PATH && fs.existsSync(path.join(process.env.LIBCLANG_PATH, 'libclang.dll')) && !process.env.LIBCLANG_PATH.endsWith('\\Llvm\\bin')) {
    return process.env.LIBCLANG_PATH;
  }
  return candidates.find((dir) => fs.existsSync(path.join(dir, 'libclang.dll')));
}

const cudaPath = findCudaPath();
const libclangPath = findLibclangPath();
const env = { ...process.env };

if (libclangPath) {
  env.LIBCLANG_PATH = libclangPath;
  env.Path = `${libclangPath};${env.Path || env.PATH || ''}`;
  env.PATH = env.Path;
  console.log(`OK: LIBCLANG_PATH=${libclangPath}`);
}

if (cudaPath) {
  env.CUDA_PATH = cudaPath;
  env.Path = `${path.join(cudaPath, 'bin')};${env.Path}`;
  env.PATH = env.Path;
  console.log(`OK: CUDA_PATH=${cudaPath}`);
} else {
  console.error('ERROR: CUDA Toolkit not found. Install CUDA and/or set CUDA_PATH.');
  process.exit(1);
}

if (process.platform === 'win32') {
  // VS generator needs CUDA Visual Studio Integration (CUDA*.props).
  // That component is missing, so compile CUDA with Ninja + nvcc instead.
  const ninjaCandidates = [
    'C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\Common7\\IDE\\CommonExtensions\\Microsoft\\CMake\\Ninja\\ninja.exe',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\Common7\\IDE\\CommonExtensions\\Microsoft\\CMake\\Ninja\\ninja.exe',
    'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\CMake\\Ninja\\ninja.exe',
    'C:\\Program Files\\Ninja\\ninja.exe',
  ];
  const ninja = ninjaCandidates.find((candidate) => fs.existsSync(candidate));
  if (!ninja) {
    console.error('ERROR: ninja.exe not found. Install CMake/Ninja via Visual Studio Build Tools.');
    process.exit(1);
  }

  // Do not point CC/CXX at cl.exe directly; Ninja needs the full MSVC+SDK env (rc.exe, mt.exe, LIB).
  delete env.CC;
  delete env.CXX;

  env.CMAKE_GENERATOR = 'Ninja';
  env.CMAKE_MAKE_PROGRAM = ninja.replace(/\\/g, '/');
  env.CMAKE_CUDA_COMPILER = path.join(cudaPath, 'bin', 'nvcc.exe').replace(/\\/g, '/');
  env.NVCC_PREAPPEND_FLAGS = '-allow-unsupported-compiler -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -D_ALLOW_COMPILER_NOT_SUPPORTED';
  env.CMAKE_CUDA_FLAGS = '-allow-unsupported-compiler -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -D_ALLOW_COMPILER_NOT_SUPPORTED';
  env.Path = `${path.dirname(ninja)};${env.Path}`;

  const sdkBin = findWindowsSdkBin();
  if (sdkBin) {
    env.Path = `${sdkBin};${env.Path}`;
    env.CMAKE_RC_COMPILER = path.join(sdkBin, 'rc.exe').replace(/\\/g, '/');
    env.CMAKE_MT = path.join(sdkBin, 'mt.exe').replace(/\\/g, '/');
  }
  env.PATH = env.Path;

  console.log(`OK: CMAKE_GENERATOR=Ninja (${ninja})`);
  console.log(`OK: CMAKE_CUDA_COMPILER=${env.CMAKE_CUDA_COMPILER}`);

  for (const profile of ['debug', 'release']) {
    const buildRoot = path.join(__dirname, '..', '..', 'target', profile, 'build');
    if (fs.existsSync(buildRoot)) {
      for (const name of fs.readdirSync(buildRoot)) {
        if (!name.startsWith('whisper-rs-sys-')) continue;
        const cmakeBuild = path.join(buildRoot, name, 'out', 'build');
        if (fs.existsSync(cmakeBuild)) {
          fs.rmSync(cmakeBuild, { recursive: true, force: true });
          console.log(`Cleared stale whisper-rs-sys CMake cache in ${profile}`);
        }
      }
    }
  }

  if (!env.BINDGEN_EXTRA_CLANG_ARGS) {
    const clangArgs = ['--target=x86_64-pc-windows-msvc'];
    const msvcRoots = [
      'C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\VC\\Tools\\MSVC',
      'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\VC\\Tools\\MSVC',
      'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\VC\\Tools\\MSVC',
    ];
    for (const root of msvcRoots) {
      if (!fs.existsSync(root)) continue;
      const versions = fs.readdirSync(root);
      for (const version of versions) {
        const includeDir = path.join(root, version, 'include');
        if (fs.existsSync(path.join(includeDir, 'stdbool.h'))) {
          clangArgs.push(`-I"${includeDir}"`);
          break;
        }
      }
    }
    const kitsRoot = 'C:\\Program Files (x86)\\Windows Kits\\10\\Include';
    if (fs.existsSync(kitsRoot)) {
      const kits = fs.readdirSync(kitsRoot).sort().reverse();
      if (kits.length > 0) {
        clangArgs.push(`-I"${path.join(kitsRoot, kits[0], 'ucrt')}"`);
        clangArgs.push(`-I"${path.join(kitsRoot, kits[0], 'shared')}"`);
        clangArgs.push(`-I"${path.join(kitsRoot, kits[0], 'um')}"`);
      }
    }
    env.BINDGEN_EXTRA_CLANG_ARGS = clangArgs.join(' ');
  }
}
function sanitizeEnvForVs2022(envObj) {
  const vs18Pattern = /[\\\/]Microsoft Visual Studio[\\\/]18[\\\/]/i;
  ['PATH', 'Path', 'INCLUDE', 'LIB', 'LIBPATH'].forEach((varName) => {
    if (envObj[varName]) {
      envObj[varName] = envObj[varName]
        .split(';')
        .filter((entry) => !vs18Pattern.test(entry))
        .join(';');
    }
  });

  const vs2022Dir = 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise';
  const msvcVersion = '14.44.35207';
  if (fs.existsSync(vs2022Dir)) {
    envObj.VSINSTALLDIR = `${vs2022Dir}\\`;
    envObj.VCINSTALLDIR = `${vs2022Dir}\\VC\\`;
    envObj.VCToolsInstallDir = `${vs2022Dir}\\VC\\Tools\\MSVC\\${msvcVersion}\\`;
    envObj.VisualStudioVersion = '17.0';
  }
}

sanitizeEnvForVs2022(env);

const userCargoBin = path.join(process.env.USERPROFILE || '', '.cargo', 'bin');
const nodeModulesBin = path.join(__dirname, '..', 'node_modules', '.bin');
env.Path = `${userCargoBin};${nodeModulesBin};${env.Path}`;
env.PATH = env.Path;

const tauriBin = path.join(nodeModulesBin, process.platform === 'win32' ? 'tauri.cmd' : 'tauri');
const tauriCmd = fs.existsSync(tauriBin)
  ? `"${tauriBin}" ${command} -- --features cuda`
  : `npx tauri ${command} -- --features cuda`;

// A clean checkout has no Tauri sidecar yet. Build it inside the same CUDA/MSVC
// environment as the desktop application so bindgen never picks a 32-bit LLVM.
const repoRoot = path.resolve(__dirname, '..', '..');
const sidecarDir = path.join(repoRoot, 'frontend', 'src-tauri', 'binaries');
const sidecarSource = path.join(repoRoot, 'target', 'release', process.platform === 'win32' ? 'llama-helper.exe' : 'llama-helper');
const sidecarTarget = path.join(sidecarDir, process.platform === 'win32'
  ? 'llama-helper-x86_64-pc-windows-msvc.exe'
  : 'llama-helper-x86_64-unknown-linux-gnu');
fs.mkdirSync(sidecarDir, { recursive: true });

const helperBuildCmd = 'cargo build -p llama-helper --release --features cuda';
const copySidecarCmd = process.platform === 'win32'
  ? `copy /Y "${sidecarSource}" "${sidecarTarget}"`
  : `cp "${sidecarSource}" "${sidecarTarget}"`;
const buildCmd = command === 'build'
  ? `${helperBuildCmd} && ${copySidecarCmd} && ${tauriCmd}`
  : tauriCmd;

function findMsvcVersion(vcvarsPath) {
  if (!vcvarsPath) return null;
  const vsDir = path.dirname(path.dirname(path.dirname(path.dirname(vcvarsPath))));
  const msvcDir = path.join(vsDir, 'Tools', 'MSVC');
  if (fs.existsSync(msvcDir)) {
    const versions = fs.readdirSync(msvcDir).sort().reverse();
    if (versions.length > 0) {
      return versions[0];
    }
  }
  return null;
}

const vcvars = process.platform === 'win32' ? findVcvars() : null;
const msvcVersion = findMsvcVersion(vcvars);
const vcvarsArgs = msvcVersion ? `-vcvars_ver=${msvcVersion}` : '';

try {
  if (vcvars) {
    console.log(`OK: Loading MSVC environment from ${vcvars} ${vcvarsArgs}`);
    execSync(`cmd.exe /c call "${vcvars}" ${vcvarsArgs} && ${buildCmd}`, {
      stdio: 'inherit',
      env,
    });
  } else {
    execSync(buildCmd, {
      stdio: 'inherit',
      env,
      shell: true,
    });
  }
} catch (err) {
  process.exit(err.status || 1);
}
