# macOS Setup Guide (26.3 Baseline)

This guide is for local development on macOS against the current repository baseline:

- `TargetFramework`: `net9.0`
- `MonoGame.Framework.DesktopGL`: `3.8.4.1`
- `MonoGame.Content.Builder.Task`: `3.8.4.1`
- `Gum.MonoGame`: `2026.3.28.2`
- `Gum.Shapes.MonoGame`: `2026.3.28.2`

It assumes you want to build and run the current C# / MonoGame desktop project at
`src/TriloGame.Game/TriloGame.Game.csproj`.

## Supported macOS Target

The game uses MonoGame DesktopGL, which MonoGame documents as supporting macOS
Catalina 10.15 and newer.

## 1. Install Required Tools

### .NET SDK

Install the .NET 9 SDK for your Mac architecture:

- Apple Silicon (`M1`, `M2`, `M3`, `M4`): install the macOS `Arm64` SDK
- Intel Mac: install the macOS `x64` SDK

Official installer:

- https://dotnet.microsoft.com/download/dotnet/9.0

After installation, confirm the SDK is available:

```bash
dotnet --info
dotnet --list-sdks
```

You should see a `.NET 9` SDK in the output.

### Git

Install Git if it is not already available:

```bash
git --version
```

If needed, install Xcode Command Line Tools:

```bash
xcode-select --install
```

### Homebrew

Homebrew is optional for the core build, but it is useful for troubleshooting and for the
optional shader toolchain setup below.

Official install instructions:

- https://brew.sh/

## 2. Clone the Repo

```bash
git clone <your-repo-url>
cd TriloGame
```

If you cloned the repo into a differently named folder, use that folder instead.

## 3. Restore, Build, Test, and Run

Run all commands from the repository root.

### Restore

```bash
dotnet restore src/TriloGame.Game/TriloGame.Game.csproj
```

### Build

```bash
dotnet build TriloGame.sln
```

### Test

```bash
dotnet test src/TriloGame.Tests/TriloGame.Tests.csproj
```

### Run

```bash
dotnet run --project src/TriloGame.Game/TriloGame.Game.csproj -c Debug
```

## 4. Important Repo-Specific Notes

### Use `dotnet` commands on macOS

The repo-root `launch` and `start` helpers are Windows `.cmd` wrappers:

- `launch.cmd`
- `start.cmd`
- `dotnet-launch.cmd`

They are not directly runnable from a normal macOS shell. Use the `dotnet restore`,
`dotnet build`, and `dotnet run` commands shown above.

### Content still builds through MGCB

Do not bypass the MonoGame content pipeline. This project still builds content through:

- `src/TriloGame.Game/Content/Content.mgcb`

The project already references `MonoGame.Content.Builder.Task`, so normal `dotnet build`
and `dotnet run` flows will invoke MGCB as part of the build.

### Shader setup is optional right now

MonoGame's effect compiler on macOS uses Wine. This repository's current
`Content.mgcb` does not include any `.fx` shader assets, so you do not need the Wine
setup just to build and run the game today.

If we add `.fx` content later, follow MonoGame's official macOS effect-compilation setup:

```bash
brew install wget p7zip curl
brew install --cask wine-stable
xattr -dr com.apple.quarantine "/Applications/Wine Stable.app"
wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash
```

## 5. Recommended Editor Options

Any editor that works well with the .NET SDK is fine. Common choices on macOS:

- Visual Studio Code with the C# extension/C# Dev Kit
- JetBrains Rider

The CLI commands above are the source of truth even if you use an IDE.

## 6. Troubleshooting

### `dotnet` command not found

Restart Terminal after installing the SDK. If it still fails, re-run:

```bash
dotnet --info
```

If the command is still missing, reinstall the official macOS SDK package for the correct
architecture.

### Wrong architecture SDK on Apple Silicon

If you are on Apple Silicon, prefer the native `Arm64` .NET SDK unless you specifically
need an x64 toolchain. Mixing x64 and Arm64 SDK installs can lead to confusing path issues.

### Native window opens but rendering/audio fails

MonoGame DesktopGL depends on SDL/OpenGL/OpenAL at runtime. If the app launches but fails
very early, confirm you are on a supported macOS version and that you are using the current
official .NET SDK for your machine architecture.

### Build passes but content or asset loads fail

macOS can expose filename casing mismatches more aggressively than Windows. If an asset fails
to load, verify that the path in code matches the exact case used under
`src/TriloGame.Game/Content/`.

## 7. One-Command Dev Loop

Once the prerequisites are installed, the normal inner loop is:

```bash
dotnet build TriloGame.sln
dotnet test src/TriloGame.Tests/TriloGame.Tests.csproj
dotnet run --project src/TriloGame.Game/TriloGame.Game.csproj -c Debug
```

## References

- MonoGame macOS setup:
  https://docs.monogame.net/articles/getting_started/1_setting_up_your_os_for_development_macos.html
- MonoGame supported platforms:
  https://docs.monogame.net/articles/getting_started/platforms.html
- .NET install on macOS:
  https://learn.microsoft.com/en-us/dotnet/core/install/macos
