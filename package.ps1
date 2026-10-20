param()
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputDir = Join-Path $projectRoot 'dist'
$exe = Join-Path $outputDir 'ReplayWatcher.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 first.' }
$version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion -replace '\.0$', ''
$packageDir = Join-Path $projectRoot 'packages'
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
# Explicit lists deliberately exclude local data, reports, backups and build dependencies.
$rootNames = @('.gitignore','app.manifest','build.ps1','build-icon.ps1','package.ps1','README.md','LICENSE','ReplayWatcher.csproj','THIRD-PARTY-NOTICES.txt')
$sourceFiles = @($rootNames | ForEach-Object { Join-Path $projectRoot $_ }) + @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
$assetNames = @('chameleon.png','ReplayWatcher.ico')
$assetFiles = @($assetNames | ForEach-Object { Join-Path (Join-Path $projectRoot 'assets') $_ })
foreach ($file in $sourceFiles) {
    $contents = [IO.File]::ReadAllText($file)
    if ($contents -match '(?i)[A-Z]:[\\/]+Users[\\/]' -or
        $contents -match '(?i)-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----' -or
        $contents -match '\bgh[pousr]_[A-Za-z0-9]{30,}\b') {
        throw ('Potential private data in source file: ' + [IO.Path]::GetFileName($file))
    }
    if ($env:USERNAME -and $env:USERNAME.Length -ge 4 -and $contents -match ('(?i)\b' + [regex]::Escape($env:USERNAME) + '\b')) {
        throw ('Local username found in source file: ' + [IO.Path]::GetFileName($file))
    }
}
$portableNames = @('ReplayWatcher.exe','NAudio.Core.dll','NAudio.Wasapi.dll','README.md','LICENSE','THIRD-PARTY-NOTICES.txt')
$checksumLines = @($portableNames | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath (Join-Path $outputDir $_) -Algorithm SHA256).Hash
    $hash + '  ' + $_
})
[IO.File]::WriteAllLines((Join-Path $outputDir 'SHA256SUMS.txt'), $checksumLines, (New-Object Text.UTF8Encoding($false)))
$portableNames += 'SHA256SUMS.txt'
$portableFiles = @($portableNames | ForEach-Object { Join-Path $outputDir $_ })
$sourceItems = @($rootNames | ForEach-Object { Join-Path $projectRoot $_ }) + @(Join-Path $projectRoot 'src') + @(Join-Path $projectRoot 'assets')
# Reject unexpected files inside src rather than silently shipping them with the folder.
$unexpected = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Recurse -File | Where-Object { $_.DirectoryName -ne (Join-Path $projectRoot 'src') -or $_.Extension -ne '.cs' })
if ($unexpected.Count) { throw 'Unexpected files in src. Review before packaging.' }
$unexpectedAssets = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'assets') -Recurse -File | Where-Object { $_.FullName -notin $assetFiles })
if ($unexpectedAssets.Count) { throw 'Unexpected files in assets. Review before packaging.' }
$portableZip = Join-Path $packageDir ('ReplayWatcher-' + $version + '-win-x64.zip')
$sourceZip = Join-Path $packageDir ('ReplayWatcher-' + $version + '-source.zip')
Compress-Archive -LiteralPath $portableFiles -DestinationPath $portableZip -Force
Compress-Archive -LiteralPath $sourceItems -DestinationPath $sourceZip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($zipPath in @($portableZip, $sourceZip)) {
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $expected = if ($zipPath -eq $portableZip) { $portableNames } else { @(($sourceFiles + $assetFiles) | ForEach-Object { $_.Substring($projectRoot.Length + 1).Replace('\','/') }) }
        $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\','/') })
        if (@(Compare-Object $expected $actual).Count) { throw 'Archive contents differ from the publication allowlist.' }
        Write-Output ('Verified ' + [IO.Path]::GetFileName($zipPath) + ': ' + $actual.Count + ' files; no local data.')
    }
    finally { $archive.Dispose() }
}
