This game is an implementation of a game idea I've had for a while. If you are reading this, the game is in progress, and further documentation is on the way!

An old version of the game is deployed to github pages here: https://qu-1ll.github.io/TriloGame/ Feel free to click around!

Current versions are not yet released to the browser, as we have moved the program to a C# build. You can download the latest version from the "releases" tab on the right side of your screen. There are instructions below to actually start the game up.

 ## Release Packaging

To use a downloaded release simply download the zipped files and unzip them. In the root of the release's directory there will be an application file named "TriloGame.Game.exe". Simply open that file and a window will open for the game!

The game window may prompt you for two issues. First, if you don't have .NET 9 installed it will likely have you install it on your device. Next, it may tell you that the authors aren't trusted and you will need to give permission to run the program. When this window appears, simply press "More Info" and then press "Run Anyway" to run the application. (Unless you dont actually trust us, then feel free to close the window and delete the game files from your device.)

You can read the design and road map below, or check out the latest release notes for more information!

We are also working on a much more detailed Wiki for the game's design and gameplay. This documentation is written in markdown, but you can open it with Obsidian to view it properly! See the "TrilobtesObsidian" folder above, and please mind the mess.

# Game Mechanics and Design Plans

## General Gameplay

The Scuttlers is an attempt at combining the colony simulator, tower defense, and roguelike game genres into one game. We plan to do this with an arsenal of intricate game mechanics and lots of unique design inspired by many other games in those genres. 

In this game you will build a city from the ground up while defending your city with your queen inside it from increasingly difficult waves of enemies. The game will challenge players to optimize their base for production and defense simultaneously. Each "run" should only take about 1 to 2 hours maximum before the player defends against a final boss wave of enemies. But don't worry, there will of course be an endless mode for players to continue the insanity!  

While players develop their city and fight wave after wave of enemies, they will also be completing quests and filling in a skill tree. As players defeat rounds of enemies they will draft and place different branches onto their skill tree for the run. Completing quests will then allow players to unlock the skills on the tree as they are putting the tree together, allowing for a unique and self-generated progression every run!

## The Road Map

In the game's current state, we only have a few buildings and some pretty basic visuals to go along with it. Since this game is meant to stand out based on game design, we will be fleshing that aspect of the game out first and foremost. Currently, we are working on the first few chunks of the skill tree. We intend for the "skill lines" to be small constellations of 5-10 skills that can be combined with other skill lines in each run's patchworked skill tree. We will also be working on a similar semi-linear tree for quest lines as well.

As we decide on which skill trees we want to add, how they should work, and which ones we plan to add first, we will be adding them to a proper road map below. Until then just let your imagine wander in how cool this game will be one day!

## macOS — Build and Run

These instructions describe what is required to compile and run the game on macOS when using the DesktopGL build (macOS/Unix environments). The MonoGame content pipeline (`mgcb`) requires a 64-bit Wine prefix for effect (shader) compilation; follow the steps below exactly.

Prerequisites
- Install .NET SDK 9 (or the SDK matching the repository `TargetFramework`).
- Install Homebrew (recommended) for easy package installation.
- Install Wine (64-bit) and `winetricks`.

Quick install and setup

1) Install Homebrew (if you don't have it):

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

2) Install Wine and winetricks:

```bash
brew install --cask wine-stable
brew install winetricks
```

3) Create a dedicated 64-bit Wine prefix for the MonoGame effect compiler and initialize it:

```bash
export WINEARCH=win64
export WINEPREFIX="$HOME/.wine-mgfxc"
wineboot --init
```

4) Install the Windows components required by MonoGame's MGFXC (shader/effect compiler):

```bash
WINEARCH=win64 WINEPREFIX="$HOME/.wine-mgfxc" winetricks -q d3dcompiler_47 dotnet8
```

5) Point the MonoGame MGFXC helper to the Wine prefix by setting `MGFXC_WINE_PATH` in your environment. For a single terminal session:

```bash
export MGFXC_WINE_PATH="$HOME/.wine-mgfxc"
```

To make this persistent, add the line to your shell profile (for example `~/.zshrc` or `~/.bash_profile`):

```bash
echo 'export MGFXC_WINE_PATH="$HOME/.wine-mgfxc"' >> ~/.zshrc
```

6) Build and run the game (example):

```bash
cd src/TriloGame.Game
export MGFXC_WINE_PATH="$HOME/.wine-mgfxc"
dotnet build
dotnet run
```

Notes & troubleshooting
- If you installed a different Wine binary (for example `/Applications/Wine Stable.app`), ensure the Wine prefix you initialize and point `MGFXC_WINE_PATH` to matches that installation. The prefix path can be any directory; just set `MGFXC_WINE_PATH` accordingly.
- If `dotnet build` fails during content build with messages about `MGFXC` or `effect compiler requires a valid Wine installation`, confirm `WINEARCH` is `win64` and that `d3dcompiler_47` and `dotnet8` are installed in the prefix.
- You can test that `mgcb` is available by running `dotnet mgcb --help`.
- The MonoGame content builder may auto-restore the `mgcb` tool; if it doesn't, install it globally with `dotnet tool install --global dotnet-mgcb` or run `dotnet tool restore` in affected projects.

