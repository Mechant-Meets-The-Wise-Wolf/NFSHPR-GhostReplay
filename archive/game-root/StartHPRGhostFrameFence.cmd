@echo off
tasklist /FI "IMAGENAME eq HPRGhost.exe" /NH | find /I "HPRGhost.exe" >nul
if not errorlevel 1 (
 echo Close the existing HPRGhost tool before starting this diagnostic build.
 pause
 exit /b 1
)
tasklist /FI "IMAGENAME eq HPRGhost-diagnostic.exe" /NH | find /I "HPRGhost-diagnostic.exe" >nul
if not errorlevel 1 (
 echo The diagnostic HPRGhost tool is already running.
 pause
 exit /b 1
)
start "" "%~dp0mod-research\ghost-system\HPRGhost-diagnostic.exe" --frame-fence
