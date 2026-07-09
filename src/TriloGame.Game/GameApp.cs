using Gum.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameGum;
using RenderingLibrary.Graphics;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Runtime.Automation;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;
using TriloGame.Game.UI.Debug;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Hud;
using TriloGame.Game.UI.Input;
using TriloGame.Game.UI.MainMenu;
using TriloGame.Game.UI.Overlays;
using TriloGame.Game.UI.Menu;
using TriloGame.Game.UI.Research;
using TriloGame.Game.UI.Selection;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game;

public sealed partial class GameApp : Microsoft.Xna.Framework.Game, IGamePlayHost
{
    private enum AppScreen
    {
        MainMenu,
        Gameplay
    }
    private GumService GumUi => GumService.Default;
    private readonly GraphicsDeviceManager _graphics;
    private readonly AudioService _audio = new();
    private readonly MusicService _music = new();
    private readonly SessionAudioBridge _sessionAudioBridge;
    private readonly SessionScreenShakeBridge _sessionScreenShakeBridge;
    private readonly SessionParticleBridge _sessionParticleBridge;
    private readonly FocusAudioSystem _focusAudioSystem;
    private readonly InputController _input = new();
    private readonly DoubleClickTracker _manualMoveDoubleClick = new();
    private readonly CameraController _camera = new();
    private readonly WorldSpriteEffectSystem _worldSpriteEffects = new();
    private readonly WorldSceneRenderer _worldSceneRenderer = new();
    private readonly MainMenuOverlayRenderer _mainMenuOverlayRenderer = new();
    private readonly MenuController _menu = new();
    private readonly SettingsMenuController _settingsMenu = new();
    private readonly SettingsMenuRenderer _settingsMenuRenderer = new();
    private readonly GameOverOverlayRenderer _gameOverOverlayRenderer = new();
    private readonly ResearchDraftController _researchDraft = new();
    private readonly TrilodexController _trilodex = new();
    private readonly ResourceHudRenderer _resourceHud = new();
    private readonly GameSessionBootstrapper _bootstrapper = new();
    private readonly GameSimulationClockSystem _simulationClock = new();
    private readonly GameOverStateSystem _gameOverState = new();
    private readonly ResearchDraftSystem _researchDraftSystem = new();
    private readonly ResourceStockpileSystem _resourceStockpileSystem = new();
    private readonly DebugMenuController _debugMenu = new();
    private readonly DebugSpawnAmountControl _debugEnemySpawnControl = new("Enemy");
    private readonly DebugSpawnAmountControl _debugTrilobiteSpawnControl = new("Trilobite");
    private readonly DebugToggleControls _debugToggleControls;
    private readonly Func<bool> _stopSimulationAfterTick;
    private GameSession _session = new();
    private readonly HashSet<Trilobite> _selectedTrilobites = [];
    private readonly List<Trilobite> _selectionResultBuffer = [];
    private Trilobite[] _pendingManualMoveTargets = [];

    private SpriteBatch _spriteBatch = null!;
    private RenderingContext _rendering = null!;
    private GumUiRenderer _gumUiRenderer = null!;
    private GumRenderTargetViewport _trilodexCatalogViewport = null!;
    private object? _selectedObject;
    private string? _activeBfsDebugField;
    private bool _debugMenuOpen;
    private bool _mainMenuWorldGenerationDropdownOpen;
    private bool _debugMapOverlayVisible;
    private bool _debugMetricsOverlayVisible;
    private bool _infiniteDraft;
    private bool _resumeSimulationAfterClosingResearchDraft;
    private bool _resumeSimulationAfterClosingTrilodex;
    private bool _mainMenuOpen;
    private bool _showRoleLabels;
    private bool _showFullMapVisibility = false;
    private bool _debugAntHolePlacementMode;
    private bool _cameraPanDragActive;
    private bool _selectionDragActive;
    private bool _buildingPlacementDragActive;
    private GridPoint _buildingPlacementDragStart;
    private double _uiClockMs;
    private WorldGenerationMethod _worldGenerationMethod = WorldGenerationMethod.Version0;
    private AppScreen _appScreen = AppScreen.MainMenu;
    private BuildingPlacementCursor? _buildingPlacementCursor;
    private Rectangle? _selectionBoxBounds;
    private RoleRadialMenuState? _roleRadialMenu;
    private bool _settingsMenuOpen => _settingsMenu.IsOpen;

    public GameApp()
    {
        _graphics = new GraphicsDeviceManager(this);
        _sessionAudioBridge = new SessionAudioBridge(_audio);
        _sessionScreenShakeBridge = new SessionScreenShakeBridge(_camera);
        _sessionParticleBridge = new SessionParticleBridge(EmitDeathMist);
        _focusAudioSystem = new FocusAudioSystem(_audio);
        _debugToggleControls = new DebugToggleControls(
            value => _showRoleLabels = value,
            value => _session.Runtime.DisableEnemySpawns = value,
            value => _session.Runtime.NoCostBuildPlacement = value,
            value => _infiniteDraft = value,
            SetFullMapVisibility,
            PlayUiSelectSound);
        _roundManager.RoundStarted += HandleRoundStarted;
        _roundManager.RoundEnded += HandleRoundEnded;
        _roundManager.DraftRequested += HandleRoundDraftRequested;
        _stopSimulationAfterTick = StopSimulationAfterTick;
        PlayApi = new GamePlayApi(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => HandleViewportResize();
    }

    public GameSession Session => _session;

    public GamePlayApi PlayApi { get; }

    public bool BuildMode => _buildingPlacementCursor is not null;

    public MenuController Menu => _menu;

    private bool _gamePaused
    {
        get => _simulationClock.IsPaused;
        set => _simulationClock.IsPaused = value;
    }

    private bool _isGameOver
    {
        get => _gameOverState.IsGameOver;
        set => _gameOverState.IsGameOver = value;
    }

    private double _tickSpeedMs
    {
        get => _simulationClock.TickSpeedMs;
        set => _simulationClock.TickSpeedMs = value;
    }

    private double _tickAccumulatorMs
    {
        get => _simulationClock.TickAccumulatorMs;
        set => _simulationClock.TickAccumulatorMs = value;
    }

    public void PlayUiSelectSound()
    {
        _audio.Play(GameAudioCue.UiSelect);
    }

    public string BuildCrashDiagnostics()
    {
        var cave = _session.Cave;
        return GameAppCrashDiagnosticsBuilder.Build(
            new GameAppCrashDiagnosticsSnapshot(
                _appScreen.ToString(),
                _gamePaused,
                _isGameOver,
                _mainMenuOpen,
                _debugMenuOpen,
                _settingsMenuOpen,
                _researchDraft.IsOpen,
                _trilodex.IsOpen,
                BuildMode,
                _debugAntHolePlacementMode,
                _tickSpeedMs,
                _tickAccumulatorMs,
                _activeBfsDebugField ?? "none",
                _session.Runtime.DisableEnemySpawns,
                FormatTickProfile(_session.Runtime.TickProfiler.Last, "last"),
                FormatTickProfile(_session.Runtime.TickProfiler.Average, "avg"),
                Window.ClientBounds.Size,
                FormatVector(_camera.CameraOrigin),
                _camera.CurrentScale,
                FormatVector(_camera.ViewCenter),
                _input.MousePoint,
                _input.MouseDelta,
                _input.Dragging,
                _input.DragStartPoint,
                _cameraPanDragActive,
                _selectionDragActive,
                FormatPressedKeys(),
                _menu.PanelOpen,
                _menu.ActiveTab,
                _menu.AssignmentFilter,
                _researchDraftSystem.PendingDraft?.Branches.Count ?? 0,
                DescribeSelectedObject(),
                FormatSelectedTrilobites(),
                DescribeFloatingBuilding(),
                DescribeRoleRadialMenu(),
                DescribeSelectionBox(),
                _miningTileSelection.TileKeys,
                _session.TickCount,
                _session.Danger,
                _session.Runtime.PeekNextDebugEnemyId(),
                FormatResources(),
                cave is not null,
                cave?.RevealedTiles.Count ?? 0,
                cave?.ReachableTiles.Count ?? 0,
                cave?.Trilobites.Count ?? 0,
                cave?.Enemies.Count ?? 0,
                cave?.Buildings.Count ?? 0,
                cave?.GetQueenBuilding() is { } queen
                    ? $"{queen.Location?.ToString() ?? "none"} HP {queen.Health}/{queen.MaxHealth}"
                    : "missing",
                cave is null ? "none" : FormatBuildingSummary(cave),
                cave is null ? "none" : FormatTrilobiteSummary(cave),
                cave is null ? "none" : FormatEnemySummary(cave)));
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1440;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.ApplyChanges();
        GumUi.Initialize(this, DefaultVisualsVersion.V2);
        MonoGameAndGum.Renderables.ShapeRenderer.Self.Initialize(GraphicsDevice, Content);
        _gumUiRenderer = new GumUiRenderer();
        _trilodexCatalogViewport = new GumRenderTargetViewport(GraphicsDevice, _gumUiRenderer.Root);
        _camera.SetViewport(Window.ClientBounds.Width, Window.ClientBounds.Height);
        StartNewGame();
        ReturnToMainMenu();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        var whitePixel = new Texture2D(GraphicsDevice, 1, 1);
        whitePixel.SetData([Color.White]);

        var sprites = new SpriteFactory();
        RegisterTexture(sprites, "empty", "Textures/EmptyTile");
        RegisterTexture(sprites, "wall", "Textures/CaveWall");
        RegisterTexture(sprites, "CaveBackground2", "Textures/CaveBackground2");
        RegisterTexture(sprites, "wall_0", "Textures/wall_0");
        RegisterTexture(sprites, "wall_1", "Textures/wall_1");
        RegisterTexture(sprites, "wall_2", "Textures/wall_2");
        RegisterTexture(sprites, "wall_2_bent", "Textures/wall_2_bent");
        RegisterTexture(sprites, "wall_3", "Textures/wall_3");
        RegisterTexture(sprites, "wall_4", "Textures/wall_4");
        // Register every mineable tile base so the world renderer can draw legacy ore deposits by name.
        RegisterTexture(sprites, OreType.SANDSTONE.Name, "Textures/Sandstone");
        RegisterTexture(sprites, OreType.MAGNETITE.Name, "Textures/Magnetite");
        RegisterTexture(sprites, OreType.MALACHITE.Name, "Textures/Malachite");
        RegisterTexture(sprites, OreType.PEROTENE.Name, "Textures/Perotene");
        RegisterTexture(sprites, OreType.ILMENITE.Name, "Textures/Ilmenite");
        RegisterTexture(sprites, OreType.COCHINIUM.Name, "Textures/Cochinium");
        RegisterTexture(sprites, OreType.LUMENITE.Name, "Textures/Lumenite");
        RegisterTexture(sprites, OreType.CHITINSTONE.Name, "Textures/Chitinstone");
        RegisterTexture(sprites, OreType.MYCOCORE.Name, "Textures/Mycocore");
        RegisterTexture(sprites, ItemCatalog.Algae.Name, "Textures/Algae");
        _worldSpriteEffects.RegisterAlphaPulse(OreType.LUMENITE.Name, new AlphaPulseEffect(0.38f, 1f, 2.1f));
        RegisterTexture(sprites, "Trilobite", "Textures/Trilobite");
        RegisterTexture(sprites, "Enemy", "Textures/Enemy");
        RegisterTexture(sprites, "AntHole", "Textures/AntHole");
        RegisterTexture(sprites, "CaveCrystal", "Textures/CaveCrystalClean");
        RegisterTexture(sprites, "Scaffold", "Textures/Scaffold");
        RegisterTexture(sprites, "Queen", "Textures/Queen");
        RegisterTexture(sprites, "AlgaeFarm", "Textures/AlgaeFarm");
        RegisterTexture(sprites, "Garage", "Textures/Garage");
        RegisterTexture(sprites, "Silo", "Textures/Silo");
        RegisterTexture(sprites, "SoilTile_0", "Textures/SoilTile_0");
        RegisterTexture(sprites, "SoilTile_Algae_1", "Textures/SoilTile_Algae_1");
        RegisterTexture(sprites, "SoilTile_Algae_2", "Textures/SoilTile_Algae_2");
        RegisterTexture(sprites, "SoilTile_Algae_3", "Textures/SoilTile_Algae_3");
        RegisterTexture(sprites, "Storage", "Textures/Storage");
        RegisterTexture(sprites, "Smith", "Textures/Smith");
        RegisterTexture(sprites, "MiningPost", "Textures/MiningPost");
        RegisterTexture(sprites, "Radar", "Textures/Radar");
        RegisterTexture(sprites, "Barracks", "Textures/Barracks");
        RegisterTexture(sprites, "Turret", "Textures/Turret");
        RegisterTexture(sprites, "Selected", "Textures/Selected");
        RegisterTexture(sprites, "SelectedEdge", "Textures/SelectedEdge");
        RegisterTexture(sprites, "Path", "Textures/Path");
        RegisterTexture(sprites, "BackArrow", "Textures/BackArrow");
        RegisterTexture(sprites, "TreeBackground", "Textures/dark-rock-wall-seamless-texture-free-105");

        _rendering = new RenderingContext
        {
            SpriteBatch = _spriteBatch,
            UiFont = Content.Load<SpriteFont>("Fonts/UiFont"),
            SmallFont = Content.Load<SpriteFont>("Fonts/SmallFont"),
            DebugFont = Content.Load<SpriteFont>("Fonts/DebugFont"),
            WhitePixel = whitePixel,
            Sprites = sprites,
            Camera = _camera
        };
        InitializeWorldParticles();

        GameAudioContentCatalog.RegisterCues(Content, _audio);
        GameAudioContentCatalog.RegisterTracks(Content, _music);
    }

    protected override void Update(GameTime gameTime)
    {
        _input.BeginFrame();
        _uiClockMs += gameTime.ElapsedGameTime.TotalMilliseconds;
        _camera.Update(gameTime);
        _worldSpriteEffects.Update(gameTime);
        UpdateMusic(gameTime);
        UpdateWorldParticles(gameTime);
        ExpirePendingManualMove();
        SyncSelectionIfRemoved();

        if (_mainMenuOpen)
        {
            _researchDraft.UpdatePointer(_input.MousePoint);
            _trilodex.UpdatePointer(_input.MousePoint);
            if (_trilodex.IsOpen)
            {
                HandleTrilodexMenuInput();
            }
            else if (_researchDraft.IsOpen)
            {
                HandleResearchDraftMenuInput();
            }
            else
            {
                HandleMainMenuInput();
            }

            ResetPassiveAudio();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (!_menu.IsRenamingSelectedTrilobite && _input.KeyPressed(Keys.OemTilde))
        {
            ToggleDebugMenu();
        }

        if (!_menu.IsRenamingSelectedTrilobite && _input.KeyPressed(Keys.F3))
        {
            _debugMetricsOverlayVisible = !_debugMetricsOverlayVisible;
            PlayUiSelectSound();
        }

        if (HasLostQueen())
        {
            TriggerGameOver();
        }

        if (_isGameOver)
        {
            HandleGameOverInput();
            ResetPassiveAudio();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        _researchDraft.UpdatePointer(_input.MousePoint);
        _trilodex.UpdatePointer(_input.MousePoint);
        if (_trilodex.IsOpen)
        {
            HandleTrilodexMenuInput();
            ResetPassiveAudio();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (_researchDraft.IsOpen)
        {
            HandleResearchDraftMenuInput();
            ResetPassiveAudio();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (_debugMenuOpen)
        {
            HandleDebugMenuInput();
            AdvanceSimulation(gameTime);
            ResetPassiveAudio();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        _menu.UpdateHover(_input.MousePoint, Window.ClientBounds.Size, _session);

        var researchHandled = _input.LeftReleased && HandleResearchDraftButtonClick(_input.MousePoint);
        var settingsHandled = _input.LeftReleased && !researchHandled && HandleSettingsClick(_input.MousePoint);
        var roundWidgetHandled = _input.LeftReleased && !researchHandled && !settingsHandled && HandleRoundDebugWidgetClick(_input.MousePoint);
        if (researchHandled || settingsHandled || roundWidgetHandled)
        {
            ResetPointerInteractionState();
        }

        if (_input.WheelDelta != 0 && !_input.Dragging)
        {
            var wheelHandled = SettingsCoversPoint(_input.MousePoint);
            if (!wheelHandled)
            {
                wheelHandled = HandleMiningOrderMenuWheel(_input.MousePoint, System.Math.Clamp(-_input.WheelDelta, -90, 90));
            }

            if (!wheelHandled)
            {
                var scrollDelta = System.Math.Clamp(-_input.WheelDelta, -90, 90);
                wheelHandled = _menu.HandleWheel(_input.MousePoint, scrollDelta, Window.ClientBounds.Size, _session);
            }

            if (!wheelHandled)
            {
                if (_input.WheelDelta > 0)
                {
                    _camera.CurrentScale = MathF.Min(GameConstants.MaxScale, _camera.CurrentScale * (4f / 3f));
                }
                else
                {
                    _camera.CurrentScale = MathF.Max(GameConstants.MinScale, _camera.CurrentScale * 0.75f);
                }
            }
        }

        if (_input.LeftPressed)
        {
            _input.BeginDrag();
        }

        if (_input.LeftHeld)
        {
            _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.LeftHeld);
        }

        if (_input.MiddlePressed)
        {
            _input.BeginDrag();
            _cameraPanDragActive = CanStartCameraPanDrag(_input.MousePoint);
        }

        if (_input.MiddleHeld && _cameraPanDragActive)
        {
            _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.MiddleHeld);
            if (_input.Dragging)
            {
                _camera.PanByScreenDelta(_input.MouseDelta.X, _input.MouseDelta.Y);
            }
        }

        if (_input.MiddleReleased)
        {
            ResetPointerInteractionState();
        }

        if (_input.RightPressed)
        {
            if (_roleRadialMenu is null && !BuildMode)
            {
                _input.BeginDrag();
                _selectionDragAppend = ControlHeld();
                _selectionDragActive = ShouldStartSelectionDrag(_input.MousePoint);
                _selectionBoxBounds = _selectionDragActive
                    ? CreateScreenRectangle(_input.MousePoint, _input.MousePoint)
                    : null;
            }
        }

        if (_input.RightHeld)
        {
            if (_roleRadialMenu is null && !BuildMode)
            {
                _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.RightHeld);
                if (_selectionDragActive && _input.Dragging)
                {
                    _selectionBoxBounds = CreateScreenRectangle(_input.DragStartPoint, _input.MousePoint);
                }
            }
        }

        if (_input.RightReleased)
        {
            if (BuildMode)
            {
                CancelBuildingPlacement();
                ResetPointerInteractionState();
            }
            else if (_selectionDragActive && _input.Dragging)
            {
                FinalizeSelectionBox();
                ResetPointerInteractionState();
            }
            else if (!_input.Dragging &&
                     !_menu.CoversScreenPoint(_input.MousePoint, Window.ClientBounds.Size) &&
                     !SettingsCoversPoint(_input.MousePoint) &&
                     !ResearchDraftCoversPoint(_input.MousePoint) &&
                     !TrilodexCoversPoint(_input.MousePoint))
            {
                HandleWorldRightClick(_input.MousePoint);
                ResetPointerInteractionState();
            }
            else
            {
                ResetPointerInteractionState();
            }
        }

        if (BuildMode && _input.LeftPressed && !GameplayUiCoversPoint(_input.MousePoint))
        {
            BeginBuildingPlacementDrag(_input.MousePoint);
        }

        if (_buildingPlacementDragActive && _input.LeftHeld)
        {
            _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.LeftHeld);
        }

        if (_input.LeftReleased && _buildingPlacementDragActive)
        {
            HandleBuildingPlacementRelease(_input.MousePoint);
            ResetPointerInteractionState();
        }
        else if (_input.LeftReleased && !settingsHandled && !roundWidgetHandled)
        {
            if (TryHandleMiningOrderMenuClick(_input.MousePoint))
            {
                ResetPointerInteractionState();
            }
            else if (TryHandleRoleRadialClick(_input.MousePoint))
            {
                ResetPointerInteractionState();
            }
            else if (!_input.Dragging &&
                     !SettingsCoversPoint(_input.MousePoint) &&
                     !ResearchDraftCoversPoint(_input.MousePoint) &&
                     !TrilodexCoversPoint(_input.MousePoint) &&
                     !HandleMenuClick(_input.MousePoint))
            {
                if (!BuildMode)
                {
                    HandleWorldClick(_input.MousePoint);
                }

                ResetPointerInteractionState();
            }
            else
            {
                ResetPointerInteractionState();
            }
        }

        HandleKeyboard(gameTime);
        if (HasLostQueen())
        {
            TriggerGameOver();
            base.Update(gameTime);
            return;
        }

        AdvanceSimulation(gameTime);
        SyncGameplayAudio();
        GumUi.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_session.Cave is not null)
        {
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            _worldSceneRenderer.DrawParallaxBackground(_rendering, Window.ClientBounds);
            _spriteBatch.End();
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_session.Cave is not null)
        {
            _worldSceneRenderer.DrawWorldLayer(_rendering, _session, _worldSpriteEffects, Window.ClientBounds.Size, _showFullMapVisibility);
            DrawWorldParticles();
            DrawSelection();
            DrawFloatingPreview();
            DrawDebugOverlay(_session.Cave);
        }

        _spriteBatch.End();
        _gumUiRenderer.BeginFrame(Window.ClientBounds.Size);
        if (_session.Cave is not null)
        {
            DrawRoleLabels(_session.Cave);
            DrawMiningTileSelection();
            DrawSelectionBox();
        }

        if (_mainMenuOpen)
        {
            _mainMenuOverlayRenderer.Draw(_gumUiRenderer, Window.ClientBounds.Size, _input.MousePoint);
            if (_debugMenuOpen)
            {
                DrawMainMenuDebugOverlay();
            }

            DrawSettingsMenu();
            if (_researchDraft.IsOpen)
            {
                _researchDraft.Draw(Window.ClientBounds.Size, _session, _researchDraftSystem, _gumUiRenderer, GetTreeBackgroundTexture(), _uiClockMs);
            }
            _trilodex.Draw(Window.ClientBounds.Size, _session, _gumUiRenderer, _trilodexCatalogViewport, GetTreeBackgroundTexture(), _uiClockMs);
        }
        else if (_isGameOver)
        {
            _gameOverOverlayRenderer.Draw(_gumUiRenderer, Window.ClientBounds.Size, _input.MousePoint);
        }
        else
        {
            DrawResourceHud();
            _menu.Draw(_rendering, Window.ClientBounds.Size, _session, _gumUiRenderer);
            DrawSettingsMenu();
            DrawRoleRadialMenu();
            DrawMiningOrderMenu();
            DrawMiningTileHoverLabel();
            DrawFocusHint();
            DrawRoundDebugWidget();
            _researchDraft.Draw(Window.ClientBounds.Size, _session, _researchDraftSystem, _gumUiRenderer, GetTreeBackgroundTexture(), _uiClockMs);
            _trilodex.Draw(Window.ClientBounds.Size, _session, _gumUiRenderer, _trilodexCatalogViewport, GetTreeBackgroundTexture(), _uiClockMs);

            if (_debugMenuOpen)
            {
                DrawDebugMenuOverlay();
            }
        }

        _gumUiRenderer.EndFrame();
        GumUi.Draw();
        if (_debugMetricsOverlayVisible && !_mainMenuOpen)
        {
            DrawDebugMetricsOverlay();
        }

        base.Draw(gameTime);
    }

    public void BeginBuildingPlacement(Factory factory)
    {
        ClearPendingManualMove();
        _debugAntHolePlacementMode = false;
        _buildingPlacementCursor = new BuildingPlacementCursor(factory, _session);
    }

    private void CancelBuildingPlacement()
    {
        _buildingPlacementCursor = null;
        _buildingPlacementDragActive = false;
        _roleRadialMenu = null;
        _miningOrderMenu.Close();
    }

    private void BeginBuildingPlacementDrag(Point point)
    {
        if (!TryGetBuildingPlacementLocation(point, out var location))
        {
            return;
        }

        _input.BeginDrag();
        _buildingPlacementDragActive = true;
        _buildingPlacementDragStart = location;
    }

    private void HandleBuildingPlacementRelease(Point point)
    {
        if (GameplayUiCoversPoint(point) || !TryGetBuildingPlacementLocation(point, out var endLocation))
        {
            return;
        }

        var locations = BuildPlacementPreviewResolver.ResolveLocations(
            _buildingPlacementCursor!.TargetBuilding,
            endLocation,
            _buildingPlacementDragStart);
        var placedAny = false;
        foreach (var location in locations)
        {
            placedAny |= TryPlaceBuildingAt(location);
        }

        if (placedAny)
        {
            _audio.Play(GameAudioCue.BuildingPlace);
            _buildingPlacementCursor!.RefreshAfterSuccessfulPlacement();
        }
    }

    private bool GameplayUiCoversPoint(Point point)
    {
        return _menu.CoversScreenPoint(point, Window.ClientBounds.Size) ||
               SettingsCoversPoint(point) ||
               ResearchDraftCoversPoint(point) ||
               TrilodexCoversPoint(point);
    }

    private void ResetPointerInteractionState()
    {
        _cameraPanDragActive = false;
        _selectionDragActive = false;
        _buildingPlacementDragActive = false;
        _selectionBoxBounds = null;
        _input.EndDrag();
    }

    public void CleanActive(bool closeMenu = false)
    {
        ClearPendingManualMove();
        _activeBfsDebugField = null;
        _debugMapOverlayVisible = false;
        _buildingPlacementCursor = null;
        _debugAntHolePlacementMode = false;
        ResetPointerInteractionState();
        _roleRadialMenu = null;
        _selectedObject = null;
        _selectedTrilobites.Clear();
        ClearMiningTileSelection();
        _menu.SetSelectedObject(null);
        if (closeMenu)
        {
            _menu.ClosePanel();
        }
    }

    public void RestartGame()
    {
        StartGameplaySession();
    }

    private void StartGameplaySession()
    {
        _sessionAudioBridge.Detach();
        _sessionScreenShakeBridge.Detach();
        _sessionParticleBridge.Detach();
        ClearWorldParticles();
        CleanActive(true);
        _tickAccumulatorMs = 0d;
        StartNewGame();
    }

    private void StartNewGame()
    {
        _appScreen = AppScreen.Gameplay;
        var bootstrap = _bootstrapper.CreateNewGame(_worldGenerationMethod);
        _session = bootstrap.Session;
        _session.Runtime.DisableEnemySpawns = false;
        _sessionAudioBridge.Attach(_session);
        _sessionScreenShakeBridge.Attach(_session);
        _sessionParticleBridge.Attach(_session);
        var spawnX = bootstrap.QueenLocation.X;
        var spawnY = bootstrap.QueenLocation.Y;

        _camera.CurrentScale = GameConstants.DefaultCameraScale;
        _camera.ClearShake();
        _camera.SetOrigin(new Vector2((spawnX * TileConstants.TileSize) + TileConstants.TileSize, (spawnY * TileConstants.TileSize) + TileConstants.TileSize));
        _activeBfsDebugField = null;
        _selectedObject = null;
        _buildingPlacementCursor = null;
        ResetPointerInteractionState();
        _roleRadialMenu = null;
        _selectedTrilobites.Clear();
        ClearMiningTileSelection();
        _mainMenuOpen = false;
        _debugMenuOpen = false;
        _mainMenuWorldGenerationDropdownOpen = false;
        _settingsMenu.Reset();
        _showRoleLabels = false;
        _infiniteDraft = false;
        _showFullMapVisibility = false;
        _simulationClock.ResetToDefaults(paused: false, tickSpeedMs: GameConstants.TickSpeedFast);
        _gameOverState.Reset();
        ResetRoundSystems();
        _researchDraftSystem.Reset();
        _researchDraft.Reset();
        _trilodex.Reset();
        _resumeSimulationAfterClosingResearchDraft = false;
        _resumeSimulationAfterClosingTrilodex = false;
        _uiClockMs = 0d;
        ClearPendingManualMove();
        _menu.ResetState();
        ClearWorldParticles();
        StartGameplayMusic();
    }

    private void ReturnToMainMenu()
    {
        ClearWorldParticles();
        CleanActive(true);
        _mainMenuOpen = true;
        CloseSettingsMenu();
        StopGameplayMusic();
        _camera.ClearShake();
        _appScreen = AppScreen.MainMenu;
        _gamePaused = true;
        _isGameOver = false;
        _debugMenuOpen = false;
        _mainMenuWorldGenerationDropdownOpen = false;
        _showFullMapVisibility = false;
        _researchDraftSystem.Reset();
        _researchDraft.Reset();
        _trilodex.Reset();
        _resumeSimulationAfterClosingResearchDraft = false;
        _resumeSimulationAfterClosingTrilodex = false;
        _tickAccumulatorMs = 0d;
        ResetPointerInteractionState();
    }

    private void TriggerGameOver()
    {
        if (!_gameOverState.TryTrigger(_session))
        {
            return;
        }

        CloseSettingsMenu();
        ForceCloseResearchDraftMenu();
        ForceCloseTrilodexMenu();
        _gamePaused = true;
        _debugMenuOpen = false;
        ResetPassiveAudio();
        ResetPointerInteractionState();
        _roleRadialMenu = null;
        _tickAccumulatorMs = 0d;
        ClearPendingManualMove();
        CleanActive(true);
    }

    private void HandleMainMenuInput()
    {
        if (_input.KeyPressed(Keys.OemTilde))
        {
            ToggleDebugMenu();
            return;
        }

        if (_debugMenuOpen)
        {
            HandleMainMenuDebugMenuInput();
            return;
        }

        if (_input.KeyPressed(Keys.Escape) &&
            _settingsMenu.HandleEscape() == SettingsMenuInteractionOutcome.RequestedClose)
        {
            PlayUiSelectSound();
            CloseSettingsMenu();
            return;
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        var viewport = Window.ClientBounds.Size;
        if (_settingsMenuOpen)
        {
            HandleSettingsPanelClick(_input.MousePoint, allowQuitToMainMenu: false);
            return;
        }

        switch (MainMenuOverlay.Build(viewport).HitTest(_input.MousePoint))
        {
            case MainMenuOverlayAction.StartGame:
                PlayUiSelectSound();
                RestartGame();
                return;
            case MainMenuOverlayAction.OpenSettings:
                PlayUiSelectSound();
                OpenSettingsMenu(pauseSimulationIfNeeded: false);
                return;
            case MainMenuOverlayAction.OpenTrilodex:
                PlayUiSelectSound();
                OpenTrilodexMenu(pauseSimulationIfNeeded: false);
                return;
            case MainMenuOverlayAction.QuitGame:
                PlayUiSelectSound();
                Exit();
                return;
        }
    }

    // Route main-menu debug clicks through the same panel-first flow as the in-session debug overlay.
    private void HandleMainMenuDebugMenuInput()
    {
        if (_input.KeyPressed(Keys.Escape))
        {
            _debugMenuOpen = false;
            _mainMenuWorldGenerationDropdownOpen = false;
            return;
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        var layout = MainMenuDebugLayout.Build(
            Window.ClientBounds.Size,
            WorldGenerationMethods.SelectablePatterns.Count,
            _mainMenuWorldGenerationDropdownOpen);
        if (layout.DropdownBounds.Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            _mainMenuWorldGenerationDropdownOpen = !_mainMenuWorldGenerationDropdownOpen;
            return;
        }

        if (_mainMenuWorldGenerationDropdownOpen && layout.DropdownOptionsBounds is { } optionBounds)
        {
            foreach (var option in BuildMainMenuWorldGenerationOptions(optionBounds, layout.RowGap))
            {
                if (!option.Bounds.Contains(_input.MousePoint))
                {
                    continue;
                }

                PlayUiSelectSound();
                _worldGenerationMethod = option.Method;
                _mainMenuWorldGenerationDropdownOpen = false;
                return;
            }
        }

        _mainMenuWorldGenerationDropdownOpen = false;
    }

    private void SetSelectedObject(object? selectedObject)
    {
        ClearPendingManualMove();
        ClearMiningTileSelection();
        _roleRadialMenu = null;
        ResetPointerInteractionState();
        _selectedObject = selectedObject;
        _selectedTrilobites.Clear();
        if (selectedObject is Trilobite trilobite)
        {
            _selectedTrilobites.Add(trilobite);
        }

        _menu.SetSelectedObject(selectedObject);
        if (selectedObject is not null)
        {
            _menu.OpenPanel();
        }

        if (selectedObject is Trilobite)
        {
            _audio.Play(GameAudioCue.TrilobiteSelected);
        }
    }

    private void SetSelectedTrilobites(IEnumerable<Trilobite> trilobites, bool openMenuForSingle = false)
    {
        ClearPendingManualMove();
        ClearMiningTileSelection();
        _roleRadialMenu = null;
        ResetPointerInteractionState();
        _selectedTrilobites.Clear();
        Trilobite? firstSelected = null;
        var selectedCount = 0;
        foreach (var trilobite in trilobites)
        {
            if (trilobite.Cave is null || !_selectedTrilobites.Add(trilobite))
            {
                continue;
            }

            firstSelected ??= trilobite;
            selectedCount++;
        }

        if (openMenuForSingle && selectedCount == 1 && firstSelected is not null)
        {
            SetSelectedObject(firstSelected);
            return;
        }

        if (selectedCount > 0)
        {
            _audio.Play(GameAudioCue.TrilobiteSelected);
        }

        _selectedObject = null;
        _menu.SetSelectedObject(null);
    }

    private void CenterSelection(object selectedObject)
    {
        var menuOffset = _menu.GetOpenPanelWidth(Window.ClientBounds.Size) / 2f;
        var focusPoint = GetFocusWorldPosition(selectedObject);
        _camera.SetOrigin(focusPoint + new Vector2(menuOffset * (1f / _camera.CurrentScale), 0f));
    }

    private bool HasGumUiRenderer => _gumUiRenderer is not null;
}

public sealed partial class GameApp
{
    private bool HasLostQueen()
    {
        return _gameOverState.HasLostQueen(_session);
    }

    private void HandleGameOverInput()
    {
        if (!_input.LeftReleased)
        {
            return;
        }

        switch (GameOverOverlay.Build(Window.ClientBounds.Size).HitTest(_input.MousePoint))
        {
            case GameOverOverlayAction.PlayAgain:
                PlayUiSelectSound();
                RestartGame();
                return;
            case GameOverOverlayAction.ReturnToMainMenu:
                PlayUiSelectSound();
                ReturnToMainMenu();
                return;
        }
    }

    private void ToggleDebugMenu()
    {
        ClearPendingManualMove();
        _debugMenuOpen = !_debugMenuOpen;
        _mainMenuWorldGenerationDropdownOpen = false;
        if (_debugMenuOpen)
        {
            CloseSettingsMenu();
        }

        ResetPointerInteractionState();
    }

    private void HandleDebugMenuInput()
    {
        if (_debugEnemySpawnControl.HandleKeyboard(_input) ||
            _debugTrilobiteSpawnControl.HandleKeyboard(_input))
        {
            return;
        }

        if (_input.KeyPressed(Keys.Escape))
        {
            _debugMenuOpen = false;
            return;
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        var debugLayout = DebugMenuLayout.Build(Window.ClientBounds.Size);
        var spawnActionSlots = DebugMenuLayout.SplitRow(debugLayout.SpawnActionsRowBounds, 2, debugLayout.ButtonGap);
        var enemySpawnInteraction = _debugEnemySpawnControl.HandlePointerReleased(spawnActionSlots[0], _input.MousePoint);
        var trilobiteSpawnInteraction = _debugTrilobiteSpawnControl.HandlePointerReleased(spawnActionSlots[1], _input.MousePoint);
        if (enemySpawnInteraction != DebugSpawnControlInteraction.None ||
            trilobiteSpawnInteraction != DebugSpawnControlInteraction.None)
        {
            if (enemySpawnInteraction == DebugSpawnControlInteraction.SpawnRequested)
            {
                PlayUiSelectSound();
                InvokeDebugMenuAction(DebugMenuAction.SpawnEnemy, _debugEnemySpawnControl.Amount);
            }
            else if (trilobiteSpawnInteraction == DebugSpawnControlInteraction.SpawnRequested)
            {
                PlayUiSelectSound();
                InvokeDebugMenuAction(DebugMenuAction.SpawnTrilobite, _debugTrilobiteSpawnControl.Amount);
            }

            return;
        }

        if (_debugToggleControls.HandleClick(
                Window.ClientBounds.Size,
                _input.MousePoint,
                _debugMenuOpen,
                _showRoleLabels,
                _session.Runtime.DisableEnemySpawns,
                _session.Runtime.NoCostBuildPlacement,
                _infiniteDraft,
                _showFullMapVisibility))
        {
            return;
        }

        if (_debugMenu.TryGetButtonAction(Window.ClientBounds.Size, _input.MousePoint, BuildDebugMenuState(), out var action))
        {
            PlayUiSelectSound();
            InvokeDebugMenuAction(action);
            return;
        }

        HandleRoundDebugWidgetClick(_input.MousePoint);
    }

    private DebugMenuState BuildDebugMenuState()
    {
        return new DebugMenuState(_gamePaused, _tickSpeedMs, _activeBfsDebugField, _debugMapOverlayVisible, _debugAntHolePlacementMode);
    }

    private void SetFullMapVisibility(bool showFullMapVisibility)
    {
        _showFullMapVisibility = showFullMapVisibility;
    }

    private void InvokeDebugMenuAction(DebugMenuAction action, int amount = 1)
    {
        switch (action)
        {
            case DebugMenuAction.Close:
                _debugMenuOpen = false;
                return;
            case DebugMenuAction.TogglePause:
                TogglePauseState();
                return;
            case DebugMenuAction.SingleTick:
                RunSingleTick();
                return;
            case DebugMenuAction.SpeedSlow:
                _tickSpeedMs = GameConstants.TickSpeedSlow;
                return;
            case DebugMenuAction.SpeedNormal:
                _tickSpeedMs = GameConstants.TickSpeedNormal;
                return;
            case DebugMenuAction.SpeedFast:
                _tickSpeedMs = GameConstants.TickSpeedFast;
                return;
            case DebugMenuAction.SpeedFastest:
                _tickSpeedMs = GameConstants.TickSpeedFastest;
                return;
            case DebugMenuAction.ToggleMapOverlay:
                _debugMapOverlayVisible = !_debugMapOverlayVisible;
                return;
            case DebugMenuAction.ShowQueenField:
                ShowBfsFieldDebug("queen");
                return;
            case DebugMenuAction.ShowEnemyField:
                ShowBfsFieldDebug("enemy");
                return;
            case DebugMenuAction.ShowColonyField:
                ShowBfsFieldDebug("colony");
                return;
            case DebugMenuAction.ClearField:
                _activeBfsDebugField = null;
                _debugMapOverlayVisible = false;
                return;
            case DebugMenuAction.ToggleRoleLabels:
                _showRoleLabels = !_showRoleLabels;
                return;
            case DebugMenuAction.RestartGame:
                RestartGame();
                return;
            case DebugMenuAction.SpawnEnemy:
                SpawnDebugEnemies(amount);
                RefreshBfsFieldDebug();
                return;
            case DebugMenuAction.SpawnTrilobite:
                SpawnDebugTrilobites(amount);
                RefreshBfsFieldDebug();
                return;
            case DebugMenuAction.PlaceAntHole:
                _debugAntHolePlacementMode = true;
                _buildingPlacementCursor = null;
                _debugMenuOpen = false;
                CloseSettingsMenu();
                return;
            default:
                return;
        }
    }

    private void SyncSelectionIfRemoved()
    {
        _selectedTrilobites.RemoveWhere(trilobite => trilobite.Cave is null);

        if (_roleRadialMenu is not null)
        {
            var remainingTargets = _roleRadialMenu.Targets
                .Where(trilobite => trilobite.Cave is not null)
                .Distinct()
                .ToArray();
            _roleRadialMenu = remainingTargets.Length == 0
                ? null
                : _roleRadialMenu with { Targets = remainingTargets };
        }

        if (_selectedObject is Building building && building.Cave is null)
        {
            CleanActive();
        }
        else if (_selectedObject is Trilobite trilobite && trilobite.Cave is null)
        {
            if (_selectedTrilobites.Count > 0)
            {
                _selectedObject = null;
                _menu.SetSelectedObject(null);
            }
            else
            {
                CleanActive();
            }
        }
        else if (_selectedObject is Creature creature && creature.Cave is null)
        {
            CleanActive();
        }
    }

    private void HandleKeyboard(GameTime gameTime)
    {
        if (_menu.IsRenamingSelectedTrilobite)
        {
            _menu.HandleRenameInput(_input);
            return;
        }

        if (_input.KeyPressed(Keys.Enter))
        {
            RunSingleTick();
        }

        if (_input.KeyPressed(Keys.Space))
        {
            TogglePauseState();
        }

        if (_gamePaused)
        {
            if (_input.KeyPressed(Keys.D1)) ShowBfsFieldDebug("queen");
            if (_input.KeyPressed(Keys.D2)) ShowBfsFieldDebug("enemy");
            if (_input.KeyPressed(Keys.D3)) ShowBfsFieldDebug("colony");
        }
        else
        {
            if (_input.KeyPressed(Keys.D1)) _tickSpeedMs = GameConstants.TickSpeedSlow;
            if (_input.KeyPressed(Keys.D2)) _tickSpeedMs = GameConstants.TickSpeedNormal;
            if (_input.KeyPressed(Keys.D3)) _tickSpeedMs = GameConstants.TickSpeedFast;
            if (_input.KeyPressed(Keys.D4)) _tickSpeedMs = GameConstants.TickSpeedFastest;
        }

        if (_input.KeyPressed(Keys.P))
        {
            SpawnDebugEnemy();
            RefreshBfsFieldDebug();
        }

        if (_input.KeyPressed(Keys.Escape))
        {
            PlayUiSelectSound();
            if (BuildMode)
            {
                CancelBuildingPlacement();
                return;
            }

            if (_settingsMenu.HandleEscape() == SettingsMenuInteractionOutcome.RequestedClose)
            {
                CloseSettingsMenu();
            }
            else
            {
                OpenSettingsMenu();
                CleanActive(true);
            }
            return;
        }

        if (_input.KeyPressed(Keys.R) && _buildingPlacementCursor is not null)
        {
            _buildingPlacementCursor.RotateClockwise();
        }

        if (_input.KeyPressed(Keys.Tab))
        {
            PlayUiSelectSound();
            if (BuildMode)
            {
                CancelBuildingPlacement();
            }

            _menu.TogglePanel();
        }

        if (_input.Dragging)
        {
            return;
        }

        var focusTarget = GetSelectedFocusTarget();
        if (focusTarget is not null && _input.KeyHeld(Keys.F))
        {
            CenterSelection(focusTarget);
            return;
        }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var dx = 0f;
        var dy = 0f;
        if (_input.KeyHeld(Keys.W)) dy += GameConstants.KeyboardPanSpeedPixelsPerSecond * dt;
        if (_input.KeyHeld(Keys.S)) dy -= GameConstants.KeyboardPanSpeedPixelsPerSecond * dt;
        if (_input.KeyHeld(Keys.A)) dx += GameConstants.KeyboardPanSpeedPixelsPerSecond * dt;
        if (_input.KeyHeld(Keys.D)) dx -= GameConstants.KeyboardPanSpeedPixelsPerSecond * dt;
        if (dx != 0f || dy != 0f)
        {
            _camera.PanByScreenDelta(dx, dy);
        }
    }

    private void HandleWorldClick(Point point)
    {
        if (_debugAntHolePlacementMode)
        {
            HandleDebugAntHolePlacementClick(point);
            return;
        }

        if (BuildMode && TryGetBuildingPlacementLocation(point, out var buildLocation))
        {
            ClearPendingManualMove();
            ClearMiningTileSelection();
            if (TryPlaceBuildingAt(buildLocation))
            {
                _audio.Play(GameAudioCue.BuildingPlace);
                _buildingPlacementCursor!.RefreshAfterSuccessfulPlacement();
            }

            return;
        }

        if (TryHitCreature(point, out var creature))
        {
            ClearPendingManualMove();
            _roleRadialMenu = null;
            ClearMiningTileSelection();
            if (creature is Trilobite trilobite && ControlHeld())
            {
                var updatedSelection = _selectedTrilobites.Where(selected => selected.Cave is not null).ToList();
                if (updatedSelection.Remove(trilobite))
                {
                    if (updatedSelection.Count == 0)
                    {
                        CleanActive();
                    }
                    else if (updatedSelection.Count == 1)
                    {
                        SetSelectedObject(updatedSelection[0]);
                    }
                    else
                    {
                        SetSelectedTrilobites(updatedSelection, openMenuForSingle: false);
                    }
                }
                else
                {
                    updatedSelection.Add(trilobite);
                    if (updatedSelection.Count == 1)
                    {
                        SetSelectedObject(trilobite);
                    }
                    else
                    {
                        SetSelectedTrilobites(updatedSelection, openMenuForSingle: false);
                    }
                }
            }
            else
            {
                SetSelectedObject(ReferenceEquals(_selectedObject, creature) ? null : creature);
            }

            return;
        }

        if (TryHitBuilding(point, out var building))
        {
            ClearPendingManualMove();
            ClearMiningTileSelection();
            if (!BuildMode)
            {
                CleanActive();
                if (building.CanBeSelected())
                {
                    SetSelectedObject(building);
                }
            }

            return;
        }

        var tile = GetTileAtScreenPoint(point);
        if (tile is null)
        {
            ClearPendingManualMove();
            if (!BuildMode)
            {
                CleanActive();
            }

            return;
        }

        if (TryHandleMiningTileSelectionClick(tile))
        {
            return;
        }

        if (HasSelectedMiningTiles())
        {
            ClearMiningTileSelection();
            return;
        }

        if (TryConsumePendingManualMove(tile.Key, out var pendingTargets))
        {
            TryHandleManualMove(tile, pendingTargets);
            return;
        }

        if (_selectedTrilobites.Count > 0)
        {
            var moveTargets = _selectedTrilobites
                .Where(trilobite => trilobite.Cave is not null)
                .Distinct()
                .ToArray();
            CleanActive();
            ArmPendingManualMove(tile.Key, moveTargets);
            return;
        }

        CleanActive();
    }

    private bool HandleMenuClick(Point point)
    {
        var result = _menu.HandleClick(point, Window.ClientBounds.Size, _session);
        if (result.PlaySelectSound)
        {
            PlayUiSelectSound();
        }

        if (result.BuildingPlacement is { } placementRequest)
        {
            BeginBuildingPlacement(placementRequest.Factory);
        }

        return result.Consumed;
    }

    private void HandleDebugAntHolePlacementClick(Point point)
    {
        var cave = _session.Cave;
        var tile = GetTileAtScreenPoint(point);
        if (cave is null || tile is null)
        {
            return;
        }

        if (!cave.CanPlaceAntHole(tile))
        {
            return;
        }

        if (cave.SpawnAntHole(tile, GameConstants.MinAmbientAntSpawnCount))
        {
            _debugAntHolePlacementMode = false;
            RefreshBfsFieldDebug();
        }
    }

    private void HandleWorldRightClick(Point point)
    {
        ClearPendingManualMove();
        if (_debugAntHolePlacementMode)
        {
            _debugAntHolePlacementMode = false;
            _roleRadialMenu = null;
            _miningOrderMenu.Close();
            return;
        }

        if (BuildMode)
        {
            CancelBuildingPlacement();
            return;
        }

        if (TryHitTrilobite(point, out var trilobite))
        {
            ClearMiningTileSelection();
            if (!SelectionRetention.ShouldPreserveCurrentSelection(_selectedTrilobites, trilobite))
            {
                SetSelectedTrilobites([trilobite], openMenuForSingle: false);
            }

            OpenRoleRadialMenu(GetCreatureScreenPosition(trilobite), _selectedTrilobites, anchorToCreature: true);
            return;
        }

        var tile = GetTileAtScreenPoint(point);
        if (tile is not null && CanSelectMiningTile(tile))
        {
            ClearObjectSelection();
            if (!_miningTileSelection.Contains(tile.Key))
            {
                SelectMiningTile(tile, append: false, toggleIfAlreadySelected: false);
            }

            OpenMiningOrderMenu(point);
            return;
        }

        if (_selectedTrilobites.Count > 1)
        {
            ClearMiningTileSelection();
            OpenRoleRadialMenu(point.ToVector2(), _selectedTrilobites, anchorToCreature: false);
            return;
        }

        ClearMiningTileSelection();
        _roleRadialMenu = null;
    }

    private bool CanStartCameraPanDrag(Point point)
    {
        return _roleRadialMenu is null
            && !_selectionDragActive
            && !_menu.CoversScreenPoint(point, Window.ClientBounds.Size)
            && !SettingsCoversPoint(point)
            && !ResearchDraftCoversPoint(point)
            && !TrilodexCoversPoint(point);
    }

    private bool ShouldStartSelectionDrag(Point point)
    {
        return !BuildMode
            && !_menu.CoversScreenPoint(point, Window.ClientBounds.Size)
            && !SettingsCoversPoint(point)
            && !ResearchDraftCoversPoint(point)
            && !TrilodexCoversPoint(point);
    }

    private void FinalizeSelectionBox()
    {
        if (_selectionBoxBounds is null)
        {
            return;
        }

        var selected = GetTrilobitesInScreenRectangle(_selectionBoxBounds.Value);
        if (selected.Count > 0)
        {
            SelectTrilobitesFromSelectionBox(selected);
            return;
        }

        if (TryFinalizeMiningTileSelectionBox())
        {
            return;
        }

        if (!_selectionDragAppend)
        {
            CleanActive();
        }
    }

    private void SelectTrilobitesFromSelectionBox(IReadOnlyList<Trilobite> selected)
    {
        if (_selectionDragAppend)
        {
            var updatedSelection = _selectedTrilobites.Where(trilobite => trilobite.Cave is not null).ToList();
            foreach (var trilobite in selected)
            {
                if (!updatedSelection.Contains(trilobite))
                {
                    updatedSelection.Add(trilobite);
                }
            }

            SetSelectedTrilobites(updatedSelection, openMenuForSingle: false);
            return;
        }

        SetSelectedTrilobites(selected, openMenuForSingle: false);
    }

    private void ArmPendingManualMove(string tileKey, IEnumerable<Trilobite> targets)
    {
        _pendingManualMoveTargets = targets
            .Where(trilobite => trilobite.Cave is not null)
            .Distinct()
            .ToArray();

        if (_pendingManualMoveTargets.Length == 0)
        {
            ClearPendingManualMove();
            return;
        }

        _manualMoveDoubleClick.Arm(tileKey, _uiClockMs);
    }

    private bool TryConsumePendingManualMove(string tileKey, out Trilobite[] targets)
    {
        targets = [];
        if (!_manualMoveDoubleClick.TryConsume(tileKey, _uiClockMs, GameConstants.DoubleClickThresholdMs))
        {
            _pendingManualMoveTargets = [];
            return false;
        }

        targets = _pendingManualMoveTargets
            .Where(trilobite => trilobite.Cave is not null)
            .Distinct()
            .ToArray();
        _pendingManualMoveTargets = [];
        return targets.Length > 0;
    }

    private void ExpirePendingManualMove()
    {
        _manualMoveDoubleClick.Expire(_uiClockMs, GameConstants.DoubleClickThresholdMs);
        if (!_manualMoveDoubleClick.HasPending)
        {
            _pendingManualMoveTargets = [];
        }
    }

    private void ClearPendingManualMove()
    {
        _manualMoveDoubleClick.Clear();
        _pendingManualMoveTargets = [];
    }

    private bool TryHandleManualMove(Tile tile, IEnumerable<Trilobite>? targets = null)
    {
        var moveTargets = (targets ?? _selectedTrilobites)
            .Where(trilobite => trilobite.Cave is not null)
            .Distinct()
            .ToArray();
        if (moveTargets.Length == 0)
        {
            return false;
        }

        var destination = GridPoint.Parse(tile.Key);
        var movedAny = false;
        foreach (var trilobite in moveTargets)
        {
            movedAny = trilobite.NavigateTo(destination, trilobite.GetBehavior(), clearExisting: true) || movedAny;
        }

        return movedAny;
    }

    private bool TryHandleRoleRadialClick(Point point)
    {
        if (_roleRadialMenu is null)
        {
            return false;
        }

        var button = BuildRoleRadialButtons(_roleRadialMenu).FirstOrDefault(candidate => candidate.Bounds.Contains(point));
        if (button.Assignment is null)
        {
            _roleRadialMenu = null;
            return false;
        }

        PlayUiSelectSound();
        AssignRoleToTrilobites(_roleRadialMenu.Targets, button.Assignment);
        _roleRadialMenu = null;
        return true;
    }

    private void OpenRoleRadialMenu(Vector2 centerScreen, IEnumerable<Trilobite> targets, bool anchorToCreature)
    {
        var validTargets = targets
            .Where(trilobite => trilobite.Cave is not null)
            .Distinct()
            .ToArray();
        _roleRadialMenu = validTargets.Length == 0
            ? null
            : new RoleRadialMenuState(centerScreen, validTargets, anchorToCreature);
    }

    private void AssignRoleToTrilobites(IEnumerable<Trilobite> targets, string assignment)
    {
        foreach (var trilobite in targets)
        {
            if (trilobite.Cave is null)
            {
                continue;
            }

            trilobite.ChangeAssignment(assignment);
        }
    }

    private void ShowBfsFieldDebug(string fieldName)
    {
        _session.Cave?.RefreshBfsField(fieldName);
        _activeBfsDebugField = fieldName;
        _debugMapOverlayVisible = true;
    }

    public void RunSingleTick()
    {
        _simulationClock.RunSingleTick(_session, HandleSimulationTickCompleted);
        RefreshBfsFieldDebug();
    }

    private void AdvanceSimulation(GameTime gameTime)
    {
        _simulationClock.Advance(
            _session,
            gameTime.ElapsedGameTime.TotalMilliseconds,
            _stopSimulationAfterTick,
            HandleSimulationTickCompleted);
    }

    private bool StopSimulationAfterTick()
    {
        if (HasLostQueen())
        {
            TriggerGameOver();
            return true;
        }

        return _gamePaused;
    }

    private void TogglePauseState()
    {
        CleanActive();
        _gamePaused = !_gamePaused;
    }

    private void RefreshBfsFieldDebug()
    {
        if (_activeBfsDebugField is not null)
        {
            _session.Cave?.RefreshBfsField(_activeBfsDebugField);
        }
    }

    private void DrawRoleLabels(Cave cave)
    {
        if (!_showRoleLabels)
        {
            return;
        }

        foreach (var trilobite in cave.Trilobites)
        {
            var position = GetCreatureScreenPosition(trilobite);
            var label = GetAssignmentLabel(trilobite.Assignment);
            var size = GumTextLayout.Measure(label, GumTextStyle.Debug);
            var bounds = new Rectangle(
                (int)MathF.Round(position.X - (size.X / 2f) - 8f),
                (int)MathF.Round(position.Y - (TileConstants.TileHalfSize * _camera.CurrentScale) - size.Y - 14f),
                (int)MathF.Round(size.X + 16f),
                (int)MathF.Round(size.Y + 8f));

            _gumUiRenderer.AddFilledRectangle(bounds, new Color(6, 12, 18, 210));
            DrawScreenBorder(bounds, new Color(127, 179, 196), 1);
            DrawScreenTextFittedCentered(label, bounds, new Color(230, 239, 245), _rendering.DebugFont, minScale: 0.72f);
        }
    }

    private void DrawSelection()
    {
        if (_selectedTrilobites.Count > 0)
        {
            if (_selectedTrilobites.Count == 1)
            {
                // Creature path rendering is intentionally disabled while autonomous
                // building navigation uses per-tick BFS-field stepping.
            }

            foreach (var selectedTrilobite in _selectedTrilobites)
            {
                DrawWorldTextureNative(
                    "Selected",
                    GetCreatureWorldPosition(selectedTrilobite));
            }

            return;
        }

        if (_selectedObject is Creature creature)
        {
            // Creature path rendering is intentionally disabled while autonomous
            // building navigation uses per-tick BFS-field stepping.

            DrawWorldTextureNative(
                "Selected",
                GetCreatureWorldPosition(creature));
        }
        else if (_selectedObject is Building building)
        {
            if (building is MiningPost miningPost && miningPost.Location is not null)
            {
                DrawWorldCircleOutline(
                    GetPlacedBuildingWorldCenter(miningPost),
                    miningPost.Radius * TileConstants.TileSize,
                    new Color(108, 196, 224, 196),
                    MathF.Max(2f, _camera.CurrentScale * 2f));
            }

            DrawTileSelectionOutline(BuildingHighlightFootprint.EnumerateTiles(building));
        }
    }

    private void DrawTileSelectionOutline(IEnumerable<GridPoint> tiles)
    {
        foreach (var edge in TileSelectionOutline.BuildEdges(tiles))
        {
            var midpoint = edge.Side switch
            {
                TileSelectionEdgeSide.Top => new Vector2(
                    edge.Tile.X * TileConstants.TileSize,
                    (edge.Tile.Y * TileConstants.TileSize) - TileConstants.TileHalfSize),
                TileSelectionEdgeSide.Right => new Vector2(
                    (edge.Tile.X * TileConstants.TileSize) + TileConstants.TileHalfSize,
                    edge.Tile.Y * TileConstants.TileSize),
                TileSelectionEdgeSide.Bottom => new Vector2(
                    edge.Tile.X * TileConstants.TileSize,
                    (edge.Tile.Y * TileConstants.TileSize) + TileConstants.TileHalfSize),
                _ => new Vector2(
                    (edge.Tile.X * TileConstants.TileSize) - TileConstants.TileHalfSize,
                    edge.Tile.Y * TileConstants.TileSize)
            };
            var origin = edge.Side is TileSelectionEdgeSide.Top or TileSelectionEdgeSide.Left
                ? new Vector2(TileConstants.TileHalfSize, 4f)
                : new Vector2(TileConstants.TileHalfSize, 0f);
            DrawWorldTextureNative(
                "SelectedEdge",
                midpoint,
                edge.Side is TileSelectionEdgeSide.Left or TileSelectionEdgeSide.Right ? MathF.PI / 2f : 0f,
                origin);
        }
    }

    private void DrawWorldCircleOutline(Vector2 worldCenter, float worldRadius, Color color, float thickness = 2f)
    {
        if (worldRadius <= 0f)
        {
            return;
        }

        const int segments = 72;
        var previousPoint = _camera.WorldToScreen(worldCenter + new Vector2(worldRadius, 0f));
        for (var index = 1; index <= segments; index++)
        {
            var angle = (index / (float)segments) * MathF.Tau;
            var nextPoint = _camera.WorldToScreen(worldCenter + new Vector2(MathF.Cos(angle) * worldRadius, MathF.Sin(angle) * worldRadius));
            DrawScreenLine(previousPoint, nextPoint, color, thickness);
            previousPoint = nextPoint;
        }
    }

    private void DrawSelectionBox()
    {
        if (_selectionBoxBounds is null || !_input.Dragging || !_selectionDragActive)
        {
            return;
        }

        _gumUiRenderer.AddFilledRectangle(_selectionBoxBounds.Value, new Color(88, 179, 214, 48));
        DrawScreenBorder(_selectionBoxBounds.Value, new Color(146, 213, 239), 2);
    }

    private void DrawRoleRadialMenu()
    {
        if (_roleRadialMenu is null)
        {
            return;
        }

        var center = _roleRadialMenu.CenterScreen;
        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(Window.ClientBounds.Size, _menu.GetOpenPanelWidth(Window.ClientBounds.Size));

        var title = _roleRadialMenu.Targets.Length == 1
            ? _roleRadialMenu.Targets[0].Name
            : $"{_roleRadialMenu.Targets.Length} Trilobites";
        var titleMeasure = GumTextLayout.Measure(title, GumTextStyle.Debug);
        var titleBounds = RoleRadialLayout.GetLabelBounds(
            center,
            titleMeasure,
            gameplayBounds);
        DrawRoundedScreenFrame(titleBounds, new Color(7, 15, 22, 232), new Color(143, 205, 226), 2, 12);
        DrawScreenTextFittedCentered(title, titleBounds, Color.White, _rendering.DebugFont, minScale: 0.72f);

        foreach (var button in BuildRoleRadialButtons(_roleRadialMenu))
        {
            var hovered = button.Bounds.Contains(_input.MousePoint);
            var fill = button.Selected
                ? hovered ? new Color(197, 173, 124) : new Color(172, 148, 102)
                : hovered ? new Color(54, 82, 103) : new Color(28, 44, 57);
            var border = button.Selected
                ? hovered ? new Color(255, 233, 188) : new Color(233, 210, 159)
                : hovered ? new Color(185, 213, 224) : new Color(94, 128, 144);
            var textColor = button.Selected ? new Color(10, 23, 34) : Color.White;

            DrawRoundedScreenFrame(button.Bounds, fill, border, 2, 12);
            DrawScreenTextFittedCentered(button.Label, button.Bounds, textColor, _rendering.SmallFont, minScale: 0.62f);
        }
    }

    private void DrawFocusHint()
    {
        if (!TryGetFocusHintTarget(out _, out _))
        {
            return;
        }

        var viewport = Window.ClientBounds.Size;
        var hintBounds = SelectionFocusLayout.GetFocusHintBounds(viewport, _menu.GetOpenPanelWidth(viewport));
        const string label = "F to focus";
        DrawRoundedScreenFrame(hintBounds, new Color(7, 15, 22, 224), new Color(143, 205, 226), 2, 14);
        DrawScreenTextFittedCentered(label, hintBounds, new Color(239, 247, 252), _rendering.SmallFont, minScale: 0.72f);
    }

    private void DrawSettingsMenu()
    {
        if (!HasGumUiRenderer)
        {
            return;
        }

        _settingsMenuRenderer.Draw(
            _gumUiRenderer,
            _rendering,
            Window.ClientBounds.Size,
            _input.MousePoint,
            _settingsMenuOpen,
            _mainMenuOpen,
            _audio.VolumePercent);
    }

    private void DrawResourceHud()
    {
        if (!HasGumUiRenderer || _session.Cave is null)
        {
            return;
        }

        var stockpile = _resourceStockpileSystem.Refresh(_session);
        _resourceHud.Draw(_gumUiRenderer, _rendering.Sprites, Window.ClientBounds.Size, _input.MousePoint, stockpile);
    }

    private void DrawFloatingPreview()
    {
        if (_debugAntHolePlacementMode)
        {
            var antHoleTile = GetTileAtScreenPoint(_input.MousePoint);
            if (antHoleTile is null)
            {
                return;
            }

            var canPlace = _session.Cave?.CanPlaceAntHole(antHoleTile) == true;
            DrawWorldTextureNative(
                "AntHole",
                new Vector2(antHoleTile.Coordinates.X * TileConstants.TileSize, antHoleTile.Coordinates.Y * TileConstants.TileSize),
                color: (canPlace ? Color.White : new Color(255, 96, 96)) * 0.75f);
            return;
        }

        if (_buildingPlacementCursor is null)
        {
            return;
        }

        if (!TryGetBuildingPlacementLocation(_input.MousePoint, out var endLocation))
        {
            return;
        }

        IReadOnlyList<GridPoint> previewLocations = _buildingPlacementDragActive
            ? BuildPlacementPreviewResolver.ResolveLocations(
                _buildingPlacementCursor.TargetBuilding,
                endLocation,
                _buildingPlacementDragStart)
            : [endLocation];

        foreach (var location in previewLocations)
        {
            if (!TryEvaluateBuildingPlacement(location, out var placement))
            {
                continue;
            }

            var previewBuilding = _buildingPlacementCursor.TargetBuilding;
            if (previewBuilding is SoilPatch soilPatch)
            {
                var previewColor = (placement.CanBuild ? Color.White : new Color(255, 96, 96)) * 0.7f;
                for (var tileIndex = 0; tileIndex < soilPatch.SoilTiles.Count; tileIndex++)
                {
                    var localOffset = soilPatch.SoilTiles[tileIndex].LocalOffset;
                    DrawWorldTextureNative(
                        soilPatch.SoilTiles[tileIndex].TextureKey,
                        new Vector2(
                            (location.X + localOffset.X) * TileConstants.TileSize,
                            (location.Y + localOffset.Y) * TileConstants.TileSize),
                        color: previewColor);
                }

                continue;
            }

            var previewCenter = _camera.WorldToScreen(BuildingPlacementGrid.GetWorldCenter(placement.Location, previewBuilding));
            if (!_rendering.Sprites.TryGet(previewBuilding.TextureKey, out var previewTexture))
            {
                continue;
            }

            DrawScreenTextureNative(
                previewBuilding.TextureKey,
                previewCenter,
                _buildingPlacementCursor.GetDisplayRotationTurns() * MathF.PI / 2f,
                color: (placement.CanBuild ? Color.White : new Color(255, 96, 96)) * 0.7f,
                scale: BuildingPlacementGrid.GetTextureFootprintScale(
                    previewBuilding,
                    previewTexture.Width,
                    previewTexture.Height,
                    _camera.CurrentScale));
        }
    }

    private bool TryGetBuildingPlacementLocation(Point point, out GridPoint location)
    {
        location = default;
        if (_buildingPlacementCursor is null)
        {
            return false;
        }

        location = BuildingPlacementGrid.GetSnappedTopLeft(_camera, point, _buildingPlacementCursor.TargetBuilding);
        return true;
    }

    private bool TryEvaluateBuildingPlacement(GridPoint location, out BuildPlacementResult placement)
    {
        placement = null!;
        if (_buildingPlacementCursor is null || _session.Cave is null)
        {
            return false;
        }

        var buildingToPlace = _buildingPlacementCursor.GetPlacementCandidate(_session.Runtime.NoCostBuildPlacement);
        placement = _session.Cave.EvaluateBuildPlacement(buildingToPlace, location, preserveReachability: true);
        return true;
    }

    private bool TryPlaceBuildingAt(GridPoint location)
    {
        if (_buildingPlacementCursor is null || _session.Cave is null)
        {
            return false;
        }

        var buildingToPlace = _buildingPlacementCursor.CreatePlacementCandidate(_session.Runtime.NoCostBuildPlacement);
        var placement = _session.Cave.EvaluateBuildPlacement(buildingToPlace, location, preserveReachability: true);
        return placement.CanBuild && _session.Cave.Build(buildingToPlace, placement.Location, preserveReachability: true);
    }

    private void DrawDebugOverlay(Cave cave)
    {
        if (!_debugMapOverlayVisible || _activeBfsDebugField is null || !_gamePaused)
        {
            return;
        }

        var field = cave.GetBfsField(_activeBfsDebugField);
        if (field is null)
        {
            return;
        }

        foreach (var tile in WorldSceneRenderer.EnumerateVisibleTiles(cave, _camera, Window.ClientBounds.Size, _showFullMapVisibility))
        {
            var value = field.GetValueOrDefault(tile.Key, int.MaxValue);
            if (value == int.MaxValue)
            {
                continue;
            }

            var screen = _camera.WorldToScreen(new Vector2(GridPoint.Parse(tile.Key).X * TileConstants.TileSize, GridPoint.Parse(tile.Key).Y * TileConstants.TileSize));
            _spriteBatch.DrawString(_rendering.DebugFont, value.ToString(), screen - new Vector2(10f, 12f), Color.Gold);
        }
    }

    private void DrawDebugMetricsOverlay()
    {
        var lines = BuildDebugMetricsOverlayLines();
        if (lines.Count == 0)
        {
            return;
        }

        var font = _rendering.DebugFont;
        var lineHeight = Math.Max(16, font.LineSpacing + 2);
        var viewport = Window.ClientBounds.Size;
        var totalHeight = lines.Count * lineHeight;
        var start = new Vector2(12f, Math.Max(12f, viewport.Y - totalHeight - 12f));

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        for (var index = 0; index < lines.Count; index++)
        {
            DrawOutlinedDebugText(lines[index], start + new Vector2(0f, index * lineHeight));
        }

        _spriteBatch.End();
    }

    private void DrawOutlinedDebugText(string text, Vector2 position)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var snapped = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        var outlineColor = new Color(0, 0, 0, 230);
        var offsets = new[]
        {
            new Vector2(-1f, -1f),
            new Vector2(0f, -1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        foreach (var offset in offsets)
        {
            _spriteBatch.DrawString(_rendering.DebugFont, text, snapped + offset, outlineColor);
        }

        _spriteBatch.DrawString(_rendering.DebugFont, text, snapped, Color.White);
    }

    private Texture2D? GetTreeBackgroundTexture()
    {
        return _rendering.Sprites.TryGet("TreeBackground", out var texture)
            ? texture
            : null;
    }

    private IReadOnlyList<string> BuildDebugMetricsOverlayLines()
    {
        return GameAppDebugMetricsBuilder.BuildLines(
            new GameAppDebugMetricsSnapshot(
                _gamePaused,
                _session.Danger,
                _session.TickCount,
                _tickSpeedMs,
                _activeBfsDebugField ?? "none",
                _showRoleLabels,
                _session.Runtime.NoCostBuildPlacement,
                _session.Runtime.TickProfiler.Last,
                _session.Runtime.TickProfiler.Average,
                _session.Runtime.TickProfiler.AverageMinerMsPerTrilobite,
                _session.Runtime.TickProfiler.AverageBuilderMsPerTrilobite,
                _session.Runtime.TickProfiler.AverageFarmerMsPerTrilobite,
                _session.Runtime.TickProfiler.AverageFighterMsPerTrilobite));
    }

    private void DrawMainMenuDebugOverlay()
    {
        var viewport = Window.ClientBounds.Size;
        var layout = MainMenuDebugLayout.Build(
            viewport,
            WorldGenerationMethods.SelectablePatterns.Count,
            _mainMenuWorldGenerationDropdownOpen);
        var pointer = _input.MousePoint;

        _gumUiRenderer.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 10, 16) * 0.4f);
        _gumUiRenderer.AddFilledRectangle(layout.PanelBounds, new Color(13, 24, 34) * 0.96f);
        DrawScreenBorder(layout.PanelBounds, new Color(187, 163, 114), 2);

        DrawScreenTextFittedCentered("Debug", layout.HeaderBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawDebugInfoCard(
            layout.SummaryBounds,
            "Startup Options",
            ["These settings apply to the next colony you start from the main menu."],
            _rendering.SmallFont,
            new Color(220, 228, 235),
            new Color(10, 19, 28),
            new Color(83, 121, 139));
        DrawDebugSectionLabel(layout.WorldGenerationLabelBounds, "World Generation Method");

        var dropdownLabel = WorldGenerationMethods.GetDisplayName(_worldGenerationMethod);
        var dropdownHovered = layout.DropdownBounds.Contains(pointer);
        DrawDebugButton(layout.DropdownBounds, dropdownLabel, dropdownHovered, true, _mainMenuWorldGenerationDropdownOpen);
        var dropdownArrowBounds = new Rectangle(
            layout.DropdownBounds.Right - 40,
            layout.DropdownBounds.Y,
            32,
            layout.DropdownBounds.Height);
        DrawScreenTextFittedCentered(
            _mainMenuWorldGenerationDropdownOpen ? "^" : "v",
            dropdownArrowBounds,
            _mainMenuWorldGenerationDropdownOpen ? new Color(10, 23, 34) : Color.White,
            _rendering.SmallFont,
            minScale: 0.72f);

        if (_mainMenuWorldGenerationDropdownOpen && layout.DropdownOptionsBounds is { } optionBounds)
        {
            foreach (var option in BuildMainMenuWorldGenerationOptions(optionBounds, layout.RowGap))
            {
                DrawDebugButton(
                    option.Bounds,
                    option.Label,
                    option.Bounds.Contains(pointer),
                    true,
                    option.Selected);
            }
        }

        DrawWrappedScreenText(
            ["` closes this panel."],
            layout.FooterBounds,
            new Color(141, 183, 199),
            _rendering.SmallFont,
            lineGap: 1);
    }

    private void DrawDebugMenuOverlay()
    {
        var viewport = Window.ClientBounds.Size;
        var overlayBounds = new Rectangle(0, 0, viewport.X, viewport.Y);
        var layout = DebugMenuLayout.Build(viewport);
        var panelBounds = layout.PanelBounds;
        var pointer = _input.MousePoint;

        _gumUiRenderer.AddFilledRectangle(overlayBounds, new Color(5, 10, 16) * 0.4f);
        _gumUiRenderer.AddFilledRectangle(panelBounds, new Color(13, 24, 34) * 0.96f);
        DrawScreenBorder(panelBounds, new Color(187, 163, 114), 2);

        DrawScreenTextFittedCentered("Debug", layout.HeaderBounds, Color.White, _rendering.UiFont, minScale: 0.72f);

        DrawDebugSectionLabel(layout.QuickControlsLabelBounds, "Quick Controls");
        DrawDebugSectionLabel(layout.SpeedLabelBounds, "Game Loop Speed");
        DrawDebugSectionLabel(layout.BfsLabelBounds, "BFS Debug");
        DrawDebugSectionLabel(layout.VisualLabelBounds, "Visual Debug");
        DrawDebugSectionLabel(layout.ActionsLabelBounds, "Actions");
        _debugToggleControls.Draw(
            _gumUiRenderer,
            viewport,
            _debugMenuOpen,
            _showRoleLabels,
            _session.Runtime.DisableEnemySpawns,
            _session.Runtime.NoCostBuildPlacement,
            _infiniteDraft,
            _showFullMapVisibility,
            pointer);
        DrawWrappedScreenText(
            ["` closes this panel. F3 toggles metrics."],
            layout.FooterBounds,
            new Color(141, 183, 199),
            _rendering.SmallFont,
            lineGap: 1);

        foreach (var button in _debugMenu.GetButtons(viewport, BuildDebugMenuState()))
        {
            DrawDebugMenuButton(button, button.Bounds.Contains(pointer));
        }

        var spawnActionSlots = DebugMenuLayout.SplitRow(layout.SpawnActionsRowBounds, 2, layout.ButtonGap);
        _debugEnemySpawnControl.Draw(_gumUiRenderer, spawnActionSlots[0], pointer);
        _debugTrilobiteSpawnControl.Draw(_gumUiRenderer, spawnActionSlots[1], pointer);
    }

    private void DrawDebugMenuButton(DebugMenuButton button, bool hovered)
    {
        var fill = new Color(36, 50, 64);
        var border = new Color(96, 120, 138);
        var textColor = Color.White;

        if (!button.Enabled)
        {
            fill = new Color(26, 34, 42);
            border = new Color(60, 70, 80);
            textColor = new Color(128, 139, 148);
        }
        else if (button.Selected)
        {
            fill = hovered ? new Color(194, 171, 122) : new Color(170, 148, 102);
            border = hovered ? new Color(255, 232, 184) : new Color(235, 210, 158);
            textColor = new Color(10, 23, 34);
        }
        else if (hovered)
        {
            fill = new Color(64, 83, 101);
            border = new Color(210, 187, 136);
        }

        _gumUiRenderer.AddFilledRectangle(button.Bounds, fill);
        DrawScreenBorder(button.Bounds, border, 2);

        var textBounds = new Rectangle(button.Bounds.X + 8, button.Bounds.Y + 4, Math.Max(0, button.Bounds.Width - 16), Math.Max(0, button.Bounds.Height - 8));
        DrawScreenTextFittedCentered(button.Label, textBounds, textColor, _rendering.SmallFont, minScale: 0.64f);
    }

    private void DrawDebugSectionLabel(Rectangle bounds, string label)
    {
        DrawScreenTextFittedCentered(label, bounds, new Color(255, 214, 150), _rendering.SmallFont, minScale: 0.72f);
    }

    private void DrawDebugButton(Rectangle bounds, string label, bool hovered, bool enabled, bool selected)
    {
        DrawDebugMenuButton(new DebugMenuButton(DebugMenuAction.Close, label, bounds, enabled, selected), hovered);
    }

    private void DrawDebugInfoCard(
        Rectangle bounds,
        string title,
        IReadOnlyList<string> lines,
        SpriteFont font,
        Color textColor,
        Color fillColor,
        Color borderColor)
    {
        _gumUiRenderer.AddFilledRectangle(bounds, fillColor);
        DrawScreenBorder(bounds, borderColor, 1);

        var titleBounds = new Rectangle(bounds.X + 12, bounds.Y + 8, Math.Max(0, bounds.Width - 24), 20);
        DrawScreenTextFittedCentered(title, titleBounds, new Color(255, 214, 150), font, minScale: 0.72f);

        var textBounds = new Rectangle(
            bounds.X + 12,
            titleBounds.Bottom + 8,
            Math.Max(0, bounds.Width - 24),
            Math.Max(0, bounds.Bottom - titleBounds.Bottom - 18));
        DrawWrappedScreenText(lines, textBounds, textColor, font, lineGap: 1);
    }

    private IReadOnlyList<MainMenuWorldGenerationOptionButton> BuildMainMenuWorldGenerationOptions(Rectangle optionBounds, int gap)
    {
        var patterns = WorldGenerationMethods.SelectablePatterns;
        var bounds = MainMenuDebugLayout.StackRows(optionBounds, patterns.Count, gap);
        var options = new MainMenuWorldGenerationOptionButton[bounds.Count];
        for (var index = 0; index < bounds.Count; index++)
        {
            var pattern = patterns[index];
            options[index] = new MainMenuWorldGenerationOptionButton(
                pattern.Method,
                pattern.DisplayName,
                bounds[index],
                pattern.Method == _worldGenerationMethod);
        }

        return options;
    }

    private IReadOnlyList<RoleRadialButton> BuildRoleRadialButtons(RoleRadialMenuState radialMenu)
    {
        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(Window.ClientBounds.Size, _menu.GetOpenPanelWidth(Window.ClientBounds.Size));
        var roles = new (string Assignment, string Label)[]
        {
            ("unassigned", "Unassigned"),
            ("miner", "Miner"),
            ("builder", "Builder"),
            ("farmer", "Farmer"),
            ("fighter", "Fighter")
        };

        var uniformAssignment = RoleSelectionState.GetUniformAssignment(radialMenu.Targets);

        var buttons = new List<RoleRadialButton>(roles.Length);
        for (var index = 0; index < roles.Length; index++)
        {
            var angle = (-MathF.PI / 2f) + (index * (MathF.Tau / roles.Length));
            var bounds = RoleRadialLayout.GetButtonBounds(radialMenu.CenterScreen, angle, gameplayBounds);
            buttons.Add(new RoleRadialButton(
                roles[index].Assignment,
                roles[index].Label,
                bounds,
                string.Equals(uniformAssignment, roles[index].Assignment, StringComparison.Ordinal)));
        }

        return buttons;
    }

    private void DrawWorldTexture(string textureKey, GridPoint point, float rotation, Vector2 sizeScale, Color? color = null)
    {
        DrawWorldTexture(textureKey, point.ToVector2(), rotation, sizeScale, color);
    }

    private void DrawWorldTexture(string textureKey, Vector2 gridPoint, float rotation, Vector2 sizeScale, Color? color = null)
    {
        if (!_rendering.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        var world = new Vector2(gridPoint.X * TileConstants.TileSize, gridPoint.Y * TileConstants.TileSize);
        var scale = new Vector2(
            (TileConstants.TileSize * sizeScale.X * _camera.CurrentScale) / texture.Width,
            (TileConstants.TileSize * sizeScale.Y * _camera.CurrentScale) / texture.Height);

        _spriteBatch.Draw(
            texture,
            _camera.WorldToScreen(world),
            null,
            color ?? Color.White,
            rotation,
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            scale,
            SpriteEffects.None,
            0f);
    }

    private void DrawTileTexture(string textureKey, GridPoint point, Color? color = null)
    {
        DrawWorldTexture(
            textureKey,
            point,
            0f,
            Vector2.One,
            color);
    }

    private void DrawWorldTextureNative(string textureKey, Vector2 worldPixels, float rotation = 0f, Vector2? origin = null, Color? color = null, Vector2? scale = null)
    {
        if (!_rendering.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        _spriteBatch.Draw(
            texture,
            _camera.WorldToScreen(worldPixels),
            null,
            color ?? Color.White,
            rotation,
            origin ?? new Vector2(texture.Width / 2f, texture.Height / 2f),
            scale ?? new Vector2(_camera.CurrentScale),
            SpriteEffects.None,
            0f);
    }

    private void DrawScreenTextureNative(string textureKey, Vector2 screenPosition, float rotation = 0f, Vector2? origin = null, Color? color = null, Vector2? scale = null)
    {
        if (!_rendering.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        _spriteBatch.Draw(
            texture,
            screenPosition,
            null,
            color ?? Color.White,
            rotation,
            origin ?? new Vector2(texture.Width / 2f, texture.Height / 2f),
            scale ?? new Vector2(_camera.CurrentScale),
            SpriteEffects.None,
            0f);
    }

    private void DrawScreenBorder(Rectangle bounds, Color color, int thickness)
    {
        if (!HasGumUiRenderer)
        {
            return;
        }

        _gumUiRenderer.AddRectangleOutline(bounds, color, thickness);
    }

    private void DrawScreenLine(Vector2 start, Vector2 end, Color color, float thickness = 1f)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.5f)
        {
            return;
        }

        _spriteBatch.Draw(
            _rendering.WhitePixel,
            start,
            null,
            color,
            MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f),
            new Vector2(length, MathF.Max(1f, thickness)),
            SpriteEffects.None,
            0f);
    }

    private void DrawRoundedScreenFrame(Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (!HasGumUiRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _gumUiRenderer.AddRoundedFrame(bounds, fill, border, thickness, radius);
    }

    private void DrawRoundedScreenRect(Rectangle bounds, Color color, int radius)
    {
        if (!HasGumUiRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _gumUiRenderer.AddRoundedRectangle(bounds, color, radius);
    }

    private void DrawScreenTextFittedCentered(string text, Rectangle bounds, Color color, SpriteFont font, float minScale = 0.72f)
    {
        if (!HasGumUiRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = ResolveGumTextStyle(font);
        var metrics = GumTextLayout.GetMetrics(style);
        var textToDraw = GumTextLayout.FitToWidth(text, bounds.Width, style);
        _gumUiRenderer.AddText(
            bounds,
            textToDraw,
            color,
            HorizontalAlignment.Center,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: 1);
    }

    private void DrawWrappedScreenText(IEnumerable<string> paragraphs, Rectangle bounds, Color color, SpriteFont font, int lineGap = 2)
    {
        if (!HasGumUiRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = ResolveGumTextStyle(font);
        var metrics = GumTextLayout.GetMetrics(style);
        var lineAdvance = Math.Max(1, metrics.LineHeight + lineGap);
        var maxLines = Math.Max(1, (bounds.Height + lineGap) / lineAdvance);
        var lines = GumTextLayout.Wrap(paragraphs, bounds.Width, maxLines, style);
        _gumUiRenderer.AddText(bounds, string.Join('\n', lines), color, verticalAlignment: VerticalAlignment.Top, fontSize: metrics.FontSize, maxLines: lines.Count);
    }

    private void DrawScreenTextFittedLeft(string text, Rectangle bounds, Color color, SpriteFont font, float minScale = 0.72f)
    {
        if (!HasGumUiRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = ResolveGumTextStyle(font);
        var metrics = GumTextLayout.GetMetrics(style);
        var textToDraw = GumTextLayout.FitToWidth(text, bounds.Width, style);
        _gumUiRenderer.AddText(
            bounds,
            textToDraw,
            color,
            HorizontalAlignment.Left,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: 1);
    }

    private GumTextStyle ResolveGumTextStyle(SpriteFont font)
    {
        if (ReferenceEquals(font, _rendering.UiFont))
        {
            return GumTextStyle.UiLarge;
        }

        if (ReferenceEquals(font, _rendering.DebugFont))
        {
            return GumTextStyle.Debug;
        }

        if (ReferenceEquals(font, _rendering.SmallFont))
        {
            return GumTextStyle.Small;
        }

        return GumTextStyle.Ui;
    }

    private static Vector2 GetPlacedBuildingWorldCenter(Building building)
    {
        return BuildingPlacementGrid.GetWorldCenter(building);
    }

    private Rectangle GetDebugMenuBounds(Point viewport)
    {
        return DebugMenuLayout.Build(viewport).PanelBounds;
    }

    private void SpawnDebugTrilobite()
    {
        SpawnDebugTrilobites(1);
    }

    private void SpawnDebugTrilobites(int amount)
    {
        var cave = _session.Cave;
        var queen = cave?.GetQueenBuilding();
        if (cave is null || queen is null)
        {
            return;
        }

        var spawnTile = queen.GetBirthTile()
            ?? cave.GetReachableTiles().FirstOrDefault(tile =>
                tile.CreatureFits() &&
                tile.EnemyOccupant is null &&
                !cave.HasBlockingSurfaceFeature(tile));
        if (spawnTile is null)
        {
            return;
        }

        var spawnCount = Math.Clamp(amount, DebugSpawnAmountControl.MinimumAmount, DebugSpawnAmountControl.MaximumAmount);
        for (var index = 0; index < spawnCount; index++)
        {
            var debugId = _session.Runtime.AllocateDebugTrilobiteId();
            var trilobite = new Trilobite($"Debug Trilobite {debugId}", spawnTile.Coordinates, _session)
            {
                Assignment = "unassigned"
            };

            if (cave.Spawn(trilobite, spawnTile))
            {
                trilobite.RestartBehavior();
                _session.RequestAudioCue(GameAudioCue.TrilobiteBirth);
            }
        }
    }

    private static Rectangle CreateScreenRectangle(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private IReadOnlyList<Trilobite> GetTrilobitesInScreenRectangle(Rectangle selection)
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return [];
        }

        _selectionResultBuffer.Clear();
        foreach (var trilobite in cave.GetTrilobiteList())
        {
            if (trilobite.Cave == cave && selection.Intersects(GetCreatureHitBounds(trilobite)))
            {
                _selectionResultBuffer.Add(trilobite);
            }
        }

        return _selectionResultBuffer;
    }

    private object? GetSelectedFocusTarget()
    {
        if (_selectedTrilobites.Count == 1)
        {
            return _selectedTrilobites.First();
        }

        return _selectedObject is Trilobite or Building ? _selectedObject : null;
    }

    private bool TryGetFocusHintTarget(out object focusTarget, out Vector2 screenPosition)
    {
        focusTarget = GetSelectedFocusTarget()!;
        screenPosition = Vector2.Zero;
        if (focusTarget is null || _input.KeyHeld(Keys.F))
        {
            return false;
        }

        switch (focusTarget)
        {
            case Creature creature when creature.Cave is null:
            case Building { Cave: null }:
            case Building { Location: null }:
                return false;
        }

        var viewport = Window.ClientBounds.Size;
        var menuWidth = _menu.GetOpenPanelWidth(viewport);
        screenPosition = GetFocusScreenPosition(focusTarget);
        if (SelectionFocusLayout.IsNearGameplayCenter(screenPosition, viewport, menuWidth))
        {
            return false;
        }

        return !SelectionFocusLayout.IsInsideGameplayBounds(screenPosition, viewport, menuWidth);
    }

    private Vector2 GetFocusWorldPosition(object focusTarget)
    {
        return focusTarget switch
        {
            Creature creature => GetCreatureWorldPosition(creature),
            Building building when building.Location is not null => GetPlacedBuildingWorldCenter(building),
            _ => Vector2.Zero
        };
    }

    private Vector2 GetFocusScreenPosition(object focusTarget)
    {
        return _camera.WorldToScreen(GetFocusWorldPosition(focusTarget));
    }

    private Vector2 GetCreatureWorldPosition(Creature creature)
    {
        return ToFrameworkVector(creature.GetWorldPosition());
    }

    private Vector2 GetCreatureScreenPosition(Creature creature)
    {
        return _camera.WorldToScreen(GetCreatureWorldPosition(creature));
    }

    private static string GetAssignmentLabel(string assignment)
    {
        return assignment switch
        {
            "unassigned" => "Unassigned",
            "miner" => "Miner",
            "builder" => "Builder",
            "farmer" => "Farmer",
            "fighter" => "Fighter",
            _ => assignment
        };
    }

    private static Vector2 ToFrameworkVector(System.Numerics.Vector2 value)
    {
        return new Vector2(value.X, value.Y);
    }

    private string FormatPressedKeys()
    {
        var pressedKeys = _input.CurrentKeyboard.GetPressedKeys();
        return pressedKeys.Length == 0
            ? "none"
            : string.Join(", ", pressedKeys.Select(key => key.ToString()));
    }

    private static string FormatVector(Vector2 vector)
    {
        return $"{vector.X:0.###}, {vector.Y:0.###}";
    }

    private static string FormatByteCount(long byteCount)
    {
        if (byteCount >= 1024 * 1024)
        {
            return $"{byteCount / (1024d * 1024d):0.00} MB";
        }

        if (byteCount >= 1024)
        {
            return $"{byteCount / 1024d:0.0} KB";
        }

        return $"{byteCount} B";
    }

    private static string FormatTickProfile(TickTimingSnapshot snapshot, string label)
    {
        return $"{label}: total {snapshot.TotalMs:0.00} ms, bfs {snapshot.TotalBfsMs:0.00} ms, trilobites {snapshot.TrilobiteMoveMs:0.00} ms, enemies {snapshot.EnemyMoveMs:0.00} ms, buildings {snapshot.BuildingTickMs:0.00} ms, miner {FormatRoleTimingSnapshot(snapshot.MinerTiming)}, builder {FormatRoleTimingSnapshot(snapshot.BuilderTiming)}, farmer {FormatRoleTimingSnapshot(snapshot.FarmerTiming)}, fighter {FormatRoleTimingSnapshot(snapshot.FighterTiming)}, alloc {FormatByteCount(snapshot.AllocatedBytes)}, gc {snapshot.Gen0Collections}/{snapshot.Gen1Collections}/{snapshot.Gen2Collections}, counts {snapshot.TrilobiteCount}/{snapshot.EnemyCount}/{snapshot.BuildingCount}, work {snapshot.DescribeDominantWork()}";
    }

    private static string FormatRoleTimingSnapshot(RoleTimingSnapshot timing)
    {
        return $"{timing.AverageMsPerTrilobite:0.00} ms/trilo (X{timing.Count} = {timing.TotalMs:0.00} ms)";
    }

    private string DescribeSelectedObject()
    {
        return _selectedObject switch
        {
            Trilobite trilobite => $"Trilobite:{trilobite.Name}@{trilobite.Location}",
            Creature creature => $"Creature:{creature.Name}@{creature.Location}",
            Building building => $"Building:{building.Name}@{building.Location?.ToString() ?? "none"}",
            null => "none",
            _ => _selectedObject.GetType().Name
        };
    }

    private string FormatSelectedTrilobites()
    {
        if (_selectedTrilobites.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "; ",
            _selectedTrilobites.Select(trilobite =>
                $"{trilobite.Name}:{trilobite.Assignment}@{trilobite.Location} HP {trilobite.Health}/{trilobite.MaxHealth}"));
    }

    private string DescribeFloatingBuilding()
    {
        if (_buildingPlacementCursor is null)
        {
            return "none";
        }

        var scaffolding = _buildingPlacementCursor.Scaffolding;
        return $"{scaffolding.Name} -> {_buildingPlacementCursor.TargetBuilding.Name} rot {_buildingPlacementCursor.GetDisplayRotationTurns()}";
    }

    private string DescribeRoleRadialMenu()
    {
        if (_roleRadialMenu is null)
        {
            return "closed";
        }

        var targets = string.Join(", ", _roleRadialMenu.Targets.Select(target => target.Name));
        return $"open @ {FormatVector(_roleRadialMenu.CenterScreen)} targets [{targets}]";
    }

    private string DescribeSelectionBox()
    {
        return _selectionBoxBounds is null
            ? "none"
            : $"{_selectionBoxBounds.Value.X}, {_selectionBoxBounds.Value.Y}, {_selectionBoxBounds.Value.Width}, {_selectionBoxBounds.Value.Height}";
    }

    private string FormatResources()
    {
        var stockpile = _resourceStockpileSystem.Refresh(_session);
        return stockpile.Entries.Count == 0
            ? "none"
            : string.Join(", ", stockpile.Entries.Select(entry => $"{entry.ResourceType}={entry.Amount}"));
    }

    private static string FormatBuildingSummary(Cave cave)
    {
        if (cave.Buildings.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "; ",
            cave.Buildings.Select(building =>
                $"{building.Name}@{building.Location?.ToString() ?? "none"} HP {building.Health}/{building.MaxHealth}"));
    }

    private static string FormatTrilobiteSummary(Cave cave)
    {
        if (cave.Trilobites.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "; ",
            cave.Trilobites.Select(trilobite =>
                $"{trilobite.Name}:{trilobite.Assignment}@{trilobite.Location} HP {trilobite.Health}/{trilobite.MaxHealth}"));
    }

    private static string FormatEnemySummary(Cave cave)
    {
        if (cave.Enemies.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "; ",
            cave.Enemies.Select(enemy =>
                $"{enemy.Name}@{enemy.Location} HP {enemy.Health}/{enemy.MaxHealth}"));
    }

    private Tile? GetTileAtScreenPoint(Point point)
    {
        var world = _camera.ScreenToWorld(point);
        var gridPoint = new GridPoint((int)MathF.Round(world.X / TileConstants.TileSize), (int)MathF.Round(world.Y / TileConstants.TileSize));
        return _session.Cave?.GetTile(gridPoint.ToString());
    }

    private bool TryHitTrilobite(Point point, out Trilobite trilobite)
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            trilobite = null!;
            return false;
        }

        foreach (var candidate in cave.GetTrilobiteList())
        {
            if (GetCreatureHitBounds(candidate).Contains(point))
            {
                trilobite = candidate;
                return true;
            }
        }

        trilobite = null!;
        return false;
    }

    private bool TryHitCreature(Point point, out Creature creature)
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            creature = null!;
            return false;
        }

        foreach (var candidate in cave.GetTrilobiteList())
        {
            if (GetCreatureHitBounds(candidate).Contains(point))
            {
                creature = candidate;
                return true;
            }
        }

        foreach (var candidate in cave.GetEnemyList())
        {
            if (GetCreatureHitBounds(candidate).Contains(point))
            {
                creature = candidate;
                return true;
            }
        }

        creature = null!;
        return false;
    }

    private IEnumerable<Tile> GetCandidateTilesForScreenPoint(Point point, Cave cave)
    {
        var world = _camera.ScreenToWorld(point);
        var centerTileX = (int)MathF.Round(world.X / TileConstants.TileSize);
        var centerTileY = (int)MathF.Round(world.Y / TileConstants.TileSize);

        for (var y = centerTileY - 2; y <= centerTileY + 2; y++)
        {
            for (var x = centerTileX - 2; x <= centerTileX + 2; x++)
            {
                var tile = cave.GetTile(new GridPoint(x, y).ToString());
                if (tile is not null)
                {
                    yield return tile;
                }
            }
        }
    }

    private bool TryHitBuilding(Point point, out Building building)
    {
        foreach (var candidate in _session.Cave?.Buildings ?? [])
        {
            if (candidate.TileArray.Any(tile =>
            {
                var tilePoint = GridPoint.Parse(tile.Key);
                return GetTileHitBounds(tilePoint).Contains(point);
            }))
            {
                building = candidate;
                return true;
            }
        }

        building = null!;
        return false;
    }

    private Rectangle GetCreatureHitBounds(Creature creature)
    {
        return GetTileHitBounds(creature.Location);
    }

    private Rectangle GetTileHitBounds(GridPoint tilePoint)
    {
        var halfSize = TileConstants.TileHalfSize * _camera.CurrentScale;
        var screen = _camera.WorldToScreen(new Vector2(tilePoint.X * TileConstants.TileSize, tilePoint.Y * TileConstants.TileSize));
        return new Rectangle(
            (int)MathF.Round(screen.X - halfSize),
            (int)MathF.Round(screen.Y - halfSize),
            (int)MathF.Round(halfSize * 2f),
            (int)MathF.Round(halfSize * 2f));
    }

    private void RegisterTexture(SpriteFactory sprites, string key, string assetName)
    {
        sprites.Register(key, Content.Load<Texture2D>(assetName));
    }

    private void HandleViewportResize()
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0)
        {
            return;
        }

        var oldWidth = (int)_camera.ViewCenter.X * 2;
        var oldHeight = (int)_camera.ViewCenter.Y * 2;
        _camera.HandleViewportResize(oldWidth, oldHeight, Window.ClientBounds.Width, Window.ClientBounds.Height);
    }

    private void SpawnDebugEnemy()
    {
        SpawnDebugEnemies(1);
    }

    private void SpawnDebugEnemies(int amount)
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return;
        }

        var occupiedKeys = cave.GetCreatures()
            .Where(creature => creature.IsTrackedInTileSystem)
            .Select(creature => creature.Location.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var reachable = cave.GetReachableTiles().Where(tile => tile.CreatureFits() && !occupiedKeys.Contains(tile.Key)).ToList();
        if (reachable.Count == 0)
        {
            return;
        }

        var spawnCount = Math.Min(
            Math.Clamp(amount, DebugSpawnAmountControl.MinimumAmount, DebugSpawnAmountControl.MaximumAmount),
            reachable.Count);
        for (var index = 0; index < spawnCount; index++)
        {
            var tileIndex = RandomUtil.NextInt(reachable.Count);
            var spawnTile = reachable[tileIndex];
            reachable[tileIndex] = reachable[^1];
            reachable.RemoveAt(reachable.Count - 1);
            cave.Spawn(
                new Enemy(
                    $"Debug Enemy {_session.Runtime.AllocateDebugEnemyId()}",
                    GridPoint.Parse(spawnTile.Key),
                    _session),
                spawnTile);
        }
    }

    bool IGamePlayHost.IsPaused
    {
        get => _gamePaused;
        set => _gamePaused = value;
    }

    double IGamePlayHost.TickSpeedMs
    {
        get => _tickSpeedMs;
        set => _tickSpeedMs = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trilodexCatalogViewport?.Dispose();
        }

        base.Dispose(disposing);
    }

    private readonly record struct MainMenuWorldGenerationOptionButton(
        WorldGenerationMethod Method,
        string Label,
        Rectangle Bounds,
        bool Selected);

    private readonly record struct RoleRadialButton(
        string? Assignment,
        string Label,
        Rectangle Bounds,
        bool Selected);

    private sealed record RoleRadialMenuState(
        Vector2 CenterScreen,
        Trilobite[] Targets,
        bool AnchorToCreature);
}
