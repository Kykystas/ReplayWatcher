param([switch]$Test, [switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
& (Join-Path $projectRoot 'build-icon.ps1')
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkDir 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Windows with .NET Framework 4.8 is required.' }
$libraryRoot = Join-Path $projectRoot 'lib'
$outputDir = Join-Path $projectRoot 'dist'
New-Item -Path $libraryRoot, $outputDir -ItemType Directory -Force | Out-Null
$packages = @(
    @{ Id='naudio.core'; Hash='794645DBFD30E4880663D52D9D9224D55C301FA54228FF623C95D572C7887347'; Dll='NAudio.Core.dll' },
    @{ Id='naudio.wasapi'; Hash='C8396B8DBAB86A2619EDFD9158BEAF18C21CB86677099B5594950FD8651B9D1A'; Dll='NAudio.Wasapi.dll' }
)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$references = @()
foreach ($package in $packages) {
    $archive = Join-Path $libraryRoot ($package.Id + '.2.2.1.zip')
    if (-not (Test-Path -LiteralPath $archive)) {
        $uri = 'https://api.nuget.org/v3-flatcontainer/{0}/2.2.1/{0}.2.2.1.nupkg' -f $package.Id
        Invoke-WebRequest -Uri $uri -OutFile $archive -UseBasicParsing
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.Hash) { throw "Package hash mismatch: $($package.Id)" }
    $expanded = Join-Path $libraryRoot $package.Id
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $dll = Join-Path $expanded ('lib\netstandard2.0\' + $package.Dll)
    $references += '/reference:' + $dll
    Copy-Item -LiteralPath $dll -Destination $outputDir -Force
}
$exe = Join-Path $outputDir 'ReplayWatcher.exe'
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5',
    '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:Microsoft.CSharp.dll',
    ('/reference:' + (Join-Path $frameworkDir 'netstandard.dll')), ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $projectRoot 'assets\ReplayWatcher.ico')), ('/resource:' + (Join-Path $projectRoot 'assets\ReplayWatcher.ico') + ',ReplayWatcher.AppIcon'), ('/out:' + $exe)) + $references + $sources
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'LICENSE'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination $outputDir -Force
if ($Test) {
    $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath (Join-Path $outputDir 'data\self-test.txt')
    if ($process.ExitCode -ne 0) { throw 'Self-tests failed.' }
}
if ($Preview) {
    Start-Process -FilePath $exe -ArgumentList '--preview' -WindowStyle Hidden -Wait
    Start-Process -FilePath $exe -ArgumentList '--preview','--microphone' -WindowStyle Hidden -Wait
    Start-Process -FilePath $exe -ArgumentList '--preview','--microphone','--english' -WindowStyle Hidden -Wait
}
Write-Output "Built: $exe"
