@echo off
:waitfortask
for /f %%i in ('tasklist') do (
  if "%%i" == "BC" (
    timeout /t 2 /nobreak >nul
    goto waitfortask
  )
)
timeout /t 1 /nobreak >nul
robocopy temp . /e /r:1 /w:1 /dst
timeout /t 1 /nobreak >nul
rmdir /s /q temp
timeout /t 1 /nobreak >nul
start "" "BC Update Utilities.exe"