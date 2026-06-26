[CmdletBinding()]
param
(
    [Parameter()]
    $ConfigurationPath,
    [Parameter()]
    $ZipCodeExecutableDirectoryPath,
    [Parameter()]
    $ZipCodeExecutableFileName = "ZipCode.Cli.exe"
)

if (-not $ConfigurationPath)
{
    $ConfigurationPath = Join-Path -Path $PSScriptRoot -ChildPath "zip-code.config.json"
}
else
{
    $ConfigurationPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ConfigurationPath)
}

if (-not $ZipCodeExecutableDirectoryPath)
{
    $localBinDirectoryPath = Join-Path -Path $PSScriptRoot -ChildPath "bin" -AdditionalChildPath "Debug", "net10.0"
    $localBinExecutablePath = Join-Path -Path $localBinDirectoryPath -ChildPath $ZipCodeExecutableFileName
    
    if (Test-Path -LiteralPath $localBinExecutablePath -PathType Leaf)
    {
        $ZipCodeExecutableDirectoryPath = $localBinDirectoryPath
    }
}

$ZipCodeExecutablePath = Join-Path -Path $ZipCodeExecutableDirectoryPath -ChildPath $ZipCodeExecutableFileName

Push-Location -LiteralPath $PSScriptRoot
try
{
    & $ZipCodeExecutablePath pack --config $ConfigurationPath --scope All
}
finally
{
    Pop-Location
}
