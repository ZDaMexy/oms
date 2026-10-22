#!/usr/bin/env python3
"""Verify the complete patched Windows x64 FFmpeg set in a disposable process.

The ABI baseline comes from FFmpeg.AutoGen 4.3.0.1's actual managed layouts;
--abi-probe must be the executable compiled against this build's installed headers.
This does not replace decoding/seek/render regression tests through osu!framework.
"""

import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess


LIBRARIES = {
    "avutil-56.dll": ("avutil", "56.70.100"),
    "avcodec-58.dll": ("avcodec", "58.134.100"),
    "avformat-58.dll": ("avformat", "58.76.100"),
    "swscale-5.dll": ("swscale", "5.9.100"),
}
EXPORTS = {
    "avutil": "av_dict_set av_dict_free av_frame_alloc av_frame_free av_frame_unref av_frame_move_ref av_frame_get_buffer av_strdup av_strerror av_malloc av_freep av_hwdevice_ctx_create av_hwframe_transfer_data".split(),
    "avcodec": "av_packet_alloc av_packet_unref av_packet_free av_codec_iterate av_codec_is_decoder avcodec_get_hw_config avcodec_alloc_context3 avcodec_free_context avcodec_parameters_to_context avcodec_open2 avcodec_receive_frame avcodec_send_packet avcodec_flush_buffers".split(),
    "avformat": "av_read_frame av_seek_frame avformat_alloc_context avformat_close_input avformat_find_stream_info avformat_open_input av_find_best_stream avio_alloc_context avio_context_free".split(),
    "swscale": "sws_freeContext sws_getCachedContext sws_scale".split(),
}


def pe_imports(path):
    data = path.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0" or struct.unpack_from("<H", data, pe + 4)[0] != 0x8664:
        raise ValueError("The native set must contain Windows x64 PE files: " + path.name)
    section_count = struct.unpack_from("<H", data, pe + 6)[0]
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    if struct.unpack_from("<H", data, optional)[0] != 0x20B:
        raise ValueError("Expected a PE32+ optional header: " + path.name)
    import_rva = struct.unpack_from("<I", data, optional + 120)[0]
    sections = []
    for index in range(section_count):
        offset = optional + optional_size + index * 40
        virtual_size, rva, raw_size, raw_offset = struct.unpack_from("<IIII", data, offset + 8)
        sections.append((rva, max(virtual_size, raw_size), raw_offset))

    def file_offset(rva):
        for start, size, raw in sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError("Unmapped import address in " + path.name)

    imports = []
    if import_rva:
        offset = file_offset(import_rva)
        while any(data[offset:offset + 20]):
            name_rva = struct.unpack_from("<I", data, offset + 12)[0]
            name_offset = file_offset(name_rva)
            imports.append(data[name_offset:data.index(b"\0", name_offset)].decode("ascii"))
            offset += 20
    return imports


def names_from_iterator(function):
    function.argtypes = [ctypes.POINTER(ctypes.c_void_p)]
    function.restype = ctypes.c_void_p
    opaque = ctypes.c_void_p()
    names = []
    while pointer := function(ctypes.byref(opaque)):
        names.append(ctypes.cast(pointer, ctypes.POINTER(ctypes.c_char_p))[0].decode())
    return names


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("native_directory", type=Path)
parser.add_argument("--abi-probe", type=Path)
parser.add_argument("--check-manifest", type=Path)
parser.add_argument("--write-manifest", type=Path)
args = parser.parse_args()
if os.name != "nt" or ctypes.sizeof(ctypes.c_void_p) != 8:
    raise RuntimeError("Run the native identity check in a Windows x64 Python process")
recipe = Path(__file__).resolve().parent
lock_bytes = (recipe / "source.lock.json").read_bytes()
lock = json.loads(lock_bytes)
native = args.native_directory.resolve(strict=True)
libraries = {}
result = {"version": lock["version"], "upstreamCommit": lock["upstreamCommit"],
          "sourceLockSha256": hashlib.sha256(lock_bytes).hexdigest(), "libraries": {}}
with os.add_dll_directory(str(native)):
    for filename, (prefix, expected_version) in LIBRARIES.items():
        path = native / filename
        imports = pe_imports(path)
        # Preserve the original self-contained four-library package. Never quietly
        # introduce a host-installed libgcc/libwinpthread dependency.
        if any(name.lower().startswith(("libgcc", "libwinpthread", "libstdc++")) for name in imports):
            raise ValueError("Unbundled compiler runtime dependency in " + filename)
        library = ctypes.CDLL(str(path), winmode=0x100 | 0x1000)
        libraries[prefix] = library
        version = getattr(library, prefix + "_version")
        version.restype = ctypes.c_uint
        packed = version()
        text_version = f"{packed >> 16}.{(packed >> 8) & 255}.{packed & 255}"
        if text_version != expected_version:
            raise ValueError("Unexpected library version: " + filename + " " + text_version)
        configuration = getattr(library, prefix + "_configuration")
        configuration.restype = ctypes.c_char_p
        for name in EXPORTS[prefix]:
            getattr(library, name)
        result["libraries"][filename] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                         "version": text_version, "imports": imports,
                                         "configuration": configuration().decode()}
    version_info = libraries["avutil"].av_version_info
    version_info.restype = ctypes.c_char_p
    if version_info().decode() != lock["version"]:
        raise ValueError("The FFmpeg source identity does not match source.lock.json")
    protocols = libraries["avformat"].avio_enum_protocols
    protocols.argtypes = [ctypes.POINTER(ctypes.c_void_p), ctypes.c_int]
    protocols.restype = ctypes.c_char_p
    opaque = ctypes.c_void_p()
    input_protocols = []
    while name := protocols(ctypes.byref(opaque), 0):
        input_protocols.append(name.decode())
    if input_protocols != ["pipe"]:
        raise ValueError("The patched decoder unexpectedly enables additional input protocols: " + str(input_protocols))
    result["inputProtocols"] = input_protocols
    result["codecs"] = names_from_iterator(libraries["avcodec"].av_codec_iterate)
    result["demuxers"] = names_from_iterator(libraries["avformat"].av_demuxer_iterate)

if args.abi_probe:
    actual = json.loads(subprocess.check_output([str(args.abi_probe.resolve(strict=True))], text=True))
    expected = json.loads((recipe / "binding-abi.json").read_bytes())
    if actual != expected:
        differences = {key: {"managed": expected.get(key), "native": actual.get(key)}
                       for key in expected.keys() | actual.keys() if actual.get(key) != expected.get(key)}
        raise ValueError("Native and FFmpeg.AutoGen public layouts disagree: " + json.dumps(differences))
    result["managedAbiVerified"] = actual
if args.check_manifest:
    expected = json.loads(args.check_manifest.read_bytes())
    if result["sourceLockSha256"] != expected["sourceLockSha256"]:
        raise ValueError("The binary manifest belongs to another source lock")
    for filename, item in result["libraries"].items():
        if item["sha256"] != expected["libraries"][filename]["sha256"]:
            raise ValueError("Native binary checksum mismatch: " + filename)
if args.write_manifest:
    with args.write_manifest.open("x", encoding="utf-8", newline="\n") as output:
        json.dump(result, output, indent=2)
        output.write("\n")
print(json.dumps(result, indent=2))
