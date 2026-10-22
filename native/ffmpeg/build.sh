#!/usr/bin/env bash
# Build only the existing Windows video decoder surface. Run in an isolated Linux
# toolchain with Python >=3.12, gpg, GNU patch, bash, make, nasm, and MinGW-w64 GCC.
# The caller supplies fresh build/output directories and owns toolchain setup.
set -euo pipefail

if [[ $# -ne 3 ]]; then
    printf 'Usage: %s SOURCE_CACHE FRESH_WORK_DIRECTORY OUTPUT_DIRECTORY\n' "$0" >&2
    exit 2
fi

task_recipe_dir=$(cd -- "$(dirname -- "$0")" && pwd)
task_cache=$(realpath -m -- "$1")
task_work=$(realpath -m -- "$2")
task_output=$(realpath -m -- "$3")
if [[ -e "$task_work" || -e "$task_output" ]]; then
    printf 'Build and output directories must be new; no existing files are removed.\n' >&2
    exit 2
fi
mkdir -p -- "$task_cache" "$task_work" "$task_output"
task_source=$(python3 "$task_recipe_dir/prepare.py" "$task_cache" "$task_work/source")
mkdir -- "$task_work/build"
cd -- "$task_work/build"

"$task_source/configure" \
    --disable-static --enable-shared --disable-all --disable-autodetect \
    --disable-doc --disable-programs --disable-debug --enable-lto \
    --enable-avcodec --enable-avformat --enable-swscale \
    --enable-demuxer=avi,flv,asf --enable-parser=mpeg4video \
    --enable-decoder=flv,msmpeg4v1,msmpeg4v2,msmpeg4v3,mpeg4,vp6,vp6f,wmv2 \
    --enable-demuxer=mov,matroska --enable-parser=h264,hevc,vp8,vp9 \
    --enable-decoder=h264,hevc,vp8,vp9 --enable-protocol=pipe \
    --enable-w32threads --enable-dxva2 --enable-d3d11va \
    --enable-hwaccel=h264_dxva2,h264_d3d11va,h264_d3d11va2 \
    --enable-hwaccel=hevc_dxva2,hevc_d3d11va,hevc_d3d11va2 \
    --enable-hwaccel=vp9_dxva2,vp9_d3d11va,vp9_d3d11va2 \
    --enable-cross-compile --target-os=mingw32 --arch=x86_64 \
    --cross-prefix=x86_64-w64-mingw32- --extra-ldflags='-static -static-libgcc' \
    --extra-version=oms.20261022 --prefix="$task_work/install" \
    --shlibdir="$task_work/install/bin"

# The POSIX-thread MinGW GCC driver adds -lpthread even with FFmpeg's w32threads.
# Keep compiler support libraries static. FFmpeg's own four libraries stay shared:
# request their exact import archives so -static never searches a .dll or an
# empty .lib generated from LTO objects instead of the linker-created .dll.a.
make -j2 LD_LIB='-l:lib%.dll.a'
make install-libs install-headers LD_LIB='-l:lib%.dll.a'
for task_library in avutil-56 avcodec-58 avformat-58 swscale-5; do
    cp -- "$task_work/install/bin/$task_library.dll" "$task_output/$task_library.dll"
done
x86_64-w64-mingw32-gcc -static -static-libgcc "$task_recipe_dir/abi-probe.c" \
    -I"$task_work/install/include" -o "$task_work/abi-probe.exe"
cp -- "$task_work/build/config.h" "$task_work/config.h"
cp -- "$task_work/build/ffbuild/config.log" "$task_work/config.log"
cp -- "$task_source/COPYING.LGPLv2.1" "$task_output/COPYING.LGPLv2.1"
cp -- "$task_source/LICENSE.md" "$task_output/FFmpeg-LICENSE.md"
cp -- "$task_recipe_dir/source.lock.json" "$task_output/source.lock.json"
x86_64-w64-mingw32-gcc --version > "$task_work/compiler-version.txt"
sha256sum "$task_output"/*.dll > "$task_work/dll-sha256.txt"
printf 'FFmpeg native libraries: %s\nABI probe: %s\n' "$task_output" "$task_work/abi-probe.exe"
