#requires -Version 5.1
# Build do StorHUB para Windows (sem SDK .NET adicional).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Compilador C# do .NET Framework não encontrado: $csc" }
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$target = Join-Path $out 'StorHUB.exe'
& $csc /nologo /target:winexe /optimize+ /out:$target /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:Microsoft.VisualBasic.dll (Join-Path $root 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação do StorHUB." }
Write-Host "Compilado: $target"
Write-Host ("SHA256: " + (Get-FileHash $target -Algorithm SHA256).Hash)
