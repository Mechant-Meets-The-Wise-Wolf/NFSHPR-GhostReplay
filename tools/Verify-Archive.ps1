param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'

$versionFile = Join-Path $Root 'VERSION'
if (!(Test-Path -LiteralPath $versionFile)) { throw 'VERSION not found' }

$values = @{}
foreach ($line in (Get-Content -LiteralPath $versionFile -Encoding UTF8)) {
    $trim = $line.Trim()
    if ($trim.Length -eq 0 -or $trim.StartsWith('#')) { continue }
    $index = $trim.IndexOf('=')
    if ($index -lt 1) { continue }
    $values[$trim.Substring(0, $index).Trim()] = $trim.Substring($index + 1).Trim()
}

$script:failed = 0

function Compare-Hash([string]$Label, [string]$Expected, [string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) {
        Write-Output ('MISSING  ' + $Label + '  ' + $Path)
        $script:failed++
        return
    }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -eq $Expected.ToLowerInvariant()) {
        Write-Output ('OK       ' + $Label + '  ' + $actual)
    }
    else {
        Write-Output ('MISMATCH ' + $Label)
        Write-Output ('         expected ' + $Expected)
        Write-Output ('         actual   ' + $actual)
        $script:failed++
    }
}

Compare-Hash 'exe'    $values['exe_sha256']    (Join-Path $Root 'app/HPRGhostReplay.exe')
Compare-Hash 'native' $values['asi_sha256']    (Join-Path $Root 'native/bin/HPRGhostNative.asi')
Compare-Hash 'loader' $values['loader_sha256'] (Join-Path $Root 'native/bin/UltimateASILoader-x64.dll')

if ($values['exe_file_version']) {
    $fileVersion = (Get-Item -LiteralPath (Join-Path $Root 'app/HPRGhostReplay.exe')).VersionInfo.FileVersion
    if ($fileVersion -eq $values['exe_file_version']) {
        Write-Output ('OK       exe_file_version  ' + $fileVersion)
    }
    else {
        Write-Output ('MISMATCH exe_file_version')
        Write-Output ('         expected ' + $values['exe_file_version'])
        Write-Output ('         actual   ' + $fileVersion)
        $script:failed++
    }
}

if ($values['package']) {
    $zipPath = Join-Path $Root ('releases/' + $values['package'])
    Compare-Hash 'package' $values['package_zip_sha256'] $zipPath

    if (Test-Path -LiteralPath $zipPath) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq 'HPRGhostReplay/HPRGhostReplay.exe' } | Select-Object -First 1
            if (!$entry) {
                Write-Output 'MISSING  package-exe  HPRGhostReplay/HPRGhostReplay.exe'
                $script:failed++
            }
            else {
                $stream = $entry.Open()
                try {
                    $sha = [Security.Cryptography.SHA256]::Create()
                    $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
                    $sha.Dispose()
                }
                finally { $stream.Dispose() }
                if ($hash -eq $values['exe_sha256'].ToLowerInvariant()) {
                    Write-Output ('OK       package-exe  ' + $hash)
                }
                else {
                    Write-Output 'MISMATCH package-exe'
                    Write-Output ('         expected ' + $values['exe_sha256'])
                    Write-Output ('         actual   ' + $hash)
                    $script:failed++
                }
            }
        }
        finally { $zip.Dispose() }
    }
}

if ($script:failed -gt 0) {
    Write-Output ('RESULT   failed=' + $script:failed)
    exit 1
}
Write-Output 'RESULT   ok'
exit 0
