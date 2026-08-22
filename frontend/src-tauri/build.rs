#[path = "build/ffmpeg.rs"]
mod ffmpeg;

fn main() {
    // Bindgen needs an MSVC target, its C headers and libclang on Windows.
    #[cfg(target_os = "windows")]
    {
        if std::env::var("BINDGEN_EXTRA_CLANG_ARGS").is_err() {
            let mut args = String::from("--target=x86_64-pc-windows-msvc");
            let vs_dirs = [
                r"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC",
                r"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC",
                r"C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\MSVC",
                r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\MSVC",
                r"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC",
                r"C:\Program Files\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC",
            ];
            'outer: for vs in &vs_dirs {
                if let Ok(entries) = std::fs::read_dir(vs) {
                    for entry in entries.flatten() {
                        let include = entry.path().join("include");
                        if include.join("vcruntime.h").exists() {
                            args.push_str(&format!(" -I\"{}\"", include.display()));
                            break 'outer;
                        }
                    }
                }
            }
            std::env::set_var("BINDGEN_EXTRA_CLANG_ARGS", args);
        }

        if std::env::var("LIBCLANG_PATH").is_err() {
            for path in [
                r"C:\Program Files\LLVM\bin",
                r"C:\LLVM\bin",
                r"C:\Program Files (x86)\LLVM\bin",
            ] {
                if std::path::Path::new(path).join("libclang.dll").exists() {
                    std::env::set_var("LIBCLANG_PATH", path);
                    break;
                }
            }
        }
    }

    // GPU Acceleration Detection and Build Guidance
    detect_and_report_gpu_capabilities();

    #[cfg(target_os = "macos")]
    {
        println!("cargo:rustc-link-lib=framework=AVFoundation");
        println!("cargo:rustc-link-lib=framework=Cocoa");
        println!("cargo:rustc-link-lib=framework=Foundation");

        // Let the enhanced_macos crate handle its own Swift compilation
        // The swift-rs crate build will be handled in the enhanced_macos crate's build.rs
    }

    // Download and bundle FFmpeg binary at build-time
    ffmpeg::ensure_ffmpeg_binary();

    tauri_build::build()
}

/// Detects GPU acceleration capabilities and provides build guidance
fn detect_and_report_gpu_capabilities() {
    let target_os = std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();

    println!("cargo:warning=🚀 Building Nabra for: {}", target_os);

    match target_os.as_str() {
        "macos" => {
            println!("cargo:warning=✅ macOS: Metal GPU acceleration ENABLED by default");
            #[cfg(feature = "coreml")]
            println!("cargo:warning=✅ CoreML acceleration ENABLED");
        }
        "windows" => {
            if cfg!(feature = "cuda") {
                println!("cargo:warning=✅ Windows: CUDA GPU acceleration ENABLED");
            } else if cfg!(feature = "vulkan") {
                println!("cargo:warning=✅ Windows: Vulkan GPU acceleration ENABLED");
            } else if cfg!(feature = "openblas") {
                println!("cargo:warning=✅ Windows: OpenBLAS CPU optimization ENABLED");
            } else {
                println!("cargo:warning=⚠️  Windows: Using CPU-only mode (no GPU or BLAS acceleration)");
                println!("cargo:warning=💡 For NVIDIA GPU: cargo build --release --features cuda");
                println!("cargo:warning=💡 For AMD/Intel GPU: cargo build --release --features vulkan");
                println!("cargo:warning=💡 For CPU optimization: cargo build --release --features openblas");

                // Try to detect NVIDIA GPU
                if which::which("nvidia-smi").is_ok() {
                    println!("cargo:warning=🎯 NVIDIA GPU detected! Consider rebuilding with --features cuda");
                }
            }
        }
        "linux" => {
            if cfg!(feature = "cuda") {
                println!("cargo:warning=✅ Linux: CUDA GPU acceleration ENABLED");
            } else if cfg!(feature = "vulkan") {
                println!("cargo:warning=✅ Linux: Vulkan GPU acceleration ENABLED");
            } else if cfg!(feature = "hipblas") {
                println!("cargo:warning=✅ Linux: AMD ROCm (HIP) acceleration ENABLED");
            } else if cfg!(feature = "openblas") {
                println!("cargo:warning=✅ Linux: OpenBLAS CPU optimization ENABLED");
            } else {
                println!("cargo:warning=⚠️  Linux: Using CPU-only mode (no GPU or BLAS acceleration)");
                println!("cargo:warning=💡 For NVIDIA GPU: cargo build --release --features cuda");
                println!("cargo:warning=💡 For AMD GPU: cargo build --release --features hipblas");
                println!("cargo:warning=💡 For other GPUs: cargo build --release --features vulkan");
                println!("cargo:warning=💡 For CPU optimization: cargo build --release --features openblas");

                // Try to detect NVIDIA GPU
                if which::which("nvidia-smi").is_ok() {
                    println!("cargo:warning=🎯 NVIDIA GPU detected! Consider rebuilding with --features cuda");
                }

                // Try to detect AMD GPU
                if which::which("rocm-smi").is_ok() {
                    println!("cargo:warning=🎯 AMD GPU detected! Consider rebuilding with --features hipblas");
                }
            }
        }
        _ => {
            println!("cargo:warning=ℹ️  Unknown platform: {}", target_os);
        }
    }

    // Performance guidance
    if !cfg!(feature = "cuda") && !cfg!(feature = "vulkan") && !cfg!(feature = "hipblas") && !cfg!(feature = "openblas") && target_os != "macos" {
        println!("cargo:warning=📊 Performance: CPU-only builds are significantly slower than GPU/BLAS builds");
        println!("cargo:warning=📚 See README.md for GPU/BLAS setup instructions");
    }
}
