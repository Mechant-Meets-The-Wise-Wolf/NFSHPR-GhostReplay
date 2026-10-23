param([string]$ZigPath='')
$ErrorActionPreference='Stop'
$taskResearch=Join-Path (Split-Path $PSScriptRoot -Parent) 'research\native-ghost-prototype'
if(!$ZigPath){$ZigPath=Join-Path $taskResearch 'tools\zig-windows-x86_64-0.13.0\zig.exe'}
if(!(Test-Path -LiteralPath $ZigPath)){throw 'Set -ZigPath to a portable Zig 0.13 C++ compiler.'}
$taskVendor=Join-Path $taskResearch 'vendor\minhook-1.3.4'
$taskBuild=Join-Path $PSScriptRoot 'build'
$taskBin=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $taskBuild,$taskBin -Force | Out-Null
$env:ZIG_GLOBAL_CACHE_DIR=Join-Path $taskResearch 'build\zig-cache'
$taskObjects=@()
foreach($taskFile in @('src\buffer.c','src\hook.c','src\trampoline.c','src\hde\hde64.c')){
 $taskObject=Join-Path $taskBuild (([IO.Path]::GetFileNameWithoutExtension($taskFile))+'.o')
 & $ZigPath cc -target x86_64-windows-gnu -O2 -c (Join-Path $taskVendor $taskFile) -o $taskObject
 if($LASTEXITCODE -ne 0){throw 'MinHook compilation failed'}
 $taskObjects+=$taskObject
}
$taskArgs=@('-target','x86_64-windows-gnu','-std=c++17','-O2','-static','-I',(Join-Path $taskVendor 'include'),(Join-Path $PSScriptRoot 'src\NativeGhost.cpp'))+$taskObjects+@('-ld3d11','-ldxgi','-luser32','-lkernel32','-luuid')
& $ZigPath c++ @taskArgs -shared -o (Join-Path $taskBin 'HPRGhostNative.asi')
if($LASTEXITCODE -ne 0){throw 'Native playback build failed'}
& $ZigPath c++ @taskArgs -DGHOST_SMOKE -municode -o (Join-Path $taskBuild 'NativeSmoke.exe')
if($LASTEXITCODE -ne 0){throw 'Native verification build failed'}
Copy-Item -LiteralPath (Join-Path $taskVendor 'LICENSE.txt') -Destination (Join-Path $taskBin 'MinHook-LICENSE.txt')
Write-Output 'Built integrated native playback.'
