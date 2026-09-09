param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$FfmpegPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw '视频制作目录已存在；请指定新的目录。' }
$ancestor = [IO.Path]::GetDirectoryName($outputRoot)
while ($ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw '视频制作目录不能经过链接。' }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public static class C7ViewportVideo
{
    static byte[] Chunk(string name, byte[] payload)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes(name)); writer.Write(payload.Length); writer.Write(payload);
            if ((payload.Length & 1) != 0) writer.Write((byte)0);
            return stream.ToArray();
        }
    }
    static byte[] Data(Action<BinaryWriter> write)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream)) { write(writer); return stream.ToArray(); }
    }
    public static void Create(string path)
    {
        const int width = 96, height = 54, frames = 60, frameBytes = width * height * 3;
        byte[] header = Data(w => {
            w.Write(100000); w.Write(frameBytes * 10); w.Write(0); w.Write(0); w.Write(frames);
            w.Write(0); w.Write(1); w.Write(frameBytes); w.Write(width); w.Write(height);
            for (int i = 0; i < 4; i++) w.Write(0);
        });
        byte[] streamHeader = Data(w => {
            w.Write(Encoding.ASCII.GetBytes("vidsDIB ")); w.Write(0); w.Write((short)0); w.Write((short)0);
            w.Write(0); w.Write(1); w.Write(10); w.Write(0); w.Write(frames); w.Write(frameBytes);
            w.Write(-1); w.Write(0); w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height);
        });
        byte[] format = Data(w => {
            w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(frameBytes); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        });
        byte[] headers = Data(w => {
            w.Write(Encoding.ASCII.GetBytes("hdrl")); w.Write(Chunk("avih", header));
            w.Write(Chunk("LIST", Data(s => {
                s.Write(Encoding.ASCII.GetBytes("strl")); s.Write(Chunk("strh", streamHeader)); s.Write(Chunk("strf", format));
            })));
        });
        byte[] movie = Data(w => {
            w.Write(Encoding.ASCII.GetBytes("movi"));
            for (int frame = 0; frame < frames; frame++)
            {
                byte[] pixels = new byte[frameBytes];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    bool border = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                    bool moving = Math.Abs(x - frame * width / frames) < 5;
                    int offset = (y * width + x) * 3;
                    pixels[offset] = border ? (byte)255 : moving ? (byte)240 : (byte)64;
                    pixels[offset + 1] = border ? (byte)255 : moving ? (byte)180 : (byte)32;
                    pixels[offset + 2] = border ? (byte)255 : moving ? (byte)40 : (byte)16;
                }
                w.Write(Chunk("00db", pixels));
            }
        });
        File.WriteAllBytes(path, Chunk("RIFF", Data(w => {
            w.Write(Encoding.ASCII.GetBytes("AVI ")); w.Write(Chunk("LIST", headers)); w.Write(Chunk("LIST", movie));
        })));
    }
}
'@
$avi = Join-Path $outputRoot 'viewport.avi'
[C7ViewportVideo]::Create($avi)
Write-Host "原创 AVI 源视频已生成：$avi"
if ($FfmpegPath) {
    $ffmpeg = [IO.Path]::GetFullPath($FfmpegPath)
    if (-not (Test-Path -LiteralPath $ffmpeg -PathType Leaf)) { throw '指定的 ffmpeg 程序不存在；原创 AVI 已保留。' }
    $mp4 = Join-Path $outputRoot 'viewport.mp4'
    & $ffmpeg -nostdin -hide_banner -loglevel error -i $avi -an -c:v libx264 -preset veryslow -crf 18 -pix_fmt yuv420p -profile:v baseline -level 3.0 -threads 1 -fflags +bitexact -flags:v +bitexact -map_metadata -1 -movflags +faststart -f mp4 $mp4
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $mp4 -PathType Leaf)) { throw '视频转码未完成；请保全制作目录并检查工具输出。' }
    Write-Host "供现有播放器直接读取的 MP4 已生成：$mp4"
}
