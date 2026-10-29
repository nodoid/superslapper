<#
Creates the self-signed code-signing certificate the MSIX packages are signed
with, and exports it beside them so it can be trusted for sideload testing.

    prlctl exec "Windows 11" powershell -NoProfile -ExecutionPolicy Bypass `
        -File '\\Mac\Home\RiderProjects\superslap\tools\release\windows\make-cert.ps1'

A self-signed certificate installs on a machine that trusts it and is not what
the Microsoft Store accepts for a trusted install: the subject here is the
publisher identity Partner Center assigned to the app, which is also the
Publisher in AppxManifest.xml, so a sideloaded package is accepted. Change both
if the account's publisher identity ever changes.
#>
param(
    [string]$Share = "\\Mac\Home\RiderProjects\superslap\out\msix",
    [string]$Work = "C:\msix",
    [string]$Subject = "CN=A6EAEB04-6634-41C1-BDFE-695819ECE444"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Work | Out-Null

# A password the signing step reads back; never printed.
$alphabet = (48..57) + (65..90) + (97..122) | ForEach-Object { [char]$_ }
$pw = -join (1..24 | ForEach-Object { $alphabet | Get-Random })
Set-Content -Path (Join-Path $Work "pfx-password.txt") -Value $pw -NoNewline

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(5) `
    -FriendlyName "SuperSlapper self-signed"

$secure = ConvertTo-SecureString $pw -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $Work "superslapper.pfx") -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath (Join-Path $Share "SuperSlapper-selfsigned.cer") | Out-Null

Write-Output ("certificate " + $cert.Thumbprint + " exported")
