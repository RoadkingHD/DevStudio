# DevStudio

DevStudio is a lightweight development studio (IDE) for Windows built with C# and
Windows Forms on .NET Framework 4.7.2. It provides a complete editing, building,
and debugging environment for **C#** projects (powered by Roslyn and the ICorDebug
debugging APIs) and for **C/C++** projects (compiled with an external MinGW toolchain).

## Features

### Editor (AvalonEdit)
- Syntax coloring for C# (Roslyn classifier) and C/C++ (built-in scanner)
- Four color themes: **Dark**, **Light**, **Blue**, **Solarized Dark** (View > Color Theme)
- **Code completion** (Ctrl+Space or after typing `.`) backed by the Roslyn completion service
- **Syntax tooltips** on hover (symbol kind, signature, doc-comment summary)
- **Colored syntax minimap** with viewport marker, click/drag to scroll
- Right-click context menu: Cut, Copy, Paste, Select All, **Go To Definition**,
  **Find References**, Find
- Built-in find/replace panel inside the editor (Ctrl+F)
- Breakpoint margin (click to toggle, F9 on the caret line)
- Current-line highlight, current-statement highlight while debugging

### Tool windows (tabbed)
- **Output** — build and debugger log
- **Error List** — errors/warnings with double-click navigation
- **Find Results** — find-in-files and Find References results
- **Call Stack** — managed call stack while debug-stopped
- **Breakpoints** — all breakpoints with delete and navigation
- **Locals** — local variables and arguments with values while debug-stopped

### Navigation
- **Go To Definition** (F12 / context menu) — jumps to source or reports metadata definitions
- **Find References** (Shift+F12 / context menu) — results in the Find Results window
- **Go To Line** (Ctrl+G), **Find in Files** (Ctrl+F) with match-case, whole-word, regex options

### Solution Explorer
- Side panel tree with project content (source files, references)
- Add existing file, add DLL reference, remove file/project, set startup project

### C# support (Roslyn)
- Opens `.csproj` projects (classic and SDK-style) or single `.cs` files
- In-process Roslyn compiler with **AnyCPU / x86 / x64** platform selection
  and Debug/Release configurations
- Portable PDBs emitted for full source-level debugging
- **WinForms designer**: open a `.cs` file containing `InitializeComponent` and
  switch to the **Designer** tab — visual surface with toolbox, property grid,
  drag-to-move, and round-tripping of changes back to the source code
- **Add DLL Reference** (Tools menu or Solution Explorer context menu)
- **Only debugger for C#**: a managed debugger built directly on the COM-based
  ICorDebug API (via ClrDebug) — start (F5), break on breakpoints, continue,
  step over (F10), step into (F11), step out (Shift+F11), stop (Shift+F5),
  with call stack and locals inspection

### C/C++ support (external MinGW compiler)
- Opens `.vcxproj` projects or single `.c/.cpp/.h` files
- Compiles with an external `g++` (MinGW): located on `PATH` or under `C:\mingw*`
- `-std=c++17`, `-g -O0` (Debug) / `-O2 -Wall` (Release), `-m32`/`-m64` per platform
- Compiler diagnostics parsed into the Error List
- Runs built executables and streams their output to the Output window
  (debugging is only available for C# programs)

### Building and running DevStudio
Requirements:
- Windows with .NET Framework 4.7.2
- Visual Studio 2019/2022 or the .NET SDK with the .NET Framework 4.7.2 targeting pack
- For C/C++ projects: MinGW-w64 (`g++`)

Build from Visual Studio (open `DevStudio.sln`) or from a developer command prompt:

```
dotnet build src/DevStudio/DevStudio.csproj
```

The output executable is `src/DevStudio/bin/Debug/DevStudio.exe`.

## Samples

- `samples/HelloCSharp` — console application (`.csproj`)
- `samples/HelloForms` — WinForms application with `InitializeComponent`
  (open `MainForm.cs` and switch to the Designer tab)
- `samples/HelloCpp` — C++ console application (`.vcxproj`, needs MinGW)

Open a sample with **File > Open Project**, set breakpoints with F9,
and press F5 to debug the C# samples.

## Keyboard shortcuts

| Shortcut | Command |
|---|---|
| F5 | Start Debugging / Continue |
| Shift+F5 | Stop Debugging |
| F6 | Build Solution |
| F9 | Toggle Breakpoint |
| F10 | Step Over |
| F11 | Step Into |
| Shift+F11 | Step Out |
| Ctrl+F5 | Start Without Debugging |
| Ctrl+F | Find in Files |
| Ctrl+G | Go To Line |
| Ctrl+N / Ctrl+Shift+N | New File / New Project |
| Ctrl+O / Ctrl+Shift+O | Open Single File / Open Project |
| Ctrl+S / Ctrl+Shift+S | Save / Save All |
