This game is an implementation of a game idea I've had for a while. If you are reading this, the game is in progress, and further documentation is on the way!

The game is deployed to github pages here: https://qu-1ll.github.io/TriloGame/ Feel free to click around!

Future versions will not be released to the browser, as we have moved the program to a C# build.

## Build and Run

Install the .NET 9 SDK, open a terminal in the repository root, and use either:

```powershell
launch
```

or:

```powershell
start
```

These repo-root commands restore, build, and run `src/TriloGame.Game/TriloGame.Game.csproj`.

You can also call the wrappers directly:

```powershell
.\dotnet-launch.cmd
.\dotnet-start.cmd
```

`dotnet launch` is not a reliable repo-local command by itself. The `dotnet` CLI only resolves custom verbs when `dotnet-launch` is installed in a place the `dotnet` host can discover, which is outside what a normal checked-in batch file can guarantee.

The direct fallback is:

```powershell
dotnet restore src/TriloGame.Game/TriloGame.Game.csproj
dotnet build src/TriloGame.Game/TriloGame.Game.csproj -c Debug
dotnet run --project src/TriloGame.Game/TriloGame.Game.csproj -c Debug
```

## Runtime Automation API

The live MonoGame host now exposes an in-process play/test API through
`src/TriloGame.Game/Runtime/Automation/GamePlayApi.cs`.

That API is intended for:

- scripted scenario setup
- runtime inspection
- automation-oriented tests
- future external tooling adapters

See [docs/playtest-api.md](docs/playtest-api.md) for the current surface area.

## UI Rendering

For the current C# / MonoGame build, all screen-space UI should render through Gum,
including UI text.

That means:

- panels, frames, cards, and overlays use Gum-backed rendering
- buttons, toggles, and other controls use Gum-backed rendering
- fitted and wrapped screen UI text should go through the Gum-backed text helpers
- prefer fixed integer Gum `FontSize` values over fractional `FontScale` for routine UI text sizing

Raw `SpriteBatch.DrawString` should not be used for new screen-space UI text.
The only acceptable exception is world-space debug text that belongs to the game world
overlay rather than the UI layer.

## Release Packaging

To publish the self-contained Windows build and push only those compiled files to the `dist` branch, run:

```powershell
.\push-dist.cmd
```

That command publishes `src/TriloGame.Game/TriloGame.Game.csproj` to `artifacts/publish/win-x64` and pushes the published output to `origin/dist`.

When you publish a GitHub Release, `.github/workflows/release.yml` now builds the same `win-x64` package, zips it, and uploads `The-Scuttlers-win-x64.zip` to the release so players can download it and run the included `.exe` without cloning the repo.

You can read the design and road map below, or check out the latest release notes for more information!

# Game Mechanics and Design Plans

## General Gameplay

The Scuttlers is an attempt at combining the colony simulator, tower defense, and roguelike game genres into one game. We plan to do this with an arsenal of intricate game mechanics and lots of unique design inspired by many other games in those genres.

In this game you will build a city from the ground up while defending your city with your queen inside it from increasingly difficult waves of enemies. The game will challenge players to optimize their base for production and defense simultaneously. Each "run" should only take about 1 to 2 hours maximum before the player defends against a final boss wave of enemies. But don't worry, there will of course be an endless mode for players to continue the insanity!

While players develop their city and fight wave after wave of enemies, they will also be completing quests and filling in a skill tree. As players defeat rounds of enemies they will draft and place different branches onto their skill tree for the run. Completing quests will then allow players to unlock the skills on the tree as they are putting the tree together, allowing for a unique and self-generated progression every run!

## The Road Map

In the game's current state, we only have a few buildings and some pretty basic visuals to go along with it. Since this game is meant to stand out based on game design, we will be fleshing that aspect of the game out first and foremost. Currently, we are working on the first few chunks of the skill tree. We intend for the "skill lines" to be small constellations of 5-10 skills that can be combined with other skill lines in each run's patchworked skill tree. We will also be working on a similar semi-linear tree for quest lines as well.

As we decide on which skill trees we want to add, how they should work, and which ones we plan to add first, we will be adding them to a proper road map below. Until then just let your imagine wander in how cool this game will be one day!
