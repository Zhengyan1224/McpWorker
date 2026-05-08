@echo off
set runtime=%1
set framework=%2
set output_dir=%3

for %%i in ("%cd%") do set "dirname=%%~nxi"

if "%runtime%"=="" set runtime=win-x64
if "%framework%"=="" set framework=net8.0
if "%output_dir%"=="" set output_dir=../../publish

dotnet publish -r %runtime% -f %framework% -p:PublishSingleFile=true -o %output_dir%/%dirname%
