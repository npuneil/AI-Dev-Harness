# tools\new-prototype.ps1
# Scaffold a new prototype by copying one of the harnesses, renaming the
# assembly / namespaces / MSIX identity, and dropping it under src\.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Local','Hybrid')] [string] $Source,
    [Parameter(Mandatory)] [string] $Name
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
$srcDir     = Join-Path $repoRoot 'src'
$sourceDir  = if ($Source -eq 'Local') { Join-Path $srcDir 'LocalAIDevHarness' } else { Join-Path $srcDir 'HybridAIDevHarness' }
$oldName    = Split-Path $sourceDir -Leaf
$targetDir  = Join-Path $srcDir $Name

if (Test-Path $targetDir) { throw "$targetDir already exists." }

Write-Host "Copying $oldName -> $Name …"
robocopy $sourceDir $targetDir /MIR /XD bin obj AppPackages BundleArtifacts .vs | Out-Null

# Rename files
Get-ChildItem $targetDir -Recurse -File | Where-Object { $_.Name -like "$oldName*" } | ForEach-Object {
    $newName = $_.Name -replace [regex]::Escape($oldName), $Name
    Rename-Item $_.FullName $newName
}
Get-ChildItem $targetDir -Recurse -Directory | Where-Object { $_.Name -like "$oldName*" } | Sort-Object FullName -Descending | ForEach-Object {
    $newName = $_.Name -replace [regex]::Escape($oldName), $Name
    Rename-Item $_.FullName $newName
}

# Text replace inside source / project files
$exts = @('.cs','.xaml','.csproj','.sln','.json','.appxmanifest','.md')
Get-ChildItem $targetDir -Recurse -File | Where-Object { $exts -contains $_.Extension.ToLower() } | ForEach-Object {
    (Get-Content $_.FullName -Raw) -replace [regex]::Escape($oldName), $Name | Set-Content $_.FullName -NoNewline
}

Write-Host "Done. Open $targetDir\$Name.sln and build."
