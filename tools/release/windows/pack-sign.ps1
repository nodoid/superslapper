<#
Packs and signs both MSIX layouts staged by tools/release/msix.py.

Run inside the Windows VM, from the Mac:

    prlctl exec "Windows 11" powershell -NoProfile -ExecutionPolicy Bypass `
        -File '\\Mac\Home\RiderProjects\superslap\tools\release\windows\pack-sign.ps1'

The self-signed certificate and its password come from make-cert.ps1, and the
makeappx/signtool binaries from the Microsoft.Windows.SDK.BuildTools package
(see tools/release/windows/README.md).
#>
param(
    [string]$Share = "\\Mac\Home\RiderProjects\superslap\out\msix",
    [string]$Work = "C:\msix"
)

$ErrorActionPreference = "Stop"

$tools = Join-Path $Work "buildtools\bin\10.0.28000.0\arm64"
$pw = Get-Content (Join-Path $Work "pfx-password.txt") -Raw

foreach ($arch in @("x64", "arm64")) {
    $layout = Join-Path $Share $arch
    $out = Join-Path $Share "SuperSlapper-$arch.msix"

    Write-Output "packing $arch"
    & (Join-Path $tools "makeappx.exe") pack /d $layout /p $out /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed for $arch" }

    Write-Output "signing $arch"
    & (Join-Path $tools "signtool.exe") sign /fd SHA256 /f (Join-Path $Work "superslapper.pfx") /p $pw $out
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for $arch" }
}
