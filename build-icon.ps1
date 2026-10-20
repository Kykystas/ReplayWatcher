$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path $PSScriptRoot 'assets'
$source = [Drawing.Bitmap]::FromFile((Join-Path $assetDir 'chameleon.png'))
try {
    $frames = @()
    foreach ($size in @(16,24,32,48,64,128,256)) {
        $bitmap = New-Object Drawing.Bitmap($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = New-Object IO.MemoryStream
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.DrawImage($source, (New-Object Drawing.Rectangle(0,0,$size,$size)))
            # .NET Framework Icon misreads small PNG-compressed ICO frames. Use classic 32-bit DIBs.
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Bmp)
            $bmp = $stream.ToArray()
            $dib = New-Object byte[] ($bmp.Length - 14)
            [Array]::Copy($bmp, 14, $dib, 0, $dib.Length)
            if ([BitConverter]::ToInt32($dib,0) -ne 40 -or [BitConverter]::ToInt16($dib,14) -ne 32) { throw 'Unexpected bitmap format.' }
            [BitConverter]::GetBytes([int]($size*2)).CopyTo($dib,8)
            $maskStride = [int]([Math]::Ceiling($size/32.0)*4)
            $mask = New-Object byte[] ($maskStride*$size)
            for ($y=0; $y -lt $size; $y++) {
                for ($x=0; $x -lt $size; $x++) {
                    if ($bitmap.GetPixel($x,$y).A -eq 0) {
                        $maskIndex = ($size-1-$y)*$maskStride + [int][Math]::Floor($x/8.0)
                        $mask[$maskIndex] = $mask[$maskIndex] -bor (128 -shr ($x%8))
                    }
                }
            }
            [BitConverter]::GetBytes([int]($size*$size*4+$mask.Length)).CopyTo($dib,20)
            $frames += @{ Size=$size; Bytes=([byte[]]($dib+$mask)) }
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [IO.File]::Create((Join-Path $assetDir 'ReplayWatcher.ico'))
    $writer = New-Object IO.BinaryWriter($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose(); $file.Dispose() }
} finally { $source.Dispose() }
