@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Invoke-PackageBuild.ps1" -RepositoryRoot "%~dp0." %*
exit /b %ERRORLEVEL%
