# Third-party notices

Eidolon vendors or links the following third-party components. Their licences apply to those components,
not to Eidolon's own source.

## Assimp

- **Project:** Open Asset Import Library (assimp) — https://github.com/assimp/assimp
- **Used by:** `EidolonAssimp` (model import), via the `Silk.NET.Assimp` bindings and the
  `Ultz.Native.Assimp` binaries.
- **Licence:** BSD-3-Clause. The full text ships with the assimp distribution; the requirement that matters
  here is that the copyright notice, conditions and disclaimer are retained in this repository and in
  binary distributions.

Formats commonly reached through it: FBX, OBJ, DAE (Collada), glTF/GLB, 3DS, MD5, Blend and others.
Assimp does **not** read USD; that would need OpenUSD as a separate dependency.

## Silk.NET

- **Project:** Silk.NET — https://github.com/dotnet/Silk.NET
- **Used by:** the Vulkan backend, windowing/input, ImGui host and the Assimp bindings.
- **Licence:** MIT.

## ImGui.NET

- **Project:** ImGui.NET — https://github.com/ImGuiNET/ImGui.NET (bindings for Dear ImGui,
  https://github.com/ocornut/imgui)
- **Used by:** the editor UI.
- **Licence:** MIT (Dear ImGui is also MIT).
