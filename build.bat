@echo off
set CONFIG=Release
set OUTDIR=bin\%CONFIG%
set ZIPNAME=mamba.TorchDiscordSync.zip

echo === BUILD ===
dotnet build mamba.TorchDiscordSync.csproj -c %CONFIG%
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)

echo === CLEAN ZIP ===
if exist %ZIPNAME% del %ZIPNAME%

echo === CREATE ZIP ===
powershell -Command ^
"Compress-Archive -Path '%OUTDIR%\mamba.TorchDiscordSync.dll','manifest.xml' -DestinationPath '%ZIPNAME%'"

echo === DONE ===
echo Created: %ZIPNAME%
pause
