@ECHO OFF

setlocal
set EnableNuGetPackageRestore=true

set MSBUILDEXE=msbuild.exe

set cfgOption=/p:Configuration=Release
REM set cfgOption=/p:Configuration=Debug
REM set cfgOption=/p:Configuration=Debug;Release

REM set logOptions=/v:d /flp:Summary;Verbosity=diag;LogFile=msbuild.log /flp1:warningsonly;logfile=msbuild.wrn /flp2:errorsonly;logfile=msbuild.err
set logOptions=/v:diag /flp:Summary;Verbosity=diag;LogFile=msbuild.log /flp1:warningsonly;logfile=msbuild.wrn /flp2:errorsonly;logfile=msbuild.err

%MSBUILDEXE% "%~dp0\MicrosoftConfigurationBuilders.msbuild" %cfgOption% %logOptions% /maxcpucount /nodeReuse:false %*

endlocal
