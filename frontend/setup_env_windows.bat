@echo off
REM Shared Windows environment for Meetily (CPU build)
REM Source this from other scripts, or run before manual cargo/pnpm commands.

set "PATH=C:\Program Files\CMake\bin;C:\Program Files\LLVM\bin;%USERPROFILE%\.cargo\bin;C:\Program Files\nodejs;%APPDATA%\npm;%PATH%"
set "LIBCLANG_PATH=C:\Program Files\LLVM\bin"
REM Required on Windows with LLVM 22: use crate-bundled Whisper bindings
set "WHISPER_DONT_GENERATE_BINDINGS=1"
set "CARGO_TARGET_DIR=%~dp0..\target"
