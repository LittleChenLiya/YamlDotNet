@echo off
rem Local acceptance command for the explicit string style selector.
rem Runs only the StringStyleSelector emitter integration tests and the sample.
setlocal
pushd "%~dp0\.."

if "%FRAMEWORK%"=="" set "FRAMEWORK=net10.0"

echo == StringStyleSelector unit and Emitter integration tests (%FRAMEWORK%) ==
dotnet test YamlDotNet.Test\YamlDotNet.Test.csproj -f %FRAMEWORK% --filter "FullyQualifiedName~StringStyleSelector" %*
if errorlevel 1 goto :fail

echo == UseStringStyleSelector sample ==
dotnet test YamlDotNet.Samples\YamlDotNet.Samples.csproj --filter "FullyQualifiedName~UseStringStyleSelector" %*
if errorlevel 1 goto :fail

popd
endlocal
exit /b 0

:fail
popd
endlocal
exit /b 1
