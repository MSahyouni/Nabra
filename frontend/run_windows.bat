@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo  Meetily - Windows CPU Dev Mode
echo  Project: %CD%
echo ========================================
echo.

call "%~dp0setup_env_windows.bat"

where node >nul 2>&1 || (
  echo [ERROR] Node.js not found. Install from https://nodejs.org
  exit /b 1
)
where cargo >nul 2>&1 || (
  echo [ERROR] Rust/cargo not found. Install from https://rustup.rs
  exit /b 1
)
where cmake >nul 2>&1 || (
  echo [ERROR] CMake not found. Install: winget install Kitware.CMake
  exit /b 1
)
if not exist "node_modules" (
  echo Installing npm dependencies...
  call pnpm.cmd install
  if errorlevel 1 exit /b 1
)

rem Prevent stale production/development chunks from causing ChunkLoadError.
if exist ".next" (
  echo Clearing stale Next.js build cache...
  rmdir /S /Q ".next"
  if errorlevel 1 exit /b 1
)

if not exist "src-tauri\binaries\llama-helper-x86_64-pc-windows-msvc.exe" (
  echo Building llama-helper sidecar...
  pushd "%~dp0.."
  cargo build -p llama-helper --release
  if errorlevel 1 (
    popd
    exit /b 1
  )
  if not exist "frontend\src-tauri\binaries" mkdir "frontend\src-tauri\binaries"
  copy /Y "target\release\llama-helper.exe" "frontend\src-tauri\binaries\llama-helper-x86_64-pc-windows-msvc.exe"
  popd
)

echo.
echo Starting Meetily (tauri:dev:cpu)...
echo Keep this window open while the app runs.
echo.

call pnpm.cmd run tauri:dev:cpu
endlocal
