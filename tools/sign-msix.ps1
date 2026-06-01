# tools\sign-msix.ps1
# Local dev MSIX signing helper. Creates a self-signed cert if one isn't
# present, then signs the given .msix or .msixbundle.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [string] $CertSubject = "CN=LocalAIDemosDev",
    [string] $CertStore   = "Cert:\CurrentUser\My"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PackagePath)) { throw "Package not found: $PackagePath" }

$cert = Get-ChildItem $CertStore | Where-Object { $_.Subject -eq $CertSubject } | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating self-signed cert $CertSubject (valid 3 years)…"
    $cert = New-SelfSignedCertificate `
        -Subject $CertSubject `
        -CertStoreLocation $CertStore `
        -Type CodeSigningCert `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(3) `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
    Write-Host "Created. Thumbprint: $($cert.Thumbprint)"
    Write-Host "Trust it on this machine with:"
    Write-Host "  `$pwd = ConvertTo-SecureString 'devpwd' -AsPlainText -Force"
    Write-Host "  Export-PfxCertificate -Cert $($cert.PSPath) -FilePath dev.pfx -Password `$pwd"
    Write-Host "  Import-Certificate -FilePath (Export-Certificate -Cert $($cert.PSPath) -FilePath dev.cer) -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}

$signTool = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1)
if (-not $signTool) { throw "signtool.exe not found. Install the Windows 10/11 SDK." }

& $signTool.FullName sign /sha1 $cert.Thumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $PackagePath
if ($LASTEXITCODE -ne 0) { throw "Signing failed." }

Write-Host "Signed $PackagePath with $($cert.Subject) ($($cert.Thumbprint))"
