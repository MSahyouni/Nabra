@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo  Meetily - Clean Windows rebuild
echo ========================================
echo.
echo This will reinstall frontend deps and rebuild.
echo Project path MUST be a local Windows drive (e.g. C:\dev\...)
echo Do NOT use \\wsl.localhost\ paths.
echo.
pause

call "%~dp0setup_env_windows.bat"

echo.
echo [1/3] Reinstalling frontend dependencies...
if exist node_modules rd /s /q node_modules
if exist package-lock.json del /f /q package-lock.json
call pnpm.cmd install
if errorlevel 1 exit /b 1

echo.
echo [2/3] Building llama-helper...
pushd "%~dp0.."
cargo build -p llama-helper --release
if errorlevel 1 (
  popd
  exit /b 1
)
if not exist "frontend\src-tauri\binaries" mkdir "frontend\src-tauri\binaries"
copy /Y "target\release\llama-helper.exe" "frontend\src-tauri\binaries\llama-helper-x86_64-pc-windows-msvc.exe"
popd

echo.
echo [3/3] Starting Tauri CPU dev mode...
call pnpm.cmd run tauri:dev:cpu
endlocal
