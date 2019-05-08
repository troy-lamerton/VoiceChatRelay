@ECHO OFF

IF NOT DEFINED MSBuild (SET MSBuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe")
%MSBuild% VivoxVoiceRelayWindows.csproj -property:Configuration=Release -property:Platform=x86
