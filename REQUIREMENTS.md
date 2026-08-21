# متطلبات التطوير — نبرة (Nabrh)

دليل مرتّب للمطورين على **Windows**. نفّذ الخطوات بالترتيب، وتحقق من كل خطوة قبل الانتقال للتالية.

التشغيل التلقائي بعد اكتمال المتطلبات:

```powershell
.\setup_env.ps1
```

هذا الأمر يبني التطبيق في وضع **CPU**. لتفعيل GPU انظر القسم 8.

---

## الترتيب السريع

| الترتيب | الأداة | إلزامي؟ |
|--------|--------|---------|
| 1 | Node.js | نعم |
| 2 | pnpm | نعم |
| 3 | Rust / Cargo | نعم |
| 4 | Python | نعم |
| 5 | Visual Studio Build Tools 2022 (C++) | نعم |
| 6 | Windows SDK + CMake + Ninja | نعم (عادة مع Build Tools) |
| 7 | LLVM / libclang | نعم (لبناء Whisper) |
| 8 | CUDA Toolkit + Visual Studio Integration | اختياري (GPU من NVIDIA) |
| 9 | Vulkan SDK | اختياري (بديل GPU) |
| 10 | إعداد Windows Security | موصى به على Windows 11 |

---

## 1. Node.js

**الغرض:** تشغيل واجهة Next.js.

**التحميل:** [https://nodejs.org](https://nodejs.org) — الإصدار LTS (`18` أو أحدث).

**التحقق:**

```powershell
node -v
```

المتوقع: `v18.x` أو أحدث.

---

## 2. pnpm

**الغرض:** تثبيت حزم الواجهة (`frontend/`).

**التثبيت:**

```powershell
npm install -g pnpm
```

**التحقق:**

```powershell
pnpm -v
```

المتوقع: `9.x` أو أحدث.

---

## 3. Rust و Cargo

**الغرض:** بناء نواة Tauri و Whisper.

**التحميل:** [https://rustup.rs](https://rustup.rs)

بعد التثبيت أغلق الطرفية وافتح واحدة جديدة.

**التحقق:**

```powershell
rustc --version
cargo --version
```

المتوقع: Rust `1.77` أو أحدث.

---

## 4. Python

**الغرض:** البيئة الافتراضية `venv` وبعض أدوات المشروع.

**التحميل:** [https://www.python.org/downloads](https://www.python.org/downloads) — `3.10` أو أحدث.  
أثناء التثبيت فعّل **Add Python to PATH**.

**التحقق:**

```powershell
python --version
```

---

## 5. Visual Studio Build Tools 2022

**الغرض:** مترجم MSVC لبناء Whisper و FFmpeg والربط مع Windows.

**التحميل:** [Visual Studio Build Tools](https://visualstudio.microsoft.com/visual-cpp-build-tools/)

أو Visual Studio 2022 Community / Professional / Enterprise.

**أثناء التثبيت اختر:**

- **Desktop development with C++**
- MSVC v143
- Windows 10/11 SDK
- CMake tools for Windows
- C++ CMake tools / Ninja (إن ظهر الخيار)

**التحقق:**

```powershell
Test-Path "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
# أو Enterprise:
Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
```

المتوقع: `True`.

تحميل بيئة المترجم في الطرفية:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\Launch-VsDevShell.ps1" -Arch amd64 -HostArch amd64
cl
```

المتوقع: رسالة استخدام `cl.exe` وليس «command not found».

---

## 6. CMake و Ninja

غالباً يُثبتان مع Visual Studio. إن لم يكونا موجودين:

- CMake: [https://cmake.org/download](https://cmake.org/download)
- Ninja يأتي عادة من مسار Build Tools

**التحقق:**

```powershell
cmake --version
```

إن لم يُعثر على الأمر، أضف إلى PATH مثلاً:

```
C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin
```

---

## 7. LLVM / Clang (`libclang`)

**الغرض:** `bindgen` عند بناء `whisper-rs-sys`.

**التثبيت (موصى به):**

```powershell
winget install LLVM.LLVM
```

أو من [LLVM Releases](https://github.com/llvm/llvm-project/releases).

**متغير البيئة:**

| الاسم | القيمة |
|--------|--------|
| `LIBCLANG_PATH` | `C:\Program Files\LLVM\bin` أو `C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\bin` |

`setup_env.ps1` يضبطه تلقائياً إن وُجد الملف في LLVM أو Visual Studio.

المشروع يستخدم `whisper-rs` 0.16 وهو متوافق مع LLVM 22.

**التحقق:**

```powershell
Test-Path "C:\Program Files\LLVM\bin\libclang.dll"
& "C:\Program Files\LLVM\bin\clang.exe" --version
```

المتوقع: `True`، ثم رقم إصدار clang.

---

## 8. CUDA Toolkit (اختياري — GPU من NVIDIA)

بدون هذه الخطوة يعمل التطبيق على **CPU**.

**التحميل:** CUDA Toolkit 12.1  
[https://developer.nvidia.com/cuda-12-1-0-download-archive](https://developer.nvidia.com/cuda-12-1-0-download-archive)

**مهم جداً:** تثبيت مخصص (Custom) وتفعيل:

- CUDA (النواة و`nvcc`)
- **Visual Studio Integration** ← بدون هذا الخيار يفشل البناء بـ `No CUDA toolset found`

**متغير البيئة:**

| الاسم | القيمة |
|--------|--------|
| `CUDA_PATH` | `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1` |

أضف أيضاً إلى PATH: `%CUDA_PATH%\bin`

**التحقق — الأدوات:**

```powershell
nvidia-smi
nvcc --version
echo $env:CUDA_PATH
```

**التحقق — تكامل Visual Studio (هذا ما كان ناقصاً):**

```powershell
Test-Path "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Microsoft\VC\v170\BuildCustomizations\CUDA 12.1.props"
Test-Path "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1\extras\visual_studio_integration"
```

المتوقع: `True` لكليهما. إذا كانت `False`، أعد تثبيت CUDA مع Visual Studio Integration.

**تشغيل التطبيق على GPU:**

```powershell
cd frontend
pnpm run tauri:dev:cuda
```

**التحقق من أن البناء استخدم CUDA:** ابحث في الطرفية عن:

```
Windows: CUDA GPU acceleration ENABLED
```

وعند تحميل نموذج Whisper:

```
compiled_backend=Cuda ... use_gpu=true
```

أثناء التسجيل راقب:

```powershell
nvidia-smi
```

يجب أن يرتفع `GPU-Util`.

---

## 9. Vulkan SDK (اختياري — بديل GPU)

إن فشل CUDA، يمكن البناء بـ Vulkan (يعمل مع NVIDIA أيضاً).

**التحميل:** [https://vulkan.lunarg.com](https://vulkan.lunarg.com)

المثبّت يضبط عادة `VULKAN_SDK`.

**التحقق:**

```powershell
echo $env:VULKAN_SDK
vulkaninfo
```

**التشغيل:**

```powershell
cd frontend
pnpm run tauri:dev:vulkan
```

---

## 10. Windows 11 — Smart App Control

إن ظهر حظر لملفات مثل `build-script-build.exe`:

1. Windows Security → App & browser control → Smart App Control → **Off**
2. أو أضف استثناءات:

```powershell
Add-MpPreference -ExclusionPath "$env:USERPROFILE\.rustup", "$env:USERPROFILE\.cargo", "C:\Users\admin\Nabrh"
```

(شغّل PowerShell كمسؤول، وعدّل المسار إن لزم.)

---

## 11. تثبيت المشروع وتشغيله

من جذر المستودع:

```powershell
.\setup_env.ps1
```

يقوم بـ:

1. التحقق من Node / pnpm / Rust / LLVM / CMake
2. تحميل بيئة Visual Studio
3. بناء `llama-helper` إن لزم
4. `pnpm install` داخل `frontend`
5. تشغيل `pnpm tauri dev` (**CPU**)

تشغيل يدوي بعد `pnpm install`:

| الوضع | الأمر (من مجلد `frontend`) |
|--------|----------------------------|
| CPU | `pnpm tauri:dev` |
| NVIDIA CUDA | `pnpm run tauri:dev:cuda` |
| Vulkan | `pnpm run tauri:dev:vulkan` |

واجهة التطوير: [http://localhost:3118](http://localhost:3118)

إن ظهرت `ChunkLoadError` عند أول تشغيل، انتظر انتهاء `Compiled /` ثم اضغط **Ctrl+R**.

---

## 12. قائمة تحقق نهائية

```powershell
node -v
pnpm -v
rustc --version
python --version
cmake --version
Test-Path "$env:LIBCLANG_PATH\libclang.dll"
nvidia-smi          # إن كان لديك NVIDIA
nvcc --version      # إن أردت CUDA
echo $env:CUDA_PATH
```

بناء CPU ناجح يظهر:

```
Windows: Using CPU-only mode
Finished `dev` profile
```

بناء CUDA ناجح يظهر:

```
Windows: CUDA GPU acceleration ENABLED
Finished `dev` profile
```

هذه الرسالة تُطبع من `frontend/src-tauri/build.rs` حسب `--features` وقت البناء، وليست فحصاً وقت التشغيل.
