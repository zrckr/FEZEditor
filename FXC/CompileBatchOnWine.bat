@echo off
cd /d "%~dp0..\Content\Effects"
for %%f in (*.fx) do (
    %~dp0fxc.exe /nologo /Vd /T fx_2_0 /Fo "%%~nf.fxb" "%%f"
)
