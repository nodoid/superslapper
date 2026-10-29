<#
Checks that Windows actually accepts the packages: verifies the signatures and
provisions the arm64 package for all users, which validates the manifest and the
payload the same way an install does. It is removed again afterwards.

    prlctl exec "Windows 11" powershell -NoProfile -ExecutionPolicy Bypass `
        -File '\\Mac\Home\RiderProjects\superslap\tools\release\windows\provision-check.ps1'

Provisioning is used rather than Add-AppxPackage because prlctl exec runs as
Local System, which the deployment stack refuses to install for.
#>
param(
    [string]$Share = "\\Mac\Home\RiderProjects\superslap\out\msix",
    [string]$Work = "C:\msix"
)

$ErrorActionPreference = "Stop"

$tools = Join-Path $Work "buildtools\bin\10.0.28000.0\arm64"
$name = "PaulFJohnson.SuperSlapper"

foreach ($arch in @("x64", "arm64")) {
    Write-Output "verifying $arch"
    & (Join-Path $tools "signtool.exe") verify /pa /v (Join-Path $Share "SuperSlapper-$arch.msix") |
        Select-String "Successfully verified"
    if ($LASTEXITCODE -ne 0) { throw "verify failed for $arch" }
}

Write-Output "provisioning arm64"
Add-AppxProvisionedPackage -Online -PackagePath (Join-Path $Share "SuperSlapper-arm64.msix") -SkipLicense |
    Select-Object DisplayName, Version, Architecture

Get-AppxProvisionedPackage -Online |
    Where-Object { $_.DisplayName -eq $name } |
    Select-Object DisplayName, Version, Architecture

Write-Output "removing provisioned package"
Get-AppxProvisionedPackage -Online |
    Where-Object { $_.DisplayName -eq $name } |
    Remove-AppxProvisionedPackage -Online | Out-Null
