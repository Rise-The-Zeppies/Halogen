param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Source,
    [Parameter(Position = 1)]
    [string]$AppName = "HalogenUIApp",
    [Parameter(Position = 2)]
    [string]$OutputDirectory = ".\dist"
)

$ErrorActionPreference = "Stop"

$SourcePath = (Resolve-Path -LiteralPath $Source).Path
if ([System.IO.Path]::GetExtension($SourcePath) -ine ".hg") {
    throw "Source must be a Halogen .hg file."
}

$AppName = $AppName.Trim()
if ([string]::IsNullOrWhiteSpace($AppName)) {
    throw "AppName cannot be empty."
}
$SafeName = ($AppName -replace '[<>:"/\\|?*]', '-').TrimEnd('.', ' ')
if ([string]::IsNullOrWhiteSpace($SafeName)) {
    throw "AppName is not a valid Windows filename."
}

$ProjectPath = Join-Path $PSScriptRoot "HalogenWindows.csproj"
$ResolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$PublishPath = Join-Path $ResolvedOutput ".publish"
$PackagePath = Join-Path $ResolvedOutput $SafeName
$ZipPath = Join-Path $ResolvedOutput "$SafeName-Windows-x64.zip"

New-Item -ItemType Directory -Force -Path $ResolvedOutput | Out-Null
if (Test-Path $PublishPath) { Remove-Item -Recurse -Force $PublishPath }
if (Test-Path $PackagePath) { Remove-Item -Recurse -Force $PackagePath }
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }

Write-Host "Publishing self-contained Windows x64 runtime..."
dotnet publish $ProjectPath --configuration Release --runtime win-x64 --self-contained true --output $PublishPath -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

New-Item -ItemType Directory -Force -Path $PackagePath | Out-Null
Copy-Item (Join-Path $PublishPath "Halogen.exe") (Join-Path $PackagePath "$SafeName.exe")
Copy-Item $SourcePath (Join-Path $PackagePath "Main.hg")
@"
$SafeName - HalogenUI app

Double-click $SafeName.exe to launch the bundled Main.hg UI.
This Windows package is self-contained and targets 64-bit Windows.
"@ | Set-Content -Encoding UTF8 (Join-Path $PackagePath "README.txt")

Compress-Archive -Path (Join-Path $PackagePath "*") -DestinationPath $ZipPath -CompressionLevel Optimal
Remove-Item -Recurse -Force $PublishPath

Write-Host ""
Write-Host "HalogenUI Windows package created:"
Write-Host "  App folder: $PackagePath"
Write-Host "  ZIP:        $ZipPath"
