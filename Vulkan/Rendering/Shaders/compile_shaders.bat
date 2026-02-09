@echo off
set GLSL_COMPILER="C:\VulkanSDK\1.4.328.1\Bin\glslc.exe"

for %%f in (*.vert, *.frag) do (
    echo Compiling %%f...
    %GLSL_COMPILER% %%f -o %%f.spv
)
pause