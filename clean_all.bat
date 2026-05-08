@echo off
setlocal enabledelayedexpansion

for /d %%d in (*) do (
    echo Processing directory: %%d
    cd %%d
    
    if exist bin (
        echo delete bin
        rmdir /s /q bin
    )
    
    if exist obj (
        echo delete obj
        rmdir /s /q obj
    )

    if exist logs (
        echo delete logs
        rmdir /s /q logs
    )
    
    cd ..
)

endlocal