param([string]$Executable = 'app\HPRGhostReplay.exe', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$packageSource = $PSScriptRoot
$packageExe = Join-Path $packageSource $Executable
if (!(Test-Path -LiteralPath $packageExe -PathType Leaf)) { throw 'Build the executable first.' }
$packageAssembly = [System.Reflection.Assembly]::LoadFile($packageExe)
$packageConfig = $packageAssembly.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyConfigurationAttribute' }
if (!$packageConfig -or $packageConfig.ConstructorArguments[0].Value -ne 'Release') { throw 'Only a Release build can be packaged. Development tools must stay local.' }
$packageVersion = (Get-Item -LiteralPath $packageExe).VersionInfo.ProductVersion
if ($packageVersion -notmatch '^\d+\.\d+$') { throw ('Unexpected test-release version: '+$packageVersion) }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $packageSource 'releases' }
$packageOutput = [IO.Path]::GetFullPath($OutputDirectory)
$packageStem = 'HPRGhostReplay-'+$packageVersion+'-win-x64'
$packageZip = Join-Path $packageOutput ($packageStem+'.zip')
$packageStage = Join-Path $packageOutput ($packageStem+'-contents')
$packageRoot = Join-Path $packageStage 'HPRGhostReplay'
if ((Test-Path -LiteralPath $packageZip) -or (Test-Path -LiteralPath $packageStage)) { throw 'Release already exists; no existing release will be overwritten.' }
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'Recordings') -Force | Out-Null
Copy-Item -LiteralPath $packageExe -Destination (Join-Path $packageRoot 'HPRGhostReplay.exe')
foreach ($packageDoc in @('使用说明.txt','第三方许可.txt')) { Copy-Item -LiteralPath (Join-Path $packageSource ('packaging\'+$packageDoc)) -Destination (Join-Path $packageRoot $packageDoc) }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot,$packageZip,[IO.Compression.CompressionLevel]::Optimal,$true)
$packageHash = (Get-FileHash -LiteralPath $packageZip -Algorithm SHA256).Hash.ToLowerInvariant()
($packageHash+'  '+[IO.Path]::GetFileName($packageZip)) | Set-Content -LiteralPath ($packageZip+'.sha256') -Encoding ascii
Write-Output ('ZIP: '+$packageZip)
Write-Output ('SHA256: '+$packageHash)

