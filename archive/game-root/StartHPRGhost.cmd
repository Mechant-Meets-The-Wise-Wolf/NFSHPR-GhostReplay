@echo off
if not exist "%~dp0NFS11Remastered.exe" (
 echo Install this package in the folder containing NFS11Remastered.exe.
 pause
 exit /b 1
)
if not exist "%~dp0mod-research\ghost-system\HPRGhostReplay.exe" (
 echo HPRGhostReplay.exe is missing. Please extract the entire package again.
 pause
 exit /b 1
)
start "" "%~dp0mod-research\ghost-system\HPRGhostReplay.exe"