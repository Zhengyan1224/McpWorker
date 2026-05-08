@echo off
setlocal enabledelayedexpansion


set runtime=win-x64
set framework=net8.0
set output_dir=..\..\publish

if not "%~1"=="" set runtime=%1
if not "%~2"=="" set framework=%2
if not "%~3"=="" set output_dir=%3

for /d %%d in (*) do (
    echo Processing directory: %%d
    cd %%d
    
    if exist build.bat (
        echo exec build.bat %runtime% %framework% %output_dir%
        call build.bat %runtime% %framework% %output_dir%
    )
    
    cd ..
)

endlocal