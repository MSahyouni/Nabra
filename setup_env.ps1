# ==============================================================================
# Nabrh Automated Setup
# ==============================================================================

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       Nabrh - Environment Setup and Run" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check Node.js
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Node.js is not installed." -ForegroundColor Red
    Write-Host "Install it from https://nodejs.org" -ForegroundColor Yellow
    exit 1
}
else {
    $nodeVersion = node -v
    Write-Host "OK: Node.js installed ($nodeVersion)" -ForegroundColor Green
}

# 2. Check pnpm
if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    Write-Host "WARNING: pnpm is not installed. Installing via npm..." -ForegroundColor Yellow
    npm install -g pnpm

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to install pnpm." -ForegroundColor Red
        exit 1
    }
}
else {
    $pnpmVersion = pnpm -v
    Write-Host "OK: pnpm installed (v$pnpmVersion)" -ForegroundColor Green
}

# 3. Check Rust / Cargo
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {

    $userCargoBin = "$env:USERPROFILE\.cargo\bin"

    if (Test-Path "$userCargoBin\cargo.exe") {
        $env:Path += ";$userCargoBin"

        Write-Host "OK: Cargo found at $userCargoBin" -ForegroundColor Green
    }
    else {
        Write-Host "ERROR: Rust/Cargo is not installed." -ForegroundColor Red
        Write-Host "Install Rust from https://rustup.rs" -ForegroundColor Yellow
        exit 1
    }
}
else {
    $cargoVersion = cargo --version
    Write-Host "OK: Rust/Cargo installed ($cargoVersion)" -ForegroundColor Green
}

# 4. Visual Studio C++ / MSVC environment
if (-not $env:INCLUDE) {

    $vsDevShells = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\Launch-VsDevShell.ps1",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\Launch-VsDevShell.ps1",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\Launch-VsDevShell.ps1",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\Launch-VsDevShell.ps1",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\Common7\Tools\Launch-VsDevShell.ps1",
        "C:\Program Files\Microsoft Visual Studio\2019\Community\Common7\Tools\Launch-VsDevShell.ps1"
    )

    foreach ($vsScript in $vsDevShells) {

        if (Test-Path $vsScript) {

            try {
                Write-Host "Loading Visual Studio C++ environment..." -ForegroundColor Cyan

                & $vsScript -Arch amd64 -HostArch amd64

                Write-Host "Visual Studio environment loaded." -ForegroundColor Green

                break
            }
            catch {
                Write-Host "Could not load: $vsScript" -ForegroundColor Yellow
            }
        }
    }
}

# 5. Find MSVC vcruntime.h
$vcruntimeFile = Get-ChildItem `
    "C:\Program Files*\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\include\vcruntime.h" `
    -ErrorAction SilentlyContinue |
    Select-Object -First 1

if ($vcruntimeFile) {

    $msvcIncDir = $vcruntimeFile.DirectoryName
    $msvcToolsRoot = Split-Path (Split-Path $msvcIncDir -Parent) -Parent
    $windowsKitRoot = "C:\Program Files (x86)\Windows Kits\10\Include"
    $windowsKitInc = Get-ChildItem $windowsKitRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1

    $bindgenArgs = @("--target=x86_64-pc-windows-msvc", "-I`"$msvcIncDir`"")

    if ($windowsKitInc) {
        $bindgenArgs += "-I`"$($windowsKitInc.FullName)\ucrt`""
        $bindgenArgs += "-I`"$($windowsKitInc.FullName)\shared`""
        $bindgenArgs += "-I`"$($windowsKitInc.FullName)\um`""
    }

    $env:BINDGEN_EXTRA_CLANG_ARGS = ($bindgenArgs -join " ")

    Write-Host "OK: MSVC headers found:" -ForegroundColor Green
    Write-Host $msvcIncDir -ForegroundColor Green
}
else {

    $env:BINDGEN_EXTRA_CLANG_ARGS =
        "--target=x86_64-pc-windows-msvc"

    Write-Host "WARNING: vcruntime.h was not found." -ForegroundColor Yellow
}

# 6. Find LLVM / libclang
$llvmPaths = @(
    "C:\Program Files\LLVM\bin",
    "C:\LLVM\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\x64\bin",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\x64\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\x64\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\x64\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\bin",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\bin"
)

$foundLlvm = $false

foreach ($p in $llvmPaths) {

    if (Test-Path "$p\libclang.dll") {

        $env:LIBCLANG_PATH = $p
        $env:Path += ";$p"

        Write-Host "OK: LLVM/libclang found at $p" -ForegroundColor Green

        $foundLlvm = $true
        break
    }
}

if (-not $foundLlvm) {

    Write-Host "WARNING: libclang.dll was not found automatically." -ForegroundColor Yellow
    Write-Host "If whisper-rs-sys fails, install LLVM and set LIBCLANG_PATH." -ForegroundColor Yellow
}
elseif (Test-Path "$env:LIBCLANG_PATH\clang.exe") {

    $clangVersion = & "$env:LIBCLANG_PATH\clang.exe" --version 2>$null | Select-Object -First 1
    if ($clangVersion -match "clang version (\d+)") {
        $clangMajor = [int]$Matches[1]
        if ($clangMajor -ge 22) {
            Write-Host "OK: LLVM $clangMajor detected (compatible with whisper-rs 0.16+)." -ForegroundColor Green
        }
    }
}

# 7. Find CUDA Toolkit (required for whisper-rs --features cuda)
if (-not $env:CUDA_PATH) {
    $cudaRoots = @(
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.4",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.3",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.2",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1",
        "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.0"
    )

    foreach ($cudaRoot in $cudaRoots) {
        if (Test-Path "$cudaRoot\bin\nvcc.exe") {
            $env:CUDA_PATH = $cudaRoot
            $env:Path = "$cudaRoot\bin;$env:Path"
            Write-Host "OK: CUDA_PATH set to $cudaRoot" -ForegroundColor Green
            break
        }
    }
}
elseif (Test-Path "$env:CUDA_PATH\bin") {
    $env:Path = "$env:CUDA_PATH\bin;$env:Path"
    Write-Host "OK: CUDA_PATH=$env:CUDA_PATH" -ForegroundColor Green
}

if (-not $env:CUDA_PATH) {
    Write-Host "WARNING: CUDA_PATH is not set. pnpm run tauri:dev:cuda will fail." -ForegroundColor Yellow
}

# cmake-rs may pick VS 2026; the CMake bundled with VS 2022 Build Tools only knows VS 2022.
# CUDA Visual Studio Integration is also missing, so Ninja + nvcc is used instead of the VS CUDA toolset.
$ninja = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($ninja) {
    $env:CMAKE_GENERATOR = "Ninja"
    $env:CMAKE_MAKE_PROGRAM = $ninja
    $env:Path = "$(Split-Path $ninja);$env:Path"
    Write-Host "OK: CMAKE_GENERATOR=Ninja" -ForegroundColor Green
}
else {
    # Do not inherit a machine-level generator such as "Visual Studio 18 2026".
    # The CMake toolchain used by this project currently exposes VS 2022 only.
    $env:CMAKE_GENERATOR = "Visual Studio 17 2022"
    Remove-Item Env:CMAKE_MAKE_PROGRAM -ErrorAction SilentlyContinue
    Write-Host "OK: CMAKE_GENERATOR=Visual Studio 17 2022" -ForegroundColor Green
}

if ($env:CUDA_PATH) {
    $env:CMAKE_CUDA_COMPILER = Join-Path $env:CUDA_PATH "bin\nvcc.exe"
    $env:NVCC_PREAPPEND_FLAGS = "-allow-unsupported-compiler -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -D_ALLOW_COMPILER_NOT_SUPPORTED"
}

# 8. Find CMake
$cmakePaths = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin",
    "C:\Program Files\CMake\bin"
)

foreach ($cp in $cmakePaths) {

    if (Test-Path "$cp\cmake.exe") {

        $env:Path += ";$cp"

        Write-Host "OK: CMake found at $cp" -ForegroundColor Green

        break
    }
}

# 9. Clean broken Rust target cache
$targetDir = Join-Path $PSScriptRoot "target"

if (Test-Path $targetDir) {

    $brokenWhisperBindings = Get-ChildItem `
        -Path $targetDir `
        -Filter "bindings.rs" `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -match "whisper-rs-sys" -and
            (Get-Content $_.FullName -Raw) -match 'pub struct whisper_full_params \{\s+pub _address: u8,'
        }

    if ($brokenWhisperBindings) {

        Write-Host "Broken whisper-rs-sys bindings detected." -ForegroundColor Yellow
        Write-Host "Cleaning whisper-rs-sys build cache..." -ForegroundColor Yellow

        Get-ChildItem `
            -Path $targetDir `
            -Directory `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "whisper-rs-sys-*" } |
            ForEach-Object {
                Remove-Item `
                    -Recurse `
                    -Force `
                    $_.FullName `
                    -ErrorAction SilentlyContinue
            }
    }
    else {

        try {
            Get-ChildItem `
                -Path $targetDir `
                -Recurse `
                -ErrorAction SilentlyContinue |
                Unblock-File `
                -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Warning: Could not unblock some target files." -ForegroundColor Yellow
        }
    }
}

# 10. Build llama-helper if missing
$llamaBinDir = Join-Path `
    $PSScriptRoot `
    "frontend\src-tauri\binaries"

if (-not (Test-Path $llamaBinDir)) {

    New-Item `
        -ItemType Directory `
        -Path $llamaBinDir `
        -Force |
        Out-Null
}

$llamaBin = Join-Path `
    $llamaBinDir `
    "llama-helper-x86_64-pc-windows-msvc.exe"

if (-not (Test-Path $llamaBin)) {

    Write-Host "Building llama-helper..." -ForegroundColor Cyan

    cargo build `
        --manifest-path "$PSScriptRoot\llama-helper\Cargo.toml"

    if ($LASTEXITCODE -ne 0) {

        Write-Host "ERROR: llama-helper build failed." -ForegroundColor Red
        exit 1
    }

    $compiledLlama = Join-Path `
        $PSScriptRoot `
        "target\debug\llama-helper.exe"

    if (Test-Path $compiledLlama) {

        Copy-Item `
            $compiledLlama `
            $llamaBin `
            -Force

        Write-Host "OK: llama-helper is ready." -ForegroundColor Green
    }
    else {

        Write-Host "WARNING: llama-helper.exe was not found after build." -ForegroundColor Yellow
    }
}
else {

    Write-Host "OK: llama-helper already exists." -ForegroundColor Green
}

# 11. Python virtual environment
$frontendDir = Join-Path $PSScriptRoot "frontend"

Set-Location $frontendDir

if (-not (Test-Path "venv")) {

    Write-Host "Creating Python virtual environment..." -ForegroundColor Cyan

    python -m venv venv

    if ($LASTEXITCODE -ne 0) {

        Write-Host "ERROR: Failed to create Python virtual environment." -ForegroundColor Red
        exit 1
    }
}

if (Test-Path "venv\Scripts\activate.ps1") {

    Write-Host "Activating Python virtual environment..." -ForegroundColor Green

    & ".\venv\Scripts\activate.ps1"
}

# 12. Install frontend dependencies
Write-Host "Installing frontend dependencies with pnpm..." -ForegroundColor Cyan

pnpm install

if ($LASTEXITCODE -ne 0) {

    Write-Host "ERROR: pnpm install failed." -ForegroundColor Red
    exit 1
}

# 13. Clean Next.js cache
if (Test-Path ".next") {

    Write-Host "Cleaning Next.js cache..." -ForegroundColor Cyan

    Remove-Item `
        -Recurse `
        -Force `
        ".next" `
        -ErrorAction SilentlyContinue
}

# 14. Start application
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Nabrh setup completed successfully!" -ForegroundColor Green
Write-Host "Starting Nabrh development mode..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

pnpm tauri dev
