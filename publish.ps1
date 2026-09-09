#Requires -Version 5.1
<#
.SYNOPSIS
  Publica o HubApp (leve, framework-dependent) e reinicia o app.
  Use sempre este script — publicar/publicar manualmente e rodar o exe
  velho foi a causa do "não apareceu as novas opções".
#>
param([string]$Config = "Debug")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Get-Process Nook -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

dotnet publish "$root/Nook/Nook.csproj" -c $Config `
  -o "$root/Nook/publish" --self-contained false --nologo -v minimal

# Limpa binários legados do nome antigo (uma vez; depois não existem mais).
Remove-Item "$root/Nook/publish/HubApp.exe", "$root/Nook/publish/HubApp.pdb" -Force -ErrorAction SilentlyContinue

Start-Process "$root/Nook/publish/Nook.exe"
Write-Output "Nook publicado ($Config) e reiniciado."
