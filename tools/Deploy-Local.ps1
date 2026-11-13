param(
    [string]$Configuration = 'Release',
    [switch]$SkipBuild,
    [switch]$SkipRegister
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$appProjDir = Join-Path $repoRoot 'src\WidBar.DailyVerse.ExtensionApp'
$csproj = Join-Path $appProjDir 'WidBar.DailyVerse.ExtensionApp.csproj'
$iconsDir = Join-Path $appProjDir 'Package\Images'
$layoutDir = Join-Path $repoRoot 'dist-layout'

if (-not (Test-Path (Join-Path $iconsDir 'AppIcon.scale-200.png'))) {
    & (Join-Path $PSScriptRoot 'New-PackageIcons.ps1') -OutDir $iconsDir
}

if (-not $SkipBuild) {
    Write-Host '[1/4] Publicando WidBar.DailyVerse.ExtensionApp (win-x64)...'
    & dotnet publish $csproj -c $Configuration -p:Platform=x64 -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        throw 'Falha ao publicar WidBar.DailyVerse.ExtensionApp.'
    }
}

$binDir = Join-Path $appProjDir "bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64"
$publishDir = Join-Path $binDir 'publish'
$pluginJson = Join-Path $appProjDir 'obj\widbar\plugin.json'
$generatedManifest = Join-Path $binDir 'AppxManifest.xml'
$manifestSrc = if (Test-Path $generatedManifest) { $generatedManifest } else { Join-Path $appProjDir 'Package\AppxManifest.xml' }

if (-not (Test-Path (Join-Path $publishDir 'WidBar.DailyVerse.ExtensionApp.exe'))) {
    throw "Executável publicado não encontrado em $publishDir"
}
if (-not (Test-Path $pluginJson)) {
    throw "Manifesto WidBar obj\widbar\plugin.json não encontrado em $pluginJson"
}

Write-Host "[2/4] Preparando layout MSIX em $layoutDir..."
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $layoutDir 'Public') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $layoutDir 'Images') | Out-Null

Copy-Item -Path (Join-Path $publishDir '*') -Destination $layoutDir -Recurse -Force
Copy-Item -Path $pluginJson -Destination (Join-Path $layoutDir 'Public\plugin.json') -Force
Copy-Item -Path (Join-Path $iconsDir '*.png') -Destination (Join-Path $layoutDir 'Images') -Force
Copy-Item -Path $manifestSrc -Destination (Join-Path $layoutDir 'AppxManifest.xml') -Force

$sdkPriCandidates = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\widbar.sdk') -Filter 'WidBar.SDK.pri' -Recurse -ErrorAction SilentlyContinue
if ($sdkPriCandidates) {
    $latestSdkPri = $sdkPriCandidates | Sort-Object FullName -Descending | Select-Object -First 1
    Copy-Item -Path $latestSdkPri.FullName -Destination (Join-Path $layoutDir 'WidBar.SDK.pri') -Force
}

Write-Host '[3/4] Gerando índice único resources.pri com App.xbf + Images...'
$makepri = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter 'makepri.exe' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $makepri) {
    throw 'makepri.exe (Windows 10/11 SDK) não encontrado.'
}

$basePri = @(
    (Join-Path $publishDir 'resources.pri'),
    (Join-Path $binDir 'resources.pri'),
    (Join-Path $publishDir 'WidBar.DailyVerse.ExtensionApp.pri')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $basePri) {
    throw 'Nenhum arquivo .pri com App.xbf foi encontrado na saída de build.'
}

$priStageRoot = Join-Path $repoRoot 'obj\pri-stage\input'
New-Item -ItemType Directory -Force -Path (Join-Path $priStageRoot 'Images') | Out-Null
Copy-Item -Path (Join-Path $iconsDir '*.png') -Destination (Join-Path $priStageRoot 'Images') -Force
Copy-Item -Path $basePri -Destination (Join-Path $priStageRoot 'AppBase.pri') -Force
Copy-Item -Path $manifestSrc -Destination (Join-Path $priStageRoot 'AppxManifest.xml') -Force

Get-ChildItem $layoutDir -Filter 'resources.*.pri' -ErrorAction SilentlyContinue | Remove-Item -Force

$priCfg = Join-Path (Split-Path $priStageRoot -Parent) 'priconfig-single.xml'
$priConfigXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
  <index root="\" startIndexAt="\">
    <default>
      <qualifier name="Language" value="pt-BR"/>
      <qualifier name="Contrast" value="standard"/>
      <qualifier name="Scale" value="100"/>
      <qualifier name="HomeRegion" value="001"/>
      <qualifier name="TargetSize" value="256"/>
      <qualifier name="LayoutDirection" value="LTR"/>
      <qualifier name="Theme" value="dark"/>
      <qualifier name="AlternateForm" value=""/>
      <qualifier name="DXFeatureLevel" value="DX9"/>
      <qualifier name="Configuration" value=""/>
      <qualifier name="DeviceFamily" value="Universal"/>
      <qualifier name="Custom" value=""/>
    </default>
    <indexer-config type="folder" foldernameAsQualifier="true" filenameAsQualifier="true" qualifierDelimiter="."/>
    <indexer-config type="PRI"/>
  </index>
</resources>
'@
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($priCfg, $priConfigXml, $utf8NoBom)

$outPriPrefix = Join-Path $layoutDir 'resources'
& $makepri new /pr "$priStageRoot" /cf "$priCfg" /mn "$priStageRoot\AppxManifest.xml" /in 'WidBar.DailyVerse.Widget' /of "$outPriPrefix" /o
if ($LASTEXITCODE -ne 0) { throw 'makepri new falhou.' }

if (-not $SkipRegister) {
    Write-Host '[4/4] Registrando extensão WidBar em Modo de Desenvolvedor...'
    $manifestPath = Join-Path $layoutDir 'AppxManifest.xml'
    try {
        Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown -ForceUpdateFromAnyVersion -ErrorAction Stop
    }
    catch {
        Write-Warning 'Se o erro mencionar sideload/licença de desenvolvedor, ative: Configurações -> Sistema -> Para desenvolvedores -> Modo de desenvolvedor.'
        throw
    }
    $pkg = Get-AppxPackage -Name 'WidBar.DailyVerse.Widget'
    if (-not $pkg) {
        throw 'O pacote WidBar.DailyVerse.Widget não apareceu em Get-AppxPackage após o registro.'
    }
    Write-Host "Pacote registrado: $($pkg.PackageFullName) (Status: $($pkg.Status))"
}
else {
    Write-Host '[4/4] Registro Appx ignorado (-SkipRegister).'
}

Write-Host 'Concluído.'
