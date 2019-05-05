#! /usr/bin/pwsh

param (
    [string]$nugetKey
)

$scriptPath = split-path -parent $MyInvocation.MyCommand.Path

$versionConfig = Get-Content -Raw -Path "$scriptPath\version.json" | ConvertFrom-Json
$version = $versionConfig.Version
$nugetPath = "$scriptPath\nuget"

Get-ChildItem -Path $nugetPath -Include *.* -File -Recurse | foreach { $_.Delete()}

$csprojs = Get-ChildItem -Path $scriptPath -Filter *.csproj -Recurse -File

foreach ($current in $csprojs) {
    if ($current -like "*OctoPlus.Console.csproj") {
        continue
    }
    $command = "dotnet pack -p:PackageVersion=$version --output `"$nugetPath`" `"$($current.FullPath)`""
    Write-Host "Going to run: $command"
    Invoke-Expression $command
}

$nupkgs = Get-ChildItem -Path $nugetPath -Filter *.nupkg -Recurse -File

foreach ($current in $nupkgs) {
    $command = "dotnet nuget push `"$($current.FullPath)`" -k `"$nugetKey`""
    Write-Host "Going to push package $($current.FullPath)"
    Invoke-Expression $command
}

Write-Host "Done."