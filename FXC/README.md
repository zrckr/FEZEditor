# FXC Shader Compiler

This folder contains `fxc.exe` from the DirectX SDK (June 2010), used to compile
editor shaders with the `fx_2_0` profile. Modern versions of `fxc.exe` shipped
with the Windows SDK may no longer support this profile.

On **Windows**, `fxc.exe` is invoked directly by the build targets. It requires
`D3DCOMPILER_43.dll` (included here, 64-bit).

On **Linux/macOS**, `fxc.exe` is run via `wine`. Install the `wine64` package
(provides the `wine` binary) and ensure `D3DCOMPILER_43.dll` is accessible to it.
