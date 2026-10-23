param([string]$OutputName = '', [ValidateSet('Release','Development')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$OutputName) { if ($Configuration -eq 'Release') { $OutputName='app\HPRGhostReplay.exe' } else { $OutputName='app\HPRGhostReplay-dev.exe' } }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Missing .NET Framework 4.x x64 C# compiler' }
Push-Location $PSScriptRoot
try {
 $nativeSources=Get-ChildItem -LiteralPath 'native\src' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
 if(!(Test-Path -LiteralPath 'native\bin\HPRGhostNative.asi') -or $nativeSources.LastWriteTimeUtc -gt (Get-Item -LiteralPath 'native\bin\HPRGhostNative.asi').LastWriteTimeUtc){& (Join-Path $PSScriptRoot 'native\Build.ps1')}
 $buildArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001','/win32icon:assets\HPRGhostReplay.ico','/resource:assets\HPRGhostReplay.ico,HprGhost.AppIcon.ico','/resource:data\events.tsv,HprGhost.events.tsv','/resource:data\vehicles.tsv,HprGhost.vehicles.tsv',('/out:"'+$OutputName+'"'),'/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll','/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll','/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll','/reference:System.Xaml.dll','src\Core.cs','src\GameLayout.cs','src\GameReader.cs','src\Lifecycle.cs','src\Program.cs','src\ModelResources.cs','src\ModelOverlay.cs','src\Timing.cs','src\FrameSampling.cs','src\ReplayLibrary.cs','src\UiControls.cs','src\UiText.cs','src\ControlWindow.cs','src\AppPaths.cs','src\AppLog.cs','src\ErrorLog.cs')
 $buildArgs=$buildArgs | Where-Object {$_ -ne 'src\ModelOverlay.cs'}
 $buildArgs+=@('/resource:native\bin\HPRGhostNative.asi,HprGhost.Native.asi','src\NativePlayback.cs','src\AsiLoader.cs','src\SteamGameDiscovery.cs','src\ComponentDialog.cs','/resource:native\bin\UltimateASILoader-x64.dll,HprGhost.AsiLoader.dll')
 if ($Configuration -eq 'Development') { $buildArgs += @('/define:DEVELOPMENT','src\DevelopmentTools.cs','src\SamplingDiagnostics.cs','src\TimingDiagnostics.cs','src\DevelopmentLog.cs','src\SelfTests.cs','src\ModelChecks.cs','src\ModelOverlay.cs','src\UiTests.cs','src\BilingualUiTests.cs','src\VehicleNameTests.cs','src\LogTests.cs','src\ReleaseLogTests.cs','src\NativeTests.cs','src\LoaderTests.cs','src\StartupInstallTests.cs','src\NativeUiCheck.cs') }
 $build = Start-Process -FilePath $compiler -ArgumentList $buildArgs -NoNewWindow -Wait -PassThru
 if ($build.ExitCode -ne 0) { throw ('Build failed: '+$build.ExitCode) }
 Write-Output ('Built '+$Configuration+': '+$OutputName)
} finally { Pop-Location }
