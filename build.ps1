<#
.SYNOPSIS
  Publica o coomer como exe unico (NativeAOT, self-contained, sem .NET instalado).

.DESCRIPTION
  Poe o vswhere no PATH pra o linker nativo achar o MSVC sozinho — assim nao
  precisa abrir o "Developer PowerShell for VS" toda vez. Saida em .\dist
  (coomer.exe + glfw3.dll). O .pdb e descartado da distribuicao.

.PARAMETER Version
  Sobrescreve o <Version> do csproj (ex: 0.2.1). Opcional.

.PARAMETER Install
  Depois de buildar, roda 'coomer.exe --install' (autostart no login).

.EXAMPLE
  .\build.ps1
  .\build.ps1 -Version 0.2.1 -Install
#>
[CmdletBinding()]
param(
  [string]$Version,
  [switch]$Install
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'

# vswhere fica fora do PATH por padrao; o alvo do NativeAOT chama ele pra
# localizar o link.exe do MSVC. Sem isso, falha com exit 123 no passo de link.
$installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $installer) { $env:PATH = "$installer;$env:PATH" }

$pubArgs = @(
  'publish', (Join-Path $root 'coomer'),
  '-c', 'Release', '-r', 'win-x64',
  '-o', $dist
)
if ($Version) { $pubArgs += "-p:Version=$Version" }

Write-Host "==> dotnet $($pubArgs -join ' ')" -ForegroundColor Cyan
dotnet @pubArgs
if ($LASTEXITCODE -ne 0) { throw "publish falhou (exit $LASTEXITCODE)" }

# Simbolos de debug nao vao pra distribuicao.
Remove-Item (Join-Path $dist 'coomer.pdb') -ErrorAction SilentlyContinue

Write-Host "`n==> dist:" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, @{n = 'MB'; e = { '{0:N1}' -f ($_.Length / 1MB) } } | Format-Table -AutoSize

if ($Install) {
  Write-Host "==> coomer --install" -ForegroundColor Cyan
  & (Join-Path $dist 'coomer.exe') --install
}
