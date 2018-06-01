@ECHO OFF

setlocal

set MSBUILDEXE=msbuild.exe

REM set logOptions=/v:d /flp:Summary;Verbosity=diag;LogFile=msbuild.log /flp1:warningsonly;logfile=msbuild.wrn /flp2:errorsonly;logfile=msbuild.err
set logOptions=/v:diag /flp:Summary;Verbosity=diag;LogFile=msbuild.log /flp1:warningsonly;logfile=msbuild.wrn /flp2:errorsonly;logfile=msbuild.err

set cfgOption=/p:Configuration=Release
REM set cfgOption=/p:Configuration=Debug
REM set cfgOption=/p:Configuration=Debug;Release

%MSBUILDEXE% "%~dp0\MicrosoftConfigurationBuilders.msbuild" /t:Clean %cfgOption% %logOptions% /maxcpucount /nodeReuse:false %*
del /F msbuild.log
del /F msbuild.wrn
del /F msbuild.err

endlocal
