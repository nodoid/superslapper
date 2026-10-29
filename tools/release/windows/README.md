# MSIX packaging, in the Windows VM

`tools/release/windows.sh` publishes the two Windows builds on the Mac and hands
the packaging to a Parallels VM, because `makeappx` and `signtool` only run on
Windows. This is the one-off setup that VM needs.

## 1. The tools

The Windows SDK installs them, but the whole SDK is not needed: Microsoft ships
the same binaries in the `Microsoft.Windows.SDK.BuildTools` NuGet package, about
22 MB. In the guest:

```powershell
New-Item -ItemType Directory -Force -Path C:\msix | Out-Null
Invoke-WebRequest -UseBasicParsing `
    -Uri 'https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools' `
    -OutFile 'C:\msix\buildtools.zip'
Expand-Archive -Path C:\msix\buildtools.zip -DestinationPath C:\msix\buildtools -Force
```

That leaves `C:\msix\buildtools\bin\<version>\<arch>\makeappx.exe` and
`signtool.exe`. The scripts use the `arm64` folder because the VM runs Windows on
ARM; on an x64 guest point `$tools` at the `x64` folder. Bump the version in the
scripts if the package ships a newer one.

## 2. The shared folder

The guest reads the staged layout from the Mac through the Parallels share of the
home directory, which appears inside Windows as `\\Mac\Home`. The scripts default
to `\\Mac\Home\RiderProjects\superslap\out\msix`; pass `-Share <path>` to point
them somewhere else.

## 3. The certificate

Run once, from the Mac:

```bash
prlctl exec "Windows 11" powershell -NoProfile -ExecutionPolicy Bypass \
    -File '\\Mac\Home\RiderProjects\superslap\tools\release\windows\make-cert.ps1'
```

It creates a self-signed code-signing certificate for `CN=Paul F. Johnson`, keeps
the `.pfx` and its password under `C:\msix`, and exports
`SuperSlapper-selfsigned.cer` next to the packages. That `.cer` has to be trusted
on any machine the packages are sideloaded onto:

```powershell
certutil -addstore Root SuperSlapper-selfsigned.cer
```

The certificate is deliberately not recreated per build: a new one changes the
package identity. For a Store listing, replace it with a certificate from a CA
or Partner Center and keep the manifest's `Publisher` in step with its subject.

## 4. Checking a build

```bash
prlctl exec "Windows 11" powershell -NoProfile -ExecutionPolicy Bypass \
    -File '\\Mac\Home\RiderProjects\superslap\tools\release\windows\provision-check.ps1' \
    -Share '\\Mac\Home\RiderProjects\superslap\Release\windows'
```

It verifies both signatures and provisions the arm64 package for all users, which
is the same manifest and payload validation an install performs. Provisioning is
used rather than `Add-AppxPackage` because `prlctl exec` runs as Local System,
which the deployment stack refuses to install for.
