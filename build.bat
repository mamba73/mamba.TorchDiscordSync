@echo off
setlocal

set CONFIG=Release
set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set PROJ=mamba.TorchDiscordSync.csproj
set OUTDIR=bin\%CONFIG%
set ZIPNAME=mamba.TorchDiscordSync.zip

echo === RESTORE ===
dotnet restore %PROJ% || goto fail

echo === BUILD ===
%MSBUILD% %PROJ% /p:Configuration=%CONFIG% /v:quiet || goto fail

echo === CLEAN ZIP ===
if exist %ZIPNAME% del %ZIPNAME%

echo === CREATE ZIP ===
powershell -Command "Compress-Archive -Path '%OUTDIR%\mamba.TorchDiscordSync.dll','manifest.xml' -DestinationPath '%ZIPNAME%'"

echo === DONE ===
echo Created: %ZIPNAME%
pause
exit /b 0

:fail
echo BUILD FAILED
pause
exit /b 1
