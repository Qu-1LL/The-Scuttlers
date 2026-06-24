using System.Text;
using Gum.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Content;
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
using TriloGame.Game.Core.Vehicles;
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
using TriloGame.Game.UI.Input;
using TriloGame.Game.UI.MainMenu;
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

    private enum ScreenUiPass
    {
        Background,
        Foreground
    }
    private GumService GumUi => GumService.Default;
    private readonly GraphicsDeviceManager _graphics;
    private readonly AudioService _audio = new();
    private readonly MusicService _ost = new();
    private readonly SessionAudioBridge _sessionAudioBridge;
    private readonly SessionScreenShakeBridge _sessionScreenShakeBridge;
    private readonly SessionParticleBridge _sessionParticleBridge;
    private readonly OpalAudioSystem _opalAudioSystem;
    private readonly FocusAudioSystem _focusAudioSystem;
    private readonly InputController _input = new();
    private readonly DoubleClickTracker _manualMoveDoubleClick = new();
    private readonly CameraController _camera = new();
    private readonly MenuController _menu = new();
    private readonly ResearchDraftController _researchDraft = new();
    private readonly GameSessionBootstrapper _bootstrapper = new();
    private readonly GameSimulationClockSystem _simulationClock = new();
    private readonly GameOverStateSystem _gameOverState = new();
    private readonly ResearchDraftSystem _researchDraftSystem = new();
    private readonly DebugToggleControls _debugToggleControls;
    private readonly Func<bool> _stopSimulationAfterTick;
    private GameSession _session = new();
    private readonly HashSet<Trilobite> _selectedTrilobites = [];
    private readonly List<Trilobite> _selectionResultBuffer = [];
    private Trilobite[] _pendingManualMoveTargets = [];

    private SpriteBatch _spriteBatch = null!;
    private RenderingContext _rendering = null!;
    private GumUiRenderer _gumUiRenderer = null!;
    private object? _selectedObject;
    private string? _activeBfsDebugField;
    private bool _debugMenuOpen;
    private bool _mainMenuWorldGenerationDropdownOpen;
    private bool _settingsMenuOpen;
    private bool _resumeSimulationAfterClosingSettings;
    private bool _resumeSimulationAfterClosingResearchDraft;
    private bool _mainMenuOpen;
    private bool _showRoleLabels;
    private bool _showFullMapVisibility;
    private bool _debugAntHolePlacementMode;
    private bool _leftPanActive;
    private bool _selectionDragActive;
    private bool _buildPlacementDragActive;
    private double _uiClockMs;
    private WorldGenerationMethod _worldGenerationMethod = WorldGenerationMethod.Version0;
    private AppScreen _appScreen = AppScreen.MainMenu;
    private ScreenUiPass _screenUiPass = ScreenUiPass.Foreground;
    private Scaffolding? _floatingBuilding;
    private Factory? _activeBuildFactory;
    private GridPoint? _buildPlacementDragStart;
    private Rectangle? _selectionBoxBounds;
    private RoleRadialMenuState? _roleRadialMenu;

    public GameApp()
    {
        _graphics = new GraphicsDeviceManager(this);
        _sessionAudioBridge = new SessionAudioBridge(_audio);
        _sessionScreenShakeBridge = new SessionScreenShakeBridge(_camera);
        _sessionParticleBridge = new SessionParticleBridge(EmitDeathMist);
        _opalAudioSystem = new OpalAudioSystem(_audio);
        _focusAudioSystem = new FocusAudioSystem(_audio);
        _debugToggleControls = new DebugToggleControls(
            value => _showRoleLabels = value,
            value => _session.Runtime.FreezeOpalProgression = value,
            SetAllowManualMining,
            SetFullMapVisibility,
            value => _session.Runtime.DisableEnemySpawns = value,
            value => _session.Runtime.NoCostBuildPlacement = value,
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

    public bool BuildMode => _floatingBuilding is not null;

    private bool BuildPlacementDragActive => _buildPlacementDragActive;

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
        var builder = new StringBuilder();

        builder.AppendLine("[Game]");
        builder.AppendLine($"AppScreen: {_appScreen}");
        builder.AppendLine($"Paused: {_gamePaused}");
        builder.AppendLine($"GameOver: {_isGameOver}");
        builder.AppendLine($"MainMenuOpen: {_mainMenuOpen}");
        builder.AppendLine($"DebugMenuOpen: {_debugMenuOpen}");
        builder.AppendLine($"WorldGenerationMethod: {WorldGenerationMethods.GetDisplayName(_worldGenerationMethod)}");
        builder.AppendLine($"SettingsMenuOpen: {_settingsMenuOpen}");
        builder.AppendLine($"ResearchDraftOpen: {_researchDraft.IsOpen}");
        builder.AppendLine($"BuildMode: {BuildMode}");
        builder.AppendLine($"DebugAntHolePlacementMode: {_debugAntHolePlacementMode}");
        builder.AppendLine($"TickSpeedMs: {_tickSpeedMs}");
        builder.AppendLine($"TickAccumulatorMs: {_tickAccumulatorMs:0.###}");
        builder.AppendLine($"ActiveBfsDebugField: {_activeBfsDebugField ?? "none"}");
        if (GameConstants.EnableOpal)
        {
            builder.AppendLine($"FreezeOpalProgression: {_session.Runtime.FreezeOpalProgression}");
        }

        builder.AppendLine($"DisableEnemySpawns: {_session.Runtime.DisableEnemySpawns}");
        builder.AppendLine($"AllowManualMining: {_session.Runtime.AllowManualMining}");
        builder.AppendLine($"FullMapVisibility: {_showFullMapVisibility}");
        builder.AppendLine($"TickTiming: {FormatTickProfile(_session.Runtime.TickProfiler.Last, "last")}");
        builder.AppendLine($"TickTimingAverage: {FormatTickProfile(_session.Runtime.TickProfiler.Average, "avg")}");
        builder.AppendLine($"Viewport: {Window.ClientBounds.Width}x{Window.ClientBounds.Height}");
        builder.AppendLine($"CameraOrigin: {FormatVector(_camera.CameraOrigin)}");
        builder.AppendLine($"CameraScale: {_camera.CurrentScale:0.###}");
        builder.AppendLine($"CameraViewCenter: {FormatVector(_camera.ViewCenter)}");
        builder.AppendLine();

        builder.AppendLine("[Input]");
        builder.AppendLine($"MousePoint: {_input.MousePoint.X}, {_input.MousePoint.Y}");
        builder.AppendLine($"MouseDelta: {_input.MouseDelta.X}, {_input.MouseDelta.Y}");
        builder.AppendLine($"Dragging: {_input.Dragging}");
        builder.AppendLine($"DragStart: {_input.DragStartPoint.X}, {_input.DragStartPoint.Y}");
        builder.AppendLine($"LeftPanActive: {_leftPanActive}");
        builder.AppendLine($"SelectionDragActive: {_selectionDragActive}");
        builder.AppendLine($"KeysHeld: {FormatPressedKeys()}");
        builder.AppendLine();

        builder.AppendLine("[UI]");
        builder.AppendLine($"MenuPanelOpen: {_menu.PanelOpen}");
        builder.AppendLine($"MenuActiveTab: {_menu.ActiveTab}");
        builder.AppendLine($"MenuAssignmentFilter: {_menu.AssignmentFilter}");
        builder.AppendLine($"MainMenuWorldGenerationDropdownOpen: {_mainMenuWorldGenerationDropdownOpen}");
        builder.AppendLine($"PendingResearchBranches: {_researchDraftSystem.PendingDraft?.Branches.Count ?? 0}");
        builder.AppendLine($"SelectedObject: {DescribeSelectedObject()}");
        builder.AppendLine($"SelectedTrilobites: {FormatSelectedTrilobites()}");
        builder.AppendLine($"FloatingBuilding: {DescribeFloatingBuilding()}");
        builder.AppendLine($"RoleRadialMenu: {DescribeRoleRadialMenu()}");
        builder.AppendLine($"SelectionBox: {DescribeSelectionBox()}");
        builder.AppendLine($"SelectedMiningTiles: {string.Join(", ", _selectedMiningTileKeys)}");
        builder.AppendLine();

        AppendSessionCrashDiagnostics(builder);
        return builder.ToString();
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1440;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.ApplyChanges();
        GumUi.Initialize(this, DefaultVisualsVersion.V2);
        MonoGameAndGum.Renderables.ShapeRenderer.Self.Initialize(GraphicsDevice, Content);
        _gumUiRenderer = new GumUiRenderer();
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
        RegisterTexture(sprites, "empty_Sand", "Textures/EmptyTile_Sand");
        RegisterTexture(sprites, "empty_Lush", "Textures/EmptyTile_Lush");
        RegisterTexture(sprites, "empty_Green", "Textures/EmptyTile_Green");
        RegisterTexture(sprites, "empty_Lava", "Textures/EmptyTile_Lava");
        RegisterTexture(sprites, "wall", "Textures/CaveWall");
        RegisterTexture(sprites, "wall_0", "Textures/wall_0");
        RegisterTexture(sprites, "wall_1", "Textures/wall_1");
        RegisterTexture(sprites, "wall_2", "Textures/wall_2");
        RegisterTexture(sprites, "wall_2_bent", "Textures/wall_2_bent");
        RegisterTexture(sprites, "wall_3", "Textures/wall_3");
        RegisterTexture(sprites, "wall_4", "Textures/wall_4");
        RegisterTexture(sprites, "Algae", "Textures/AlgaeTile");
        RegisterTexture(sprites, "Sandstone", "Textures/SandTile");
        RegisterTexture(sprites, "Malachite", "Textures/MalachiteTile");
        RegisterTexture(sprites, "Magnetite", "Textures/MagnetiteTile");
        if (GameConstants.EnableOpal)
        {
            RegisterTexture(sprites, "Opal", "Textures/Opal");
        }
        RegisterTexture(sprites, "Perotene", "Textures/PeroteneTile");
        RegisterTexture(sprites, "Ilmenite", "Textures/IlmeniteTile");
        RegisterTexture(sprites, "Cochinium", "Textures/CochiniumTile");
        RegisterTexture(sprites, "Trilobite", "Textures/Trilobite");
        RegisterTexture(sprites, "Enemy", "Textures/Enemy");
        RegisterTexture(sprites, "AntHole", "Textures/AntHole");
        RegisterTexture(sprites, "Scaffold", "Textures/Scaffold");
        RegisterTexture(sprites, "Queen", "Textures/Queen");
        RegisterTexture(sprites, "AlgaeFarm", "Textures/AlgaeFarm");
        RegisterTexture(sprites, "SoilTile_0", "Textures/SoilTile_0");
        foreach (var resourceType in GrowableResourceType.GetAll())
        {
            for (var growthLevel = 1; growthLevel <= 3; growthLevel++)
            {
                var textureKey = resourceType.GetSoilTileTextureKey(growthLevel);
                RegisterTexture(sprites, textureKey, $"Textures/{textureKey}");
            }
        }
        RegisterTexture(sprites, "Garage", "Textures/Garage");
        RegisterTexture(sprites, "Silo", "Textures/Silo");
        RegisterTexture(sprites, "Plow", "Textures/Plow");
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
        TryRegisterTexture(sprites, "Rock", "Textures/Rock");

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

        _audio.Register(GameAudioCue.BuildingPlace, Content.Load<SoundEffect>("Audio/Effects/BuildingPlace"));
        _audio.Register(GameAudioCue.BuildingFinished, Content.Load<SoundEffect>("Audio/Effects/BuildingFinished"));
        _audio.Register(GameAudioCue.AntHoleSpawn, Content.Load<SoundEffect>("Audio/Effects/AntHoleSpawn"));
        _audio.Register(GameAudioCue.TrilobiteExplosion, Content.Load<SoundEffect>("Audio/Effects/TrilobiteExplosion"));
        if (GameConstants.EnableOpal)
        {
            _audio.Register(GameAudioCue.OpalChangeStart, Content.Load<SoundEffect>("Audio/Effects/OpalChangeStart"));
            _audio.Register(GameAudioCue.OpalAlarm, Content.Load<SoundEffect>("Audio/Effects/OpalAlarm"));
            _audio.Register(GameAudioCue.OpalRestore, Content.Load<SoundEffect>("Audio/Effects/OpalRestore"));
        }
        _audio.Register(GameAudioCue.TrilobiteBirth, Content.Load<SoundEffect>("Audio/Effects/TrilobiteBirth"));
        _audio.Register(GameAudioCue.TrilobiteSelected, Content.Load<SoundEffect>("Audio/Effects/TrilobiteSelected"));
        _audio.Register(GameAudioCue.UiSelect, Content.Load<SoundEffect>("Audio/Effects/UiSelect"));
        _audio.Register(GameAudioCue.VolumeSound, Content.Load<SoundEffect>("Audio/Effects/VolumeSound"));
        _audio.Register(GameAudioCue.MiningPostFocus, Content.Load<SoundEffect>("Audio/Effects/pickaxe"));
        _audio.Register(GameAudioCue.AlgaeFarmFocus, Content.Load<SoundEffect>("Audio/Effects/mulch"));

        _ost.Register(MusicTrack.PlaceholderTrack, Content.Load<SoundEffect>("Audio/Music/cheerwine_diddy_party"));
        _ost.Register(MusicTrack.AdaptiveTest1, Content.Load<SoundEffect>("Audio/Music/shapes and colors demo1"));
        _ost.Register(MusicTrack.AdaptiveTest2, Content.Load<SoundEffect>("Audio/Music/shapes and colors drumsonly demo1"));
        
    }

    protected override void Update(GameTime gameTime)
    {
        _input.BeginFrame();
        _uiClockMs += gameTime.ElapsedGameTime.TotalMilliseconds;
        _camera.Update(gameTime);
        _ost.Update(gameTime);
        UpdateWorldParticles(gameTime);
        ExpirePendingManualMove();
        SyncSelectionIfRemoved();

        if (_mainMenuOpen)
        {
            HandleMainMenuInput();
            SyncOpalAudioState(gameTime);
            _focusAudioSystem.Reset();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (!_menu.IsRenamingSelectedTrilobite && _input.KeyPressed(Keys.OemTilde))
        {
            ToggleDebugMenu();
        }

        if (HasLostQueen())
        {
            TriggerGameOver();
        }

        if (_isGameOver)
        {
            HandleGameOverInput();
            SyncOpalAudioState(gameTime);
            _focusAudioSystem.Reset();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        _researchDraft.UpdatePointer(_input.MousePoint);
        if (_researchDraft.IsOpen)
        {
            HandleResearchDraftMenuInput();
            SyncOpalAudioState(gameTime);
            _focusAudioSystem.Reset();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (_debugMenuOpen)
        {
            HandleDebugMenuInput();
            AdvanceSimulation(gameTime);
            SyncOpalAudioState(gameTime);
            _focusAudioSystem.Reset();
            GumUi.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        _menu.UpdateHover(_input.MousePoint, Window.ClientBounds.Size, _session);

        var suppressReleaseUi = BuildPlacementDragActive;
        var researchHandled = !suppressReleaseUi && _input.LeftReleased && HandleResearchDraftButtonClick(_input.MousePoint);
        var settingsHandled = !suppressReleaseUi && _input.LeftReleased && !researchHandled && HandleSettingsClick(_input.MousePoint);
        var roundWidgetHandled = !suppressReleaseUi && _input.LeftReleased && !researchHandled && !settingsHandled && HandleRoundDebugWidgetClick(_input.MousePoint);
        if (researchHandled)
        {
            _leftPanActive = false;
            _selectionDragActive = false;
            _selectionDragMode = null;
            _selectionBoxBounds = null;
            _input.EndDrag();
        }
        else if (settingsHandled)
        {
            _leftPanActive = false;
            _selectionDragActive = false;
            _selectionDragMode = null;
            _selectionBoxBounds = null;
            _input.EndDrag();
        }
        else if (roundWidgetHandled)
        {
            _leftPanActive = false;
            _selectionDragActive = false;
            _selectionDragMode = null;
            _selectionBoxBounds = null;
            _input.EndDrag();
        }

        if (_input.WheelDelta != 0 && !IsCameraControlDragBlocked(_input.Dragging, BuildPlacementDragActive))
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
            if (TryBeginBuildPlacementDrag(_input.MousePoint))
            {
                _leftPanActive = false;
            }
            else
            {
                _leftPanActive = CanStartLeftPan(_input.MousePoint);
            }
        }

        if (_input.LeftHeld && BuildPlacementDragActive)
        {
            _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.LeftHeld);
        }

        if (_input.LeftHeld && _leftPanActive)
        {
            _input.UpdateDrag(GameConstants.DragThresholdPixels, _input.LeftHeld);
            if (_input.Dragging)
            {
                _camera.PanByScreenDelta(_input.MouseDelta.X, _input.MouseDelta.Y);
            }
        }

        if (_input.RightPressed)
        {
            if (_roleRadialMenu is null)
            {
                _input.BeginDrag();
                _selectionDragAppend = ControlHeld();
                _selectionDragMode = ResolveSelectionDragMode(_input.MousePoint);
                _selectionDragActive = _selectionDragMode is not null;
                _selectionBoxBounds = _selectionDragActive
                    ? CreateScreenRectangle(_input.MousePoint, _input.MousePoint)
                    : null;
            }
        }

        if (_input.RightHeld)
        {
            if (_roleRadialMenu is null)
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
            if (_selectionDragActive && _input.Dragging)
            {
                FinalizeSelectionBox();
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
            else if (!_input.Dragging &&
                     !_menu.CoversScreenPoint(_input.MousePoint, Window.ClientBounds.Size) &&
                     !SettingsCoversPoint(_input.MousePoint) &&
                     !ResearchDraftCoversPoint(_input.MousePoint))
            {
                HandleWorldRightClick(_input.MousePoint);
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
            else
            {
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
        }

        if (_input.LeftReleased && !settingsHandled && !roundWidgetHandled)
        {
            if (BuildPlacementDragActive)
            {
                TryFinalizeBuildPlacementDrag();

                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                ClearBuildPlacementDrag();
                _input.EndDrag();
            }
            else if (TryHandleMiningOrderMenuClick(_input.MousePoint))
            {
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
            else if (TryHandleRoleRadialClick(_input.MousePoint))
            {
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
            else if (!_input.Dragging &&
                     !SettingsCoversPoint(_input.MousePoint) &&
                     !ResearchDraftCoversPoint(_input.MousePoint) &&
                     !_menu.HandleClick(_input.MousePoint, Window.ClientBounds.Size, this, _session))
            {
                HandleWorldClick(_input.MousePoint);
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
            }
            else
            {
                _leftPanActive = false;
                _selectionDragActive = false;
                _selectionDragMode = null;
                _selectionBoxBounds = null;
                _input.EndDrag();
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
        SyncOpalAudioState(gameTime);
        if (!_settingsMenuOpen)
        {
        _focusAudioSystem.Update(_session, _camera);
        }
        GumUi.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_session.Cave is not null)
        {
            DrawTiles(_session.Cave);
            DrawSurfaceFeatures(_session.Cave);
            DrawDroppedResources(_session.Cave);
            DrawCreatures(_session.Cave, drawBelowBuildings: true);
            DrawBuildings(_session.Cave);
            DrawCreatures(_session.Cave, drawBelowBuildings: false);
            DrawVehicles(_session.Cave);
            DrawProjectiles();
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
            DrawMainMenuOverlayBackground();
            DrawMainMenuOverlayForeground();
            if (_debugMenuOpen)
            {
                DrawMainMenuDebugOverlay();
            }
            DrawSettingsMenu();
        }
        else if (_isGameOver)
        {
            DrawGameOverOverlayBackground();
            DrawGameOverOverlayForeground();
        }
        else
        {
            _menu.Draw(_rendering, Window.ClientBounds.Size, this, _session, _gumUiRenderer);
            DrawSettingsMenu();
            DrawRoleRadialMenu();
            DrawMiningOrderMenu();
            DrawMiningTileHoverLabel();
            DrawFocusHint();
            DrawRoundDebugWidget();
            _researchDraft.Draw(
                Window.ClientBounds.Size,
                _session,
                _researchDraftSystem,
                CanSkipCurrentRoundGracePeriod(),
                _gumUiRenderer);
            DrawStoredResourceTotals();
            if (_debugMenuOpen)
            {
                DrawDebugMenuOverlay();
            }
        }

        _gumUiRenderer.EndFrame();
        GumUi.Draw();
        base.Draw(gameTime);
    }

    public void BeginBuildingPlacement(Factory factory)
    {
        BeginBuildingPlacement(factory, CreatePlacementScaffolding(_session, factory));
    }

    public void BeginBuildingPlacement(Scaffolding scaffolding)
    {
        BeginBuildingPlacement(null, scaffolding);
    }

    private void BeginBuildingPlacement(Factory? sourceFactory, Scaffolding scaffolding)
    {
        ClearPendingManualMove();
        _debugAntHolePlacementMode = false;
        _floatingBuilding = scaffolding;
        _floatingBuilding.SetDisplayRotationTurns(0);
        _activeBuildFactory = sourceFactory;
        ClearBuildPlacementDrag();
    }

    public void CleanActive(bool closeMenu = false)
    {
        ClearActiveState(clearBuildPlacement: true, closeMenu);
    }

    private void ClearActiveState(bool clearBuildPlacement, bool closeMenu)
    {
        ClearPendingManualMove();
        _activeBfsDebugField = null;
        if (clearBuildPlacement)
        {
            _floatingBuilding = null;
            _activeBuildFactory = null;
        }

        _debugAntHolePlacementMode = false;
        _leftPanActive = false;
        _selectionDragActive = false;
        ClearBuildPlacementDrag();
        _selectionDragMode = null;
        _selectionBoxBounds = null;
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
        ResetOpalAudioState();
        ClearWorldParticles();
        CleanActive(true);
        _tickAccumulatorMs = 0d;
        _input.EndDrag();
        StartNewGame();
    }

    private void StartNewGame()
    {
        _appScreen = AppScreen.Gameplay;
        var allowManualMining = _session.Runtime.AllowManualMining;
        var bootstrap = _bootstrapper.CreateNewGame(_worldGenerationMethod);
        _session = bootstrap.Session;
        _session.Runtime.DisableEnemySpawns = false;
        _session.Runtime.AllowManualMining = allowManualMining;
        _sessionAudioBridge.Attach(_session);
        _sessionScreenShakeBridge.Attach(_session);
        _sessionParticleBridge.Attach(_session);
        var spawnX = bootstrap.QueenLocation.X;
        var spawnY = bootstrap.QueenLocation.Y;

        _camera.CurrentScale = 1f;
        _camera.ClearShake();
        _camera.SetOrigin(new Vector2((spawnX * TileConstants.TileSize) + TileConstants.TileSize, (spawnY * TileConstants.TileSize) + TileConstants.TileSize));
        _activeBfsDebugField = null;
        _selectedObject = null;
        _floatingBuilding = null;
        _activeBuildFactory = null;
        _leftPanActive = false;
        _selectionDragActive = false;
        ClearBuildPlacementDrag();
        _selectionDragMode = null;
        _selectionBoxBounds = null;
        _roleRadialMenu = null;
        _selectedTrilobites.Clear();
        ClearMiningTileSelection();
        _mainMenuOpen = false;
        _debugMenuOpen = false;
        _mainMenuWorldGenerationDropdownOpen = false;
        _settingsMenuOpen = false;
        _showRoleLabels = false;
        _showFullMapVisibility = false;
        _simulationClock.ResetToDefaults(paused: false, tickSpeedMs: GameConstants.TickSpeedFast);
        _gameOverState.Reset();
        ResetRoundSystems();
        _researchDraftSystem.Reset();
        _researchDraft.Reset();
        _resumeSimulationAfterClosingResearchDraft = false;
        _uiClockMs = 0d;
        _input.EndDrag();
        ClearPendingManualMove();
        _menu.ResetState();
        ResetOpalAudioState();
        ClearWorldParticles();

        
        _ost.Start(MusicTrack.AdaptiveTest1);
    }

    private void ReturnToMainMenu()
    {
        ResetOpalAudioState();
        ClearWorldParticles();
        CleanActive(true);
        CloseSettingsMenu();
        _ost.Stop();
        _camera.ClearShake();
        _appScreen = AppScreen.MainMenu;
        _gamePaused = true;
        _isGameOver = false;
        _mainMenuOpen = true;
        _debugMenuOpen = false;
        _mainMenuWorldGenerationDropdownOpen = false;
        _showFullMapVisibility = false;
        _researchDraftSystem.Reset();
        _researchDraft.Reset();
        _resumeSimulationAfterClosingResearchDraft = false;
        _tickAccumulatorMs = 0d;
        _input.EndDrag();
    }

    private void TriggerGameOver()
    {
        if (!_gameOverState.TryTrigger(_session))
        {
            return;
        }

        CloseSettingsMenu();
        ForceCloseResearchDraftMenu();
        _gamePaused = true;
        _debugMenuOpen = false;
        _selectionDragActive = false;
        _selectionBoxBounds = null;
        _roleRadialMenu = null;
        _tickAccumulatorMs = 0d;
        _input.EndDrag();
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

        if (_settingsMenuOpen && _input.KeyPressed(Keys.Escape))
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

        if (GetMainMenuStartButtonBounds(viewport).Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            RestartGame();
            return;
        }

        if (GetMainMenuSettingsButtonBounds(viewport).Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            OpenSettingsMenu(pauseSimulationIfNeeded: false);
            return;
        }

        if (GetMainMenuQuitButtonBounds(viewport).Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            Exit();
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
            WorldGenerationMethods.All.Length,
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
        _selectionBoxBounds = null;
        _selectionDragActive = false;
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
        _selectionBoxBounds = null;
        _selectionDragActive = false;
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

    private bool IsMainMenuActive => _appScreen == AppScreen.MainMenu;

    private bool IsScreenUiBackgroundPass => _screenUiPass == ScreenUiPass.Background;

    private bool IsScreenUiForegroundPass => _screenUiPass == ScreenUiPass.Foreground;
    private bool HasGumUiRenderer => _gumUiRenderer is not null;
}

public sealed partial class GameApp
{
    private void SyncOpalAudioState(GameTime gameTime)
    {
        if (!GameConstants.EnableOpal)
        {
            return;
        }

        _opalAudioSystem.Update(_session, gameTime.ElapsedGameTime.TotalMilliseconds);
    }

    private void ResetOpalAudioState()
    {
        if (!GameConstants.EnableOpal)
        {
            return;
        }

        _opalAudioSystem.Reset();
    }

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

        var viewport = Window.ClientBounds.Size;
        if (GetPlayAgainButtonBounds(viewport).Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            RestartGame();
            return;
        }

        if (GetQuitToMainMenuButtonBounds(viewport).Contains(_input.MousePoint))
        {
            PlayUiSelectSound();
            ReturnToMainMenu();
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

        _input.EndDrag();
    }

    private void HandleDebugMenuInput()
    {
        if (_input.KeyPressed(Keys.Escape))
        {
            _debugMenuOpen = false;
            return;
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        if (_debugToggleControls.HandleClick(
                Window.ClientBounds.Size,
                _input.MousePoint,
                _debugMenuOpen,
                _showRoleLabels,
                _session.Runtime.FreezeOpalProgression,
                _session.Runtime.AllowManualMining,
                _showFullMapVisibility,
                _session.Runtime.DisableEnemySpawns,
                _session.Runtime.NoCostBuildPlacement))
        {
            return;
        }

        foreach (var button in BuildDebugMenuButtons(Window.ClientBounds.Size))
        {
            if (button.Enabled && button.Bounds.Contains(_input.MousePoint))
            {
                PlayUiSelectSound();
                InvokeDebugMenuAction(button.Action);
                return;
            }
        }

        HandleRoundDebugWidgetClick(_input.MousePoint);
    }

    private bool SettingsCoversPoint(Point point)
    {
        var viewport = Window.ClientBounds.Size;
        if (SettingsMenuLayout.GetSettingsButtonBounds(viewport).Contains(point))
        {
            return true;
        }

        return _settingsMenuOpen && SettingsMenuLayout.GetPanelBounds(viewport).Contains(point);
    }

    private bool HandleSettingsClick(Point point)
    {
        var viewport = Window.ClientBounds.Size;
        var buttonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        if (buttonBounds.Contains(point))
        {
            PlayUiSelectSound();
            if (_settingsMenuOpen)
            {
                CloseSettingsMenu();
            }
            else
            {
                OpenSettingsMenu();
            }

            return true;
        }

        if (!_settingsMenuOpen)
        {
            return false;
        }

        return HandleSettingsPanelClick(point, allowQuitToMainMenu: true);
    }

    private bool HandleSettingsPanelClick(Point point, bool allowQuitToMainMenu)
    {
        var panelBounds = SettingsMenuLayout.GetPanelBounds(Window.ClientBounds.Size, allowQuitToMainMenu);
        var volumeDownBounds = SettingsMenuLayout.GetVolumeDownButtonBounds(panelBounds);
        var volumeUpBounds = SettingsMenuLayout.GetVolumeUpButtonBounds(panelBounds);
        var volumeBarBounds = SettingsMenuLayout.GetVolumeBarBounds(panelBounds);
        if (SettingsMenuLayout.GetCloseButtonBounds(panelBounds).Contains(point) ||
            SettingsMenuLayout.GetBackButtonBounds(panelBounds).Contains(point))
        {
            PlayUiSelectSound();
            CloseSettingsMenu();
            return true;
        }

        if (volumeDownBounds.Contains(point))
        {
            ChangeVolumeSetting(-SettingsMenuLayout.VolumeStep);
            return true;
        }

        if (volumeUpBounds.Contains(point))
        {
            ChangeVolumeSetting(SettingsMenuLayout.VolumeStep);
            return true;
        }

        if (volumeBarBounds.Contains(point))
        {
            SetVolumeSetting(SettingsMenuLayout.GetSnappedVolumeFromBar(volumeBarBounds, point.X));
            return true;
        }

        if (allowQuitToMainMenu && SettingsMenuLayout.GetReturnToMainMenuButtonBounds(panelBounds).Contains(point))
        {
            PlayUiSelectSound();
            ReturnToMainMenu();
            return true;
        }

        if (!panelBounds.Contains(point))
        {
            CloseSettingsMenu();
            return true;
        }

        return true;
    }

    private void SetVolumeSetting(int volumePercent)
    {
        PlayUiSelectSound();
        if (_audio.SetVolumePercent(volumePercent) | _ost.SetVolumePercent(volumePercent))
        {
            _audio.Play(GameAudioCue.VolumeSound);
        }
    }

    private void ChangeVolumeSetting(int delta)
    {
        SetVolumeSetting(_audio.VolumePercent + delta);
    }

    private void SetAllowManualMining(bool allowManualMining)
    {
        _session.Runtime.AllowManualMining = allowManualMining;
        if (!allowManualMining)
        {
            ClearMiningTileSelection();
        }
    }

    private void SetFullMapVisibility(bool showFullMapVisibility)
    {
        _showFullMapVisibility = showFullMapVisibility;
    }

    private void OpenSettingsMenu(bool pauseSimulationIfNeeded = true)
    {
        if (_settingsMenuOpen)
        {
            return;
        }

        _focusAudioSystem.Reset();

        _ost.CrossfadeTo(MusicTrack.AdaptiveTest2, TimeSpan.FromSeconds(0.5));

        _settingsMenuOpen = true;
        _roleRadialMenu = null;
        _selectionDragActive = false;
        _selectionBoxBounds = null;
        _leftPanActive = false;
        _input.EndDrag();

        _resumeSimulationAfterClosingSettings = false;
        if (pauseSimulationIfNeeded && !_mainMenuOpen && !_gamePaused)
        {
            _gamePaused = true;
            _resumeSimulationAfterClosingSettings = true;
        }
    }

    private void CloseSettingsMenu()
    {
        if (!_settingsMenuOpen)
        {
            return;
        }

        _ost.CrossfadeTo(MusicTrack.AdaptiveTest1, TimeSpan.FromSeconds(0.5));

        _settingsMenuOpen = false;
        if (_resumeSimulationAfterClosingSettings)
        {
            _gamePaused = false;
        }

        _resumeSimulationAfterClosingSettings = false;
    }

    private void InvokeDebugMenuAction(DebugMenuAction action)
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
                return;
            case DebugMenuAction.ToggleRoleLabels:
                _showRoleLabels = !_showRoleLabels;
                return;
            case DebugMenuAction.RestartGame:
                RestartGame();
                return;
            case DebugMenuAction.SpawnEnemy:
                SpawnDebugEnemy();
                RefreshBfsFieldDebug();
                return;
            case DebugMenuAction.SpawnTrilobite:
                SpawnDebugTrilobite();
                RefreshBfsFieldDebug();
                return;
            case DebugMenuAction.PlaceAntHole:
                _debugAntHolePlacementMode = true;
                _floatingBuilding = null;
                _activeBuildFactory = null;
                ClearBuildPlacementDrag();
                _debugMenuOpen = false;
                CloseSettingsMenu();
                return;
            default:
                return;
        }
    }

    private void SyncSelectionIfRemoved()
    {
        _selectedTrilobites.RemoveWhere(trilobite => trilobite.Cave is null || !trilobite.CanBeDirectlySelected());

        if (_roleRadialMenu is not null)
        {
            var remainingTargets = _roleRadialMenu.Targets
                .Where(trilobite => trilobite.Cave is not null && trilobite.CanBeDirectlySelected())
                .Distinct()
                .ToArray();
            _roleRadialMenu = remainingTargets.Length == 0
                ? null
                : _roleRadialMenu with { Targets = remainingTargets };
        }

        if (_selectedObject is SoilAreaSelection soilAreaSelection && !soilAreaSelection.IsStillValid)
        {
            CleanActive();
        }
        else if (_selectedObject is WallSelection wallSelection && !wallSelection.IsStillValid)
        {
            CleanActive();
        }
        else if (_selectedObject is SoilArea soilArea && !soilArea.IsStillValid)
        {
            CleanActive();
        }
        else if (_selectedObject is Building building && building.Cave is null)
        {
            CleanActive();
        }
        else if (_selectedObject is Vehicle vehicle && vehicle.Cave is null)
        {
            CleanActive();
        }
        else if (_selectedObject is Trilobite trilobite && (trilobite.Cave is null || !trilobite.CanBeDirectlySelected()))
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
        else if (_selectedObject is Creature creature && (creature.Cave is null || !creature.CanBeDirectlySelected()))
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
            if (_settingsMenuOpen)
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

        if (_input.KeyPressed(Keys.R) && _floatingBuilding is not null)
        {
            _floatingBuilding.RotateMap();
            var nextRotation = (_floatingBuilding.GetDisplayRotationTurns() + 1) % 4;
            _floatingBuilding.SetDisplayRotationTurns(nextRotation);
            _floatingBuilding.TargetBuilding.SetDisplayRotationTurns(nextRotation);
        }

        if (_input.KeyPressed(Keys.Tab))
        {
            PlayUiSelectSound();
            if (BuildMode)
            {
                CancelActiveBuildingPlacement();
                _menu.OpenBuildingsPanel();
            }
            else
            {
                _menu.TogglePanel();
            }
        }

        if (!_input.Dragging)
        {
            var focusTarget = GetSelectedFocusTarget();
            if (focusTarget is not null && _input.KeyHeld(Keys.F))
            {
                CenterSelection(focusTarget);
                return;
            }
        }

        if (IsCameraControlDragBlocked(_input.Dragging, BuildPlacementDragActive))
        {
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

        if (TryHitVehicle(point, out var vehicle))
        {
            ClearPendingManualMove();
            ClearMiningTileSelection();
            if (!BuildMode)
            {
                SetSelectedObject(ReferenceEquals(_selectedObject, vehicle) ? null : vehicle);
            }

            return;
        }

        if (TryHitBuilding(point, out var building))
        {
            ClearPendingManualMove();
            ClearMiningTileSelection();
            if (!BuildMode)
            {
                var selectionTarget = BuildingSelectionResolver.Resolve(building, _selectedObject);
                CleanActive();
                if (selectionTarget.CanBeSelected())
                {
                    SetSelectedObject(selectionTarget);
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

        if (BuildMode && _floatingBuilding is not null)
        {
            ClearPendingManualMove();
            ClearMiningTileSelection();
            var location = GridPoint.Parse(tile.Key);
            var buildingToPlace = _session.Runtime.NoCostBuildPlacement
                ? _floatingBuilding.TargetBuilding
                : _floatingBuilding;
            if (_session.Cave!.CanBuild(buildingToPlace, location, true))
            {
                var displayRotationTurns = _floatingBuilding.GetDisplayRotationTurns();
                var built = _session.Runtime.NoCostBuildPlacement
                    ? BuildWithoutCost(location, _floatingBuilding)
                    : _session.Cave.Build(_floatingBuilding, location);
                if (built)
                {
                    _audio.Play(GameAudioCue.BuildingPlace);
                    ContinueSelectedBuildingPlacement(displayRotationTurns);
                }
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
            _miningOrderMenu = null;
            return;
        }

        if (BuildMode)
        {
            _roleRadialMenu = null;
            _miningOrderMenu = null;
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
            if (!SelectionRetention.ShouldPreserveCurrentSelection(_selectedMiningTileKeys, tile.Key, StringComparer.Ordinal))
            {
                SelectMiningTile(tile);
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

    private bool CanStartLeftPan(Point point)
    {
        return _roleRadialMenu is null
            && _selectionDragMode is null
            && !_menu.CoversScreenPoint(point, Window.ClientBounds.Size)
            && !SettingsCoversPoint(point)
            && !ResearchDraftCoversPoint(point);
    }

    private bool ShouldStartSelectionDrag(Point point)
    {
        return !BuildMode
            && !_menu.CoversScreenPoint(point, Window.ClientBounds.Size)
            && !SettingsCoversPoint(point)
            && !ResearchDraftCoversPoint(point);
    }

    private void FinalizeSelectionBox()
    {
        if (_selectionBoxBounds is null)
        {
            return;
        }

        var selected = GetTrilobitesInScreenRectangle(_selectionBoxBounds.Value);
        if (selected.Count == 0)
        {
            if (!_selectionDragAppend)
            {
                CleanActive();
            }

            return;
        }

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

    private void DrawTiles(Cave cave)
    {
        foreach (var tile in GetMapVisibleTiles(cave))
        {
            var key = GetTileTextureKey(tile);
            DrawTileTexture(key, tile.Coordinates, GetTileDrawColor(tile));
        }
    }

    private static string GetTileTextureKey(Tile tile)
    {
        if (tile.Base == "wall")
        {
            return "wall";
        }

        return string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(tile.BiomeName)
            ? $"empty_{tile.BiomeName}"
            : tile.Base;
    }

    private void DrawDroppedResources(Cave cave)
    {
        var offsets = new[]
        {
            new Vector2(-18f, -14f),
            new Vector2(0f, -16f),
            new Vector2(18f, -10f),
            new Vector2(-12f, 12f),
            new Vector2(14f, 14f)
        };

        foreach (var tile in GetMapVisibleTiles(cave))
        {
            var droppedSandstone = tile.GetDroppedResourceCount(OreType.SANDSTONE.Name);
            if (droppedSandstone <= 0)
            {
                continue;
            }

            var worldCenter = new Vector2(tile.Coordinates.X * TileConstants.TileSize, tile.Coordinates.Y * TileConstants.TileSize);
            var spriteCount = Math.Min(droppedSandstone, offsets.Length);
            for (var index = 0; index < spriteCount; index++)
            {
                DrawWorldTextureNative(
                    "wall",
                    worldCenter + offsets[index],
                    color: new Color(255, 255, 255, 230),
                    scale: new Vector2(GameConstants.WallDropSpriteScale * _camera.CurrentScale));
            }
        }
    }

    private static Color GetTileDrawColor(Tile tile)
    {
        if (!tile.IsOreTile())
        {
            return Color.White;
        }

        var clampedYield = Math.Clamp(tile.ResourceYield, GameConstants.DarkestOreYield, GameConstants.MaxOreYield);
        var yieldRange = Math.Max(1, GameConstants.MaxOreYield - GameConstants.DarkestOreYield);
        var normalized = (clampedYield - GameConstants.DarkestOreYield) / (float)yieldRange;
        var brightness = 1f - (GameConstants.MaxOreDarkness * (1f - normalized));
        brightness = Math.Clamp(brightness, 1f - GameConstants.MaxOreDarkness, 1f);
        return new Color(brightness, brightness, brightness, 1f);
    }

    private void DrawBuildings(Cave cave)
    {
        foreach (var building in cave.Buildings)
        {
            if (building is Scaffolding scaffold)
            {
                DrawScaffold(scaffold, cave);

                continue;
            }

            if (building is SoilPatch soilPatch)
            {
                DrawSoilPatch(soilPatch);
                continue;
            }

            if (building.Location is null)
            {
                continue;
            }

            DrawWorldTextureNative(
                building.TextureKey,
                GetPlacedBuildingWorldCenter(building),
                building.GetDisplayRotationTurns() * MathF.PI / 2f,
                GetPlacedBuildingOrigin(building));
        }
    }

    private void DrawSoilPatch(SoilPatch soilPatch, Color? tint = null, GridPoint? overrideLocation = null)
    {
        var location = overrideLocation ?? soilPatch.Location;
        if (location is not { } root)
        {
            return;
        }

        var color = tint ?? Color.White;
        foreach (var soilTile in soilPatch.SoilTiles)
        {
            var tileLocation = new GridPoint(
                root.X + soilTile.LocalOffset.X,
                root.Y + soilTile.LocalOffset.Y);
            DrawWorldTextureNative(
                soilTile.TextureKey,
                new Vector2(tileLocation.X * TileConstants.TileSize, tileLocation.Y * TileConstants.TileSize),
                color: color);
        }
    }

    private void DrawVehicles(Cave cave)
    {
        foreach (var vehicle in cave.GetVehicles())
        {
            if (vehicle.Location is null)
            {
                continue;
            }

            DrawWorldTextureNative(
                vehicle.TextureKey,
                ToFrameworkVector(vehicle.GetWorldCenter()),
                vehicle.GetDisplayRotationTurns() * MathF.PI / 2f,
                GetPlacedVehicleOrigin(vehicle));
        }
    }

    private void DrawScaffold(Scaffolding scaffold, Cave cave)
    {
        if (scaffold.Location is not { } location)
        {
            return;
        }

        for (var x = 0; x < scaffold.Size.X; x++)
        {
            for (var y = 0; y < scaffold.Size.Y; y++)
            {
                if (scaffold.OpenMap[y][x] > 1)
                {
                    continue;
                }

                var tilePoint = new GridPoint(location.X + x, location.Y + y);
                var tile = cave.GetTile(tilePoint);
                if (tile is null || !IsMapTileVisible(cave, tile))
                {
                    continue;
                }

                DrawWorldTextureNative(
                    "Scaffold",
                    new Vector2(tilePoint.X * TileConstants.TileSize, tilePoint.Y * TileConstants.TileSize));
            }
        }
    }

    private void DrawCreatures(Cave cave, bool drawBelowBuildings)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            if (!trilobite.IsVisible || trilobite.DrawBelowBuildings != drawBelowBuildings)
            {
                continue;
            }

            DrawWorldTextureNative(
                "Trilobite",
                GetCreatureWorldPosition(trilobite),
                trilobite.RotationRadians);
        }

        foreach (var enemy in cave.Enemies)
        {
            if (!enemy.IsVisible || enemy.DrawBelowBuildings != drawBelowBuildings)
            {
                continue;
            }

            DrawWorldTextureNative(
                "Enemy",
                GetCreatureWorldPosition(enemy),
                enemy.RotationRadians);
        }
    }

    private void DrawProjectiles()
    {
        foreach (var projectileFlight in _session.Runtime.ActiveProjectileFlights)
        {
            var textureKey = _rendering.Sprites.TryGet(projectileFlight.Projectile.SpriteKey, out _)
                ? projectileFlight.Projectile.SpriteKey
                : "wall";
            var worldPosition = ToFrameworkVector(projectileFlight.CurrentWorldPosition);
            var normalizedWorldPosition = worldPosition / TileConstants.TileSize;
            var projectileScale = new Vector2(projectileFlight.Projectile.SpriteScale);
            DrawWorldTexture(
                textureKey,
                normalizedWorldPosition,
                MathHelper.ToRadians(projectileFlight.AngleDegrees),
                projectileScale);
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
            if (!trilobite.CanBeDirectlySelected())
            {
                continue;
            }

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
            if (building is SoilArea soilArea)
            {
                soilArea.RefreshSelectionFootprint(soilArea.Ranch);
            }
            else if (building is SoilAreaSelection soilAreaSelection)
            {
                soilAreaSelection.RefreshSelectionFootprint();
            }
            else if (building is WallSelection wallSelection)
            {
                wallSelection.RefreshSelectionFootprint();
            }

            if (building is MiningPost miningPost && miningPost.Location is not null)
            {
                DrawWorldCircleOutline(
                    GetPlacedBuildingWorldCenter(miningPost),
                    miningPost.Radius * TileConstants.TileSize,
                    new Color(108, 196, 224, 196),
                    MathF.Max(2f, _camera.CurrentScale * 2f));
            }

            DrawTileSelectionOutline(building.TileArray);
        }
        else if (_selectedObject is Vehicle vehicle)
        {
            DrawTileSelectionOutline(vehicle.TileArray);
        }
    }

    private void DrawTileSelectionOutline(IEnumerable<Tile> tiles)
    {
        var tileList = tiles as IReadOnlyCollection<Tile> ?? tiles.ToArray();
        foreach (var tile in tileList)
        {
            var tilePoint = GridPoint.Parse(tile.Key);
            foreach (var neighbor in tile.Neighbors)
            {
                if (tileList.Contains(neighbor))
                {
                    continue;
                }

                var neighborPoint = GridPoint.Parse(neighbor.Key);
                var dx = neighborPoint.X - tilePoint.X;
                var dy = neighborPoint.Y - tilePoint.Y;
                var midpoint = new Vector2(
                    (tilePoint.X * TileConstants.TileSize) + (dx * TileConstants.TileHalfSize),
                    (tilePoint.Y * TileConstants.TileSize) + (dy * TileConstants.TileHalfSize));
                var origin = dy < 0 || dx < 0
                    ? new Vector2(TileConstants.TileHalfSize, 4f)
                    : new Vector2(TileConstants.TileHalfSize, 0f);
                DrawWorldTextureNative(
                    "SelectedEdge",
                    midpoint,
                    dy == 0 ? MathF.PI / 2f : 0f,
                    origin);
            }
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

    private void DrawMainMenu()
    {
        var viewport = Window.ClientBounds.Size;
        var overlayBounds = new Rectangle(0, 0, viewport.X, viewport.Y);
        var cardBounds = MainMenuLayout.GetCardBounds(viewport);
        var titleBounds = MainMenuLayout.GetTitleBounds(cardBounds);
        var startBounds = MainMenuLayout.GetStartGameButtonBounds(cardBounds);
        var quitBounds = MainMenuLayout.GetQuitGameButtonBounds(cardBounds);
        var comingSoonBounds = MainMenuLayout.GetComingSoonBounds(cardBounds);

        if (IsScreenUiBackgroundPass)
        {
            DrawRoundedScreenRect(overlayBounds, new Color(6, 11, 16) * 0.94f, 0);
            DrawRoundedScreenFrame(cardBounds, new Color(16, 30, 42, 244), new Color(141, 199, 219), 3, 22);
            return;
        }

        if (!IsScreenUiForegroundPass)
        {
            return;
        }

        DrawScreenTextFittedCentered(
            "Welcome to The Scuttlers",
            titleBounds,
            Color.White,
            _rendering.UiFont,
            minScale: 0.66f);

        DrawMainMenuButton(
            startBounds,
            "Start Game",
            startBounds.Contains(_input.MousePoint),
            new Color(32, 90, 112),
            new Color(173, 229, 242));
        DrawMainMenuButton(
            quitBounds,
            "Quit Game",
            quitBounds.Contains(_input.MousePoint),
            new Color(86, 54, 42),
            new Color(231, 196, 174));

        DrawScreenTextFittedCentered(
            "Trilo-dex coming soon!",
            comingSoonBounds,
            new Color(181, 206, 217),
            _rendering.SmallFont,
            minScale: 0.7f);
    }

    private void DrawMainMenuButton(Rectangle bounds, string label, bool hovered, Color fill, Color border)
    {
        var buttonFill = hovered
            ? Color.Lerp(fill, Color.White, 0.14f)
            : fill;
        var buttonBorder = hovered
            ? Color.Lerp(border, Color.White, 0.22f)
            : border;

        DrawRoundedScreenFrame(bounds, buttonFill, buttonBorder, 2, 16);
        DrawScreenTextFittedCentered(label, bounds, new Color(246, 251, 253), _rendering.SmallFont, minScale: 0.72f);
    }

    private void DrawSettingsMenu()
    {
        var viewport = Window.ClientBounds.Size;
        if (!_mainMenuOpen)
        {
            var buttonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
            var buttonHovered = buttonBounds.Contains(_input.MousePoint);
            var buttonFill = _settingsMenuOpen
                ? buttonHovered ? new Color(39, 86, 109) : new Color(33, 75, 95)
                : buttonHovered ? new Color(20, 48, 68) : new Color(13, 33, 48);
            var buttonBorder = _settingsMenuOpen
                ? buttonHovered ? new Color(160, 221, 237) : new Color(140, 207, 224)
                : buttonHovered ? new Color(76, 116, 136) : new Color(53, 88, 106);
            var buttonText = _settingsMenuOpen ? Color.White : new Color(214, 231, 239);

            DrawRoundedScreenFrame(buttonBounds, buttonFill, buttonBorder, 2, 14);
            DrawGearIcon(new Rectangle(buttonBounds.X + 12, buttonBounds.Y + 10, 24, 24), buttonText);
            DrawScreenTextFittedCentered(
                "Settings",
                new Rectangle(buttonBounds.X + 40, buttonBounds.Y, buttonBounds.Width - 46, buttonBounds.Height),
                buttonText,
                _rendering.SmallFont,
                minScale: 0.72f);
        }

        if (!_settingsMenuOpen)
        {
            return;
        }

        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: !_mainMenuOpen);
        var closeBounds = SettingsMenuLayout.GetCloseButtonBounds(panelBounds);
        var backBounds = SettingsMenuLayout.GetBackButtonBounds(panelBounds);
        var titleBounds = new Rectangle(panelBounds.X + 20, panelBounds.Y + 16, panelBounds.Width - 40, 26);
        var valueBounds = SettingsMenuLayout.GetVolumeValueBounds(panelBounds);
        var volumeDownBounds = SettingsMenuLayout.GetVolumeDownButtonBounds(panelBounds);
        var volumeUpBounds = SettingsMenuLayout.GetVolumeUpButtonBounds(panelBounds);
        var volumeBarBounds = SettingsMenuLayout.GetVolumeBarBounds(panelBounds);
        var volumeFillBounds = SettingsMenuLayout.GetVolumeFillBounds(volumeBarBounds, _audio.VolumePercent);
        var returnBounds = !_mainMenuOpen ? SettingsMenuLayout.GetReturnToMainMenuButtonBounds(panelBounds) : Rectangle.Empty;
        var volumeDownHovered = volumeDownBounds.Contains(_input.MousePoint);
        var volumeUpHovered = volumeUpBounds.Contains(_input.MousePoint);
        var volumeBarHovered = volumeBarBounds.Contains(_input.MousePoint);
        var closeHovered = closeBounds.Contains(_input.MousePoint);
        var backHovered = backBounds.Contains(_input.MousePoint);
        var returnHovered = !_mainMenuOpen && returnBounds.Contains(_input.MousePoint);

        _gumUiRenderer.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(0, 0, 0, _mainMenuOpen ? 180 : 96));
        DrawRoundedScreenFrame(panelBounds, new Color(8, 19, 29, 247), new Color(77, 122, 140), 3, 16);
        DrawRoundedScreenFrame(
            closeBounds,
            closeHovered ? new Color(36, 64, 82) : new Color(22, 44, 60),
            closeHovered ? new Color(188, 221, 234) : new Color(110, 149, 167),
            2,
            10);
        DrawScreenTextFittedCentered("X", closeBounds, Color.White, _rendering.SmallFont, minScale: 1f);

        DrawScreenTextFittedCentered("Settings", titleBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered(
            $"Volume: {_audio.VolumePercent}%",
            valueBounds,
            new Color(216, 232, 239),
            _rendering.SmallFont,
            minScale: 0.72f);

        DrawRoundedScreenFrame(
            volumeDownBounds,
            volumeDownHovered ? new Color(36, 64, 82) : new Color(22, 44, 60),
            volumeDownHovered ? new Color(188, 221, 234) : new Color(110, 149, 167),
            2,
            10);
        DrawScreenTextFittedCentered("-", volumeDownBounds, Color.White, _rendering.UiFont, minScale: 0.9f);

        DrawRoundedScreenFrame(
            volumeUpBounds,
            volumeUpHovered ? new Color(36, 64, 82) : new Color(22, 44, 60),
            volumeUpHovered ? new Color(188, 221, 234) : new Color(110, 149, 167),
            2,
            10);
        DrawScreenTextFittedCentered("+", volumeUpBounds, Color.White, _rendering.UiFont, minScale: 0.9f);

        DrawRoundedScreenFrame(
            volumeBarBounds,
            volumeBarHovered ? new Color(14, 29, 41) : new Color(10, 22, 32),
            volumeBarHovered ? new Color(159, 209, 224) : new Color(74, 114, 132),
            2,
            12);
        DrawRoundedScreenRect(volumeFillBounds, new Color(143, 205, 226), 10);

        if (!_mainMenuOpen)
        {
            DrawRoundedScreenFrame(
                returnBounds,
                returnHovered ? new Color(82, 113, 96) : new Color(61, 92, 76),
                returnHovered ? new Color(185, 230, 204) : new Color(129, 170, 149),
                2,
                12);
            DrawScreenTextFittedCentered("Return To Main Menu", returnBounds, Color.White, _rendering.SmallFont, minScale: 0.72f);
        }

        DrawRoundedScreenFrame(
            backBounds,
            backHovered ? new Color(32, 61, 80) : new Color(20, 43, 58),
            backHovered ? new Color(180, 219, 233) : new Color(107, 151, 169),
            2,
            12);
        if (_rendering.Sprites.TryGet("BackArrow", out var backArrowTexture))
        {
            _gumUiRenderer.AddSprite(
                new Rectangle(backBounds.X + 9, backBounds.Y + 7, Math.Max(0, backBounds.Width - 18), Math.Max(0, backBounds.Height - 14)),
                backArrowTexture,
                Color.White);
        }
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

        if (_floatingBuilding is null)
        {
            return;
        }

        if (!TryGetBuildPlacementLocations(out var locations) || locations.Count == 0)
        {
            return;
        }

        if (_floatingBuilding.TargetBuilding is SoilPatch soilPatch)
        {
            var canBuildGrid = CanBuildSoilPatchGrid(locations);
            var soilPreviewColor = (canBuildGrid ? Color.White : new Color(255, 96, 96)) * 0.7f;
            for (var index = 0; index < locations.Count; index++)
            {
                DrawSoilPatch(soilPatch, soilPreviewColor, locations[index]);
            }

            return;
        }

        if (_floatingBuilding.TargetBuilding is Wall)
        {
            DrawWallPlacementPreview(locations, CanBuildDraggedIndividualPlacements(locations));
            return;
        }

        DrawBuildingPlacementPreview(locations, CanBuildDraggedIndividualPlacements(locations));
    }

    private void DrawDebugOverlay(Cave cave)
    {
        if (_activeBfsDebugField is null || !_gamePaused)
        {
            return;
        }

        var field = cave.GetBfsField(_activeBfsDebugField);
        if (field is null)
        {
            return;
        }

        foreach (var tile in GetMapVisibleTiles(cave))
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

    private void DrawGameOverOverlayBackground()
    {
        var viewport = Window.ClientBounds.Size;
        var overlayBounds = new Rectangle(0, 0, viewport.X, viewport.Y);
        var cardBounds = GetGameOverCardBounds(viewport);
        var playAgainBounds = GetPlayAgainButtonBounds(viewport);
        var quitBounds = GetQuitToMainMenuButtonBounds(viewport);
        var playAgainHovered = playAgainBounds.Contains(_input.MousePoint);
        var quitHovered = quitBounds.Contains(_input.MousePoint);
        var playAgainFill = playAgainHovered ? new Color(218, 190, 132) : new Color(201, 173, 118);
        var playAgainBorder = playAgainHovered ? new Color(255, 230, 176) : new Color(238, 215, 164);
        var quitFill = quitHovered ? new Color(85, 121, 102) : new Color(67, 102, 84);
        var quitBorder = quitHovered ? new Color(185, 232, 205) : new Color(137, 190, 161);

        DrawRoundedGumRect(overlayBounds, new Color(7, 11, 16) * 0.82f, 0);
        DrawRoundedGumFrame(cardBounds, new Color(18, 31, 42), new Color(196, 172, 121), 2, 18);
        DrawRoundedGumFrame(playAgainBounds, playAgainFill, playAgainBorder, 2, 14);
        DrawRoundedGumFrame(quitBounds, quitFill, quitBorder, 2, 14);
    }

    private void DrawGameOverOverlayForeground()
    {
        var viewport = Window.ClientBounds.Size;
        var cardBounds = GetGameOverCardBounds(viewport);
        var playAgainBounds = GetPlayAgainButtonBounds(viewport);
        var quitBounds = GetQuitToMainMenuButtonBounds(viewport);
        DrawScreenTextFittedCentered("Game Over", new Rectangle(cardBounds.X + 24, cardBounds.Y + 24, cardBounds.Width - 48, 42), Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("The Queen has died.", new Rectangle(cardBounds.X + 24, cardBounds.Y + 76, cardBounds.Width - 48, 24), new Color(255, 214, 150), _rendering.SmallFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Start a fresh colony or return to the main menu.", new Rectangle(cardBounds.X + 24, cardBounds.Y + 104, cardBounds.Width - 48, 34), new Color(171, 198, 208), _rendering.SmallFont, minScale: 0.72f);

        DrawScreenTextFittedCentered("Play Again", playAgainBounds, new Color(10, 23, 34), _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Quit to Main Menu", quitBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
    }

    private void DrawMainMenuOverlayBackground()
    {
        var viewport = Window.ClientBounds.Size;
        var overlayBounds = new Rectangle(0, 0, viewport.X, viewport.Y);
        var startBounds = GetMainMenuStartButtonBounds(viewport);
        var settingsBounds = GetMainMenuSettingsButtonBounds(viewport);
        var quitBounds = GetMainMenuQuitButtonBounds(viewport);
        var startHovered = startBounds.Contains(_input.MousePoint);
        var settingsHovered = settingsBounds.Contains(_input.MousePoint);
        var quitHovered = quitBounds.Contains(_input.MousePoint);

        DrawRoundedGumRect(overlayBounds, Color.Black, 0);
        DrawRoundedGumFrame(
            startBounds,
            startHovered ? new Color(218, 190, 132) : new Color(201, 173, 118),
            startHovered ? new Color(255, 230, 176) : new Color(238, 215, 164),
            2,
            14);
        DrawRoundedGumFrame(
            settingsBounds,
            settingsHovered ? new Color(39, 86, 109) : new Color(33, 75, 95),
            settingsHovered ? new Color(160, 221, 237) : new Color(140, 207, 224),
            2,
            14);
        DrawRoundedGumFrame(
            quitBounds,
            quitHovered ? new Color(85, 121, 102) : new Color(67, 102, 84),
            quitHovered ? new Color(185, 232, 205) : new Color(137, 190, 161),
            2,
            14);
    }

    private void DrawMainMenuOverlayForeground()
    {
        var viewport = Window.ClientBounds.Size;
        var titleBounds = GetMainMenuTitleBounds(viewport);
        var startBounds = GetMainMenuStartButtonBounds(viewport);
        var settingsBounds = GetMainMenuSettingsButtonBounds(viewport);
        var quitBounds = GetMainMenuQuitButtonBounds(viewport);
        var comingSoonBounds = GetMainMenuComingSoonBounds(viewport);
        DrawScreenTextFittedCentered("Welcome to The Scuttlers", titleBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Start Game", startBounds, new Color(10, 23, 34), _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Settings", settingsBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Quit Game", quitBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered("Trilodeck coming soon!", comingSoonBounds, new Color(171, 198, 208), _rendering.SmallFont, minScale: 0.72f);
    }

    private void DrawMainMenuDebugOverlay()
    {
        var viewport = Window.ClientBounds.Size;
        var layout = MainMenuDebugLayout.Build(
            viewport,
            WorldGenerationMethods.All.Length,
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
        DrawDebugInfoCard(
            layout.SummaryBounds,
            "Run State",
            BuildDebugSummaryLines(),
            _rendering.DebugFont,
            new Color(220, 228, 235),
            new Color(10, 19, 28),
            new Color(83, 121, 139));
        DrawDebugPerformanceCard(layout.PerformanceBounds);

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
            _session.Runtime.FreezeOpalProgression,
            _session.Runtime.AllowManualMining,
            _showFullMapVisibility,
            _session.Runtime.DisableEnemySpawns,
            _session.Runtime.NoCostBuildPlacement,
            pointer);
        DrawWrappedScreenText(
            ["` closes this panel. Hotkeys still work."],
            layout.FooterBounds,
            new Color(141, 183, 199),
            _rendering.SmallFont,
            lineGap: 1);

        foreach (var button in BuildDebugMenuButtons(viewport))
        {
            DrawDebugMenuButton(button, button.Bounds.Contains(pointer));
        }
    }

    private void DrawDebugMenuButton(DebugMenuButton button, bool hovered)
    {
        DrawDebugButton(button.Bounds, button.Label, hovered, button.Enabled, button.Selected);
    }

    private void DrawDebugButton(Rectangle bounds, string label, bool hovered, bool enabled, bool selected)
    {
        var fill = new Color(36, 50, 64);
        var border = new Color(96, 120, 138);
        var textColor = Color.White;

        if (!enabled)
        {
            fill = new Color(26, 34, 42);
            border = new Color(60, 70, 80);
            textColor = new Color(128, 139, 148);
        }
        else if (selected)
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

        _gumUiRenderer.AddFilledRectangle(bounds, fill);
        DrawScreenBorder(bounds, border, 2);

        var textBounds = new Rectangle(bounds.X + 8, bounds.Y + 4, Math.Max(0, bounds.Width - 16), Math.Max(0, bounds.Height - 8));
        DrawScreenTextFittedCentered(label, textBounds, textColor, _rendering.SmallFont, minScale: 0.64f);
    }

    private void DrawDebugSectionLabel(Rectangle bounds, string label)
    {
        DrawScreenTextFittedCentered(label, bounds, new Color(255, 214, 150), _rendering.SmallFont, minScale: 0.72f);
    }

    private void DrawDebugInfoCard(
        Rectangle bounds,
        string title,
        IReadOnlyList<string> lines,
        SpriteFont font,
        Color textColor,
        Color fill,
        Color border)
    {
        _gumUiRenderer.AddFilledRectangle(bounds, fill);
        DrawScreenBorder(bounds, border, 1);

        var titleBounds = new Rectangle(bounds.X + 12, bounds.Y + 8, Math.Max(0, bounds.Width - 24), 20);
        DrawScreenTextFittedCentered(title, titleBounds, new Color(255, 214, 150), _rendering.SmallFont, minScale: 0.72f);

        var textBounds = new Rectangle(
            bounds.X + 12,
            titleBounds.Bottom + 8,
            Math.Max(0, bounds.Width - 24),
            Math.Max(0, bounds.Bottom - titleBounds.Bottom - 18));
        DrawWrappedScreenText(lines, textBounds, textColor, font, lineGap: 1);
    }

    private void DrawDebugPerformanceCard(Rectangle bounds)
    {
        _gumUiRenderer.AddFilledRectangle(bounds, new Color(9, 17, 25));
        DrawScreenBorder(bounds, new Color(74, 109, 125), 1);

        var titleBounds = new Rectangle(bounds.X + 12, bounds.Y + 8, Math.Max(0, bounds.Width - 24), 20);
        DrawScreenTextFittedCentered("Performance", titleBounds, new Color(255, 214, 150), _rendering.SmallFont, minScale: 0.72f);

        var contentBounds = new Rectangle(
            bounds.X + 12,
            titleBounds.Bottom + 8,
            Math.Max(0, bounds.Width - 24),
            Math.Max(0, bounds.Bottom - titleBounds.Bottom - 18));

        var workBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, Math.Min(24, contentBounds.Height));
        DrawScreenTextFittedLeft(
            _session.Runtime.TickProfiler.Last.DescribeDominantWorkShort(),
            workBounds,
            new Color(203, 224, 233),
            _rendering.SmallFont,
            minScale: 0.8f);

        var metricsBounds = new Rectangle(
            contentBounds.X,
            workBounds.Bottom + 6,
            contentBounds.Width,
            Math.Max(0, contentBounds.Bottom - workBounds.Bottom - 6));
        var rowCount = 9;
        var rowHeight = Math.Max(18, metricsBounds.Height / rowCount);
        var average = _session.Runtime.TickProfiler.Average;
        var last = _session.Runtime.TickProfiler.Last;
        var rows = new (string Label, string Value)[]
        {
            ("Miner role", FormatRoleTimingMetric(_session.Runtime.TickProfiler.AverageMinerMsPerTrilobite, last.MinerTiming)),
            ("Builder role", FormatRoleTimingMetric(_session.Runtime.TickProfiler.AverageBuilderMsPerTrilobite, last.BuilderTiming)),
            ("Farmer role", FormatRoleTimingMetric(_session.Runtime.TickProfiler.AverageFarmerMsPerTrilobite, last.FarmerTiming)),
            ("Fighter role", FormatRoleTimingMetric(_session.Runtime.TickProfiler.AverageFighterMsPerTrilobite, last.FighterTiming)),
            ("Avg ene", $"{average.EnemyMoveMs:0.00} ms"),
            ("Avg bld", $"{average.BuildingTickMs:0.00} ms"),
            ("Avg total", $"{average.TotalMs:0.00} ms"),
            ("Stats", $"Alloc {FormatByteCount(last.AllocatedBytes)}   GC {last.Gen0Collections}/{last.Gen1Collections}/{last.Gen2Collections}"),
            ("Counts", $"{last.TrilobiteCount} tri  {last.EnemyCount} ene  {last.BuildingCount} bld")
        };

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var rowY = metricsBounds.Y + (rowIndex * rowHeight);
            var rowBounds = new Rectangle(
                metricsBounds.X,
                rowY,
                metricsBounds.Width,
                Math.Max(1, rowIndex == rowCount - 1 ? metricsBounds.Bottom - rowY : rowHeight));

            DrawDebugMetricCell(rowBounds, rows[rowIndex].Label, rows[rowIndex].Value);
        }
    }

    private void DrawDebugMetricCell(Rectangle bounds, string label, string value)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var labelWidth = Math.Min(104, Math.Max(60, bounds.Width / 2));
        var labelBounds = new Rectangle(bounds.X, bounds.Y, labelWidth, bounds.Height);
        var valueBounds = new Rectangle(bounds.X + labelWidth + 6, bounds.Y, Math.Max(0, bounds.Width - labelWidth - 6), bounds.Height);
        DrawScreenTextFittedLeft(label, labelBounds, new Color(156, 187, 199), _rendering.SmallFont, minScale: 0.8f);
        DrawScreenTextFittedLeft(value, valueBounds, new Color(226, 236, 241), _rendering.SmallFont, minScale: 0.8f);
    }

    private IReadOnlyList<string> BuildDebugSummaryLines()
    {
        var lines = new List<string>
        {
            $"Paused: {(_gamePaused ? "Yes" : "No")}    Danger: {(_session.Danger ? "Yes" : "No")}    Tick: {_session.TickCount}",
            $"Tick Speed: {(int)_tickSpeedMs} ms",
            $"BFS View: {(_activeBfsDebugField ?? "none")} (visible while paused)",
            $"Role Labels: {(_showRoleLabels ? "On" : "Off")}    No Cost Build: {(_session.Runtime.NoCostBuildPlacement ? "On" : "Off")}"
        };

        if (GameConstants.EnableOpal)
        {
            lines[3] += $"    Opal Frozen: {(_session.Runtime.FreezeOpalProgression ? "On" : "Off")}";
        }

        return lines;
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
        if (!_rendering.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        var centerWorld = new Vector2(point.X * TileConstants.TileSize, point.Y * TileConstants.TileSize);
        var topLeftWorld = centerWorld - new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);
        var bottomRightWorld = centerWorld + new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);

        var topLeftScreen = _camera.WorldToScreen(topLeftWorld);
        var bottomRightScreen = _camera.WorldToScreen(bottomRightWorld);

        var left = (int)MathF.Floor(topLeftScreen.X);
        var top = (int)MathF.Floor(topLeftScreen.Y);
        var right = (int)MathF.Ceiling(bottomRightScreen.X);
        var bottom = (int)MathF.Ceiling(bottomRightScreen.Y);
        var destination = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));

        _spriteBatch.Draw(texture, destination, color ?? Color.White);
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

    private void DrawGearIcon(Rectangle bounds, Color color)
    {
        if (!HasGumUiRenderer)
        {
            return;
        }

        var iconSize = Math.Min(bounds.Width, bounds.Height);
        if (iconSize <= 0)
        {
            return;
        }

        var centerSize = Math.Max(8, iconSize / 2);
        var toothThickness = Math.Max(2, iconSize / 8);
        var toothLength = Math.Max(3, iconSize / 6);
        var centerBounds = new Rectangle(
            bounds.Center.X - (centerSize / 2),
            bounds.Center.Y - (centerSize / 2),
            centerSize,
            centerSize);
        _gumUiRenderer.AddFilledRectangle(centerBounds, color);

        _gumUiRenderer.AddFilledRectangle(new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Y, toothThickness, toothLength), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Bottom - toothLength, toothThickness, toothLength), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothLength, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);

        var diagonalTooth = Math.Max(3, toothThickness + 1);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X + toothThickness, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X + toothThickness, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
        _gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
    }

    private void DrawRoundedScreenFrame(Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (!HasGumUiRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _gumUiRenderer.AddRoundedFrame(bounds, fill, border, thickness, radius);
    }

    private void DrawRoundedGumFrame(Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        DrawRoundedScreenFrame(bounds, fill, border, thickness, radius);
    }

    private void DrawRoundedGumRect(Rectangle bounds, Color color, int radius)
    {
        DrawRoundedScreenRect(bounds, color, radius);
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
        var location = building.Location ?? GridPoint.Zero;
        return new Vector2(
            (location.X * TileConstants.TileSize) + ((building.Size.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((building.Size.Y - 1) * TileConstants.TileHalfSize));
    }

    private static Vector2 GetPlacedBuildingOrigin(Building building)
    {
        var baseSize = building.GetDisplayPivotBaseSize();
        return new Vector2(baseSize.X * TileConstants.TileHalfSize, baseSize.Y * TileConstants.TileHalfSize);
    }

    private static Vector2 GetPlacedVehicleOrigin(Vehicle vehicle)
    {
        var baseSize = vehicle.Size;
        return new Vector2(baseSize.X * TileConstants.TileHalfSize, baseSize.Y * TileConstants.TileHalfSize);
    }

    private static Vector2 GetFloatingBuildingOrigin(Scaffolding scaffolding)
    {
        var pivotBaseSize = scaffolding.TargetBuilding.GetDisplayPivotBaseSize();
        var width = pivotBaseSize.X * TileConstants.TileSize;
        var height = pivotBaseSize.Y * TileConstants.TileSize;
        var pivotX = (float)TileConstants.TileHalfSize;
        var pivotY = (float)TileConstants.TileHalfSize;

        switch (scaffolding.GetDisplayRotationTurns())
        {
            case 1:
                pivotY = height - TileConstants.TileHalfSize;
                break;
            case 2:
                pivotX = width - TileConstants.TileHalfSize;
                pivotY = height - TileConstants.TileHalfSize;
                break;
            case 3:
                pivotX = width - TileConstants.TileHalfSize;
                break;
        }

        return new Vector2(pivotX, pivotY);
    }

    private static Rectangle GetGameOverCardBounds(Point viewport)
    {
        var width = Math.Min(520, Math.Max(320, viewport.X - 48));
        var height = Math.Min(320, Math.Max(240, viewport.Y - 80));
        return new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
    }

    private static Rectangle GetPlayAgainButtonBounds(Point viewport)
    {
        var cardBounds = GetGameOverCardBounds(viewport);
        const int width = 240;
        const int height = 54;
        return new Rectangle(cardBounds.Center.X - (width / 2), cardBounds.Bottom - 132, width, height);
    }

    private static Rectangle GetQuitToMainMenuButtonBounds(Point viewport)
    {
        var cardBounds = GetGameOverCardBounds(viewport);
        const int width = 240;
        const int height = 54;
        return new Rectangle(cardBounds.Center.X - (width / 2), cardBounds.Bottom - 66, width, height);
    }

    private static Rectangle GetMainMenuTitleBounds(Point viewport)
    {
        var width = Math.Min(760, Math.Max(320, viewport.X - 80));
        return new Rectangle((viewport.X - width) / 2, 82, width, 58);
    }

    private static Rectangle GetMainMenuStartButtonBounds(Point viewport)
    {
        var centerX = viewport.X / 2;
        const int width = 240;
        const int height = 54;
        return new Rectangle(centerX - (width / 2), 246, width, height);
    }

    private static Rectangle GetMainMenuSettingsButtonBounds(Point viewport)
    {
        var startBounds = GetMainMenuStartButtonBounds(viewport);
        return new Rectangle(startBounds.X, startBounds.Bottom + 18, startBounds.Width, startBounds.Height);
    }

    private static Rectangle GetMainMenuQuitButtonBounds(Point viewport)
    {
        var settingsBounds = GetMainMenuSettingsButtonBounds(viewport);
        return new Rectangle(settingsBounds.X, settingsBounds.Bottom + 18, settingsBounds.Width, settingsBounds.Height);
    }

    private static Rectangle GetMainMenuComingSoonBounds(Point viewport)
    {
        var quitBounds = GetMainMenuQuitButtonBounds(viewport);
        return new Rectangle(quitBounds.X - 40, quitBounds.Bottom + 18, quitBounds.Width + 80, 28);
    }

    private Rectangle GetDebugMenuBounds(Point viewport)
    {
        return DebugMenuLayout.Build(viewport).PanelBounds;
    }

    private IReadOnlyList<MainMenuWorldGenerationOptionButton> BuildMainMenuWorldGenerationOptions(Rectangle optionBounds, int gap)
    {
        var bounds = MainMenuDebugLayout.StackRows(optionBounds, WorldGenerationMethods.All.Length, gap);
        var options = new MainMenuWorldGenerationOptionButton[bounds.Count];
        for (var index = 0; index < bounds.Count; index++)
        {
            var method = WorldGenerationMethods.All[index];
            options[index] = new MainMenuWorldGenerationOptionButton(
                method,
                WorldGenerationMethods.GetDisplayName(method),
                bounds[index],
                method == _worldGenerationMethod);
        }

        return options;
    }

    private IReadOnlyList<DebugMenuButton> BuildDebugMenuButtons(Point viewport)
    {
        var layout = DebugMenuLayout.Build(viewport);
        var quickButtons = DebugMenuLayout.SplitRow(layout.QuickControlsRowBounds, 3, layout.ButtonGap);
        var speedButtons = DebugMenuLayout.SplitRow(layout.SpeedRowBounds, 4, layout.ButtonGap);
        var bfsTopButtons = DebugMenuLayout.SplitRow(layout.BfsTopRowBounds, 2, layout.ButtonGap);
        var bfsBottomButtons = DebugMenuLayout.SplitRow(layout.BfsBottomRowBounds, 2, layout.ButtonGap);
        var actionButtons = DebugMenuLayout.SplitRow(layout.ActionsRowBounds, 4, layout.ButtonGap);

        return
        [
            new DebugMenuButton(
                DebugMenuAction.TogglePause,
                _gamePaused ? "Resume" : "Pause",
                quickButtons[0],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.SingleTick,
                "Step Tick",
                quickButtons[1],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.Close,
                "Close",
                quickButtons[2],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.SpeedSlow,
                "500 ms",
                speedButtons[0],
                true,
                TickSpeedMatches(GameConstants.TickSpeedSlow)),
            new DebugMenuButton(
                DebugMenuAction.SpeedNormal,
                "250 ms",
                speedButtons[1],
                true,
                TickSpeedMatches(GameConstants.TickSpeedNormal)),
            new DebugMenuButton(
                DebugMenuAction.SpeedFast,
                "100 ms",
                speedButtons[2],
                true,
                TickSpeedMatches(GameConstants.TickSpeedFast)),
            new DebugMenuButton(
                DebugMenuAction.SpeedFastest,
                "50 ms",
                speedButtons[3],
                true,
                TickSpeedMatches(GameConstants.TickSpeedFastest)),
            new DebugMenuButton(
                DebugMenuAction.ShowQueenField,
                "Queen",
                bfsTopButtons[0],
                true,
                string.Equals(_activeBfsDebugField, "queen", StringComparison.Ordinal)),
            new DebugMenuButton(
                DebugMenuAction.ShowEnemyField,
                "Enemy",
                bfsTopButtons[1],
                true,
                string.Equals(_activeBfsDebugField, "enemy", StringComparison.Ordinal)),
            new DebugMenuButton(
                DebugMenuAction.ShowColonyField,
                "Colony",
                bfsBottomButtons[0],
                true,
                string.Equals(_activeBfsDebugField, "colony", StringComparison.Ordinal)),
            new DebugMenuButton(
                DebugMenuAction.ClearField,
                "Clear",
                bfsBottomButtons[1],
                true,
                _activeBfsDebugField is null),
            new DebugMenuButton(
                DebugMenuAction.RestartGame,
                "Restart Game",
                actionButtons[0],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.SpawnEnemy,
                "Spawn Debug Enemy",
                actionButtons[1],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.SpawnTrilobite,
                "Spawn Trilobite",
                actionButtons[2],
                true,
                false),
            new DebugMenuButton(
                DebugMenuAction.PlaceAntHole,
                "Place Ant Hole",
                actionButtons[3],
                true,
                _debugAntHolePlacementMode)
        ];
    }

    private bool BuildWithoutCost(GridPoint location, Scaffolding scaffolding)
    {
        var targetBuilding = scaffolding.TargetBuilding;
        targetBuilding.SetDisplayRotationTurns(scaffolding.GetDisplayRotationTurns());
        return _session.Cave!.Build(targetBuilding, location);
    }

    // Cancel placement without disturbing world/object selection so Tab can act as a build-only escape hatch.
    private void CancelActiveBuildingPlacement()
    {
        _floatingBuilding = null;
        _activeBuildFactory = null;
        _leftPanActive = false;
        ClearBuildPlacementDrag();
        _input.EndDrag();
        _menu.ClearBuildSelection(clearHover: false);
    }

    // Build placement drags keep camera zoom and keyboard pan active so players can extend footprints off-screen.
    private static bool IsCameraControlDragBlocked(bool dragging, bool buildPlacementDragActive)
    {
        return dragging && !buildPlacementDragActive;
    }

    // Rebuild the active menu selection after placement so repeat placement stays armed.
    private void ContinueSelectedBuildingPlacement(int displayRotationTurns)
    {
        if (_activeBuildFactory is null)
        {
            CleanActive();
            return;
        }

        var nextPlacement = CreatePlacementScaffolding(_session, _activeBuildFactory, displayRotationTurns);
        ClearActiveState(clearBuildPlacement: false, closeMenu: false);
        _floatingBuilding = nextPlacement;
    }

    // Completed builds consume their scaffolding, so repeat placement must mint a fresh copy.
    private static Scaffolding CreatePlacementScaffolding(GameSession session, Factory factory, int displayRotationTurns = 0)
    {
        var normalizedTurns = ((displayRotationTurns % 4) + 4) % 4;
        var scaffolding = new Scaffolding(session, factory.Build(session));
        for (var turn = 0; turn < normalizedTurns; turn++)
        {
            scaffolding.RotateMap();
        }

        scaffolding.SetDisplayRotationTurns(normalizedTurns);
        scaffolding.TargetBuilding.SetDisplayRotationTurns(normalizedTurns);
        return scaffolding;
    }

    // Buildings opt into drag placement through a shared placement interface instead of bespoke mouse code.
    private bool TryBeginBuildPlacementDrag(Point point)
    {
        if (_floatingBuilding?.TargetBuilding is not IBuildPlacementDragTarget ||
            _roleRadialMenu is not null ||
            _menu.CoversScreenPoint(point, Window.ClientBounds.Size) ||
            SettingsCoversPoint(point) ||
            ResearchDraftCoversPoint(point))
        {
            return false;
        }

        var tile = GetTileAtScreenPoint(point);
        if (tile is null)
        {
            return false;
        }

        _buildPlacementDragActive = true;
        _buildPlacementDragStart = tile.Coordinates;
        return true;
    }

    private void ClearBuildPlacementDrag()
    {
        _buildPlacementDragActive = false;
        _buildPlacementDragStart = null;
    }

    private bool TryFinalizeBuildPlacementDrag()
    {
        if (_floatingBuilding?.TargetBuilding is not IBuildPlacementDragTarget ||
            !TryGetBuildPlacementLocations(out var locations) ||
            locations.Count == 0)
        {
            return false;
        }

        return _floatingBuilding.TargetBuilding is SoilPatch
            ? TryFinalizeSoilPatchGridPlacement(locations)
            : TryFinalizeDraggedIndividualPlacement(locations);
    }

    private bool TryFinalizeDraggedIndividualPlacement(IReadOnlyList<GridPoint> locations)
    {
        if (_floatingBuilding is null ||
            _session.Cave is null ||
            !CanBuildDraggedIndividualPlacements(locations))
        {
            return false;
        }

        for (var index = 0; index < locations.Count; index++)
        {
            if (!TryCreateIndependentPlacementScaffolding(out var placement))
            {
                return false;
            }

            var built = _session.Runtime.NoCostBuildPlacement
                ? BuildWithoutCost(locations[index], placement)
                : _session.Cave.Build(placement, locations[index]);
            if (!built)
            {
                return false;
            }
        }

        _audio.Play(GameAudioCue.BuildingPlace);
        ContinueSelectedBuildingPlacement(_floatingBuilding.GetDisplayRotationTurns());
        return true;
    }

    private bool TryFinalizeSoilPatchGridPlacement(IReadOnlyList<GridPoint> locations)
    {
        if (_floatingBuilding?.TargetBuilding is not SoilPatch || _session.Cave is null)
        {
            return false;
        }

        if (!CanBuildSoilPatchGrid(locations) ||
            !TryCreateSoilAreaPlacement(locations, out var soilArea, out var location))
        {
            return false;
        }

        var built = _session.Runtime.NoCostBuildPlacement
            ? _session.Cave.BuildSoilArea(soilArea, location)
            : _session.Cave.Build(new Scaffolding(_session, soilArea), location);

        if (built)
        {
            _audio.Play(GameAudioCue.BuildingPlace);
            ContinueSelectedBuildingPlacement(_floatingBuilding.GetDisplayRotationTurns());
        }

        return built;
    }

    private bool CanBuildSoilPatchGrid(IReadOnlyList<GridPoint> locations)
    {
        if (_floatingBuilding?.TargetBuilding is not SoilPatch ||
            _session.Cave is null ||
            !TryCreateSoilAreaPlacement(locations, out var soilArea, out var location))
        {
            return false;
        }

        if (!_session.Cave.CanBuildSoilArea(soilArea, location, preserveReachability: true))
        {
            return false;
        }

        if (_session.Runtime.NoCostBuildPlacement)
        {
            return true;
        }

        var scaffold = new Scaffolding(_session, soilArea);
        return _session.Cave.CanBuild(scaffold, location, preserveReachability: true);
    }

    private bool TryCreateSoilAreaPlacement(IReadOnlyList<GridPoint> locations, out SoilArea soilArea, out GridPoint location)
    {
        soilArea = new SoilArea(_session);
        location = GridPoint.Zero;
        if (locations.Count == 0)
        {
            return false;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        for (var index = 0; index < locations.Count; index++)
        {
            minX = Math.Min(minX, locations[index].X);
            minY = Math.Min(minY, locations[index].Y);
        }

        location = new GridPoint(minX, minY);
        for (var index = 0; index < locations.Count; index++)
        {
            var soilPatch = new SoilPatch(_session);
            soilArea.AddSoilPatch(soilPatch, new GridPoint(locations[index].X - minX, locations[index].Y - minY));
        }

        return true;
    }

    // Resolve the live placement anchors from the hovered tile so single buildings and drag rows share one snap path.
    private bool TryGetBuildPlacementLocations(out List<GridPoint> locations)
    {
        locations = [];
        if (_floatingBuilding is null)
        {
            return false;
        }

        var endTile = GetTileAtScreenPoint(_input.MousePoint);
        if (endTile is null)
        {
            return false;
        }

        locations = BuildPlacementPreviewResolver.ResolveLocations(
            _floatingBuilding.TargetBuilding,
            endTile.Coordinates,
            BuildPlacementDragActive ? _buildPlacementDragStart : null);
        return locations.Count > 0;
    }

    private bool CanBuildDraggedIndividualPlacements(IReadOnlyList<GridPoint> locations)
    {
        var cave = _session.Cave;
        if (cave is null ||
            locations.Count == 0 ||
            !TryCreatePlacementPreviewBuilding(out var previewBuilding))
        {
            return false;
        }

        var occupiedTiles = new HashSet<GridPoint>();
        for (var index = 0; index < locations.Count; index++)
        {
            for (var x = 0; x < previewBuilding.Size.X; x++)
            {
                for (var y = 0; y < previewBuilding.Size.Y; y++)
                {
                    if (previewBuilding.OpenMap[y][x] > 1)
                    {
                        continue;
                    }

                    if (!occupiedTiles.Add(new GridPoint(locations[index].X + x, locations[index].Y + y)))
                    {
                        return false;
                    }
                }
            }

            if (!cave.CanBuild(previewBuilding, locations[index], preserveReachability: true))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryCreatePlacementPreviewBuilding(out Building previewBuilding)
    {
        previewBuilding = null!;
        if (_floatingBuilding is null)
        {
            return false;
        }

        if (_activeBuildFactory is not null)
        {
            var previewPlacement = CreatePlacementScaffolding(
                _session,
                _activeBuildFactory,
                _floatingBuilding.GetDisplayRotationTurns());
            previewBuilding = _session.Runtime.NoCostBuildPlacement
                ? previewPlacement.TargetBuilding
                : previewPlacement;
            return true;
        }

        previewBuilding = _session.Runtime.NoCostBuildPlacement
            ? _floatingBuilding.TargetBuilding
            : _floatingBuilding;
        return true;
    }

    private bool TryCreateIndependentPlacementScaffolding(out Scaffolding placement)
    {
        placement = null!;
        if (_floatingBuilding is null)
        {
            return false;
        }

        if (_activeBuildFactory is not null)
        {
            placement = CreatePlacementScaffolding(
                _session,
                _activeBuildFactory,
                _floatingBuilding.GetDisplayRotationTurns());
            return true;
        }

        if (_floatingBuilding.TargetBuilding is Wall)
        {
            placement = new Scaffolding(_session, new Wall(_session));
            return true;
        }

        return false;
    }

    private void DrawBuildingPlacementPreview(IReadOnlyList<GridPoint> locations, bool canBuild)
    {
        var previewColor = (canBuild ? Color.White : new Color(255, 96, 96)) * 0.7f;
        for (var index = 0; index < locations.Count; index++)
        {
            DrawFloatingBuildingPreviewAt(locations[index], previewColor);
        }
    }

    private void DrawFloatingBuildingPreviewAt(GridPoint location, Color previewColor)
    {
        if (_floatingBuilding is null)
        {
            return;
        }

        DrawWorldTextureNative(
            _floatingBuilding.TargetBuilding.TextureKey,
            new Vector2(location.X * TileConstants.TileSize, location.Y * TileConstants.TileSize),
            _floatingBuilding.GetDisplayRotationTurns() * MathF.PI / 2f,
            GetFloatingBuildingOrigin(_floatingBuilding),
            previewColor);
    }

    // Preview the snapped line as separate connected wall segments rather than one footprint.
    private void DrawWallPlacementPreview(IReadOnlyList<GridPoint> locations, bool canBuild)
    {
        var previewColor = (canBuild ? Color.White : new Color(255, 96, 96)) * 0.7f;
        for (var index = 0; index < locations.Count; index++)
        {
            var (textureKey, rotationTurns) = ResolveWallPreviewAppearance(locations, index);
            DrawWorldTextureNative(
                textureKey,
                new Vector2(locations[index].X * TileConstants.TileSize, locations[index].Y * TileConstants.TileSize),
                rotationTurns * MathF.PI / 2f,
                color: previewColor);
        }
    }

    private static (string TextureKey, int RotationTurns) ResolveWallPreviewAppearance(IReadOnlyList<GridPoint> locations, int index)
    {
        const int topBit = 1;
        const int rightBit = 2;
        const int bottomBit = 4;
        const int leftBit = 8;

        var connectionMask = 0;
        if (index > 0)
        {
            var previous = locations[index - 1];
            var current = locations[index];
            if (previous.X == current.X)
            {
                connectionMask |= previous.Y < current.Y ? topBit : bottomBit;
            }
            else
            {
                connectionMask |= previous.X < current.X ? leftBit : rightBit;
            }
        }

        if (index + 1 < locations.Count)
        {
            var next = locations[index + 1];
            var current = locations[index];
            if (next.X == current.X)
            {
                connectionMask |= next.Y < current.Y ? topBit : bottomBit;
            }
            else
            {
                connectionMask |= next.X < current.X ? leftBit : rightBit;
            }
        }

        return WallType.Default.ResolveAppearance(connectionMask);
    }

    private void SpawnDebugTrilobite()
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

    private bool TickSpeedMatches(double tickSpeed)
    {
        return Math.Abs(_tickSpeedMs - tickSpeed) < 0.01d;
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
        var topLeft = _camera.ScreenToWorld(new Point(selection.Left, selection.Top));
        var bottomRight = _camera.ScreenToWorld(new Point(selection.Right, selection.Bottom));
        var minTileX = (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X) / TileConstants.TileSize) - 2;
        var minTileY = (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) - 2;
        var maxTileX = (int)MathF.Ceiling(MathF.Max(topLeft.X, bottomRight.X) / TileConstants.TileSize) + 2;
        var maxTileY = (int)MathF.Ceiling(MathF.Max(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) + 2;

        for (var y = minTileY; y <= maxTileY; y++)
        {
            for (var x = minTileX; x <= maxTileX; x++)
            {
                var tile = cave.GetTile(new GridPoint(x, y).ToString());
                if (tile is null)
                {
                    continue;
                }

                foreach (var trilobite in tile.Trilobites)
                {
                    if (trilobite.CanBeDirectlySelected() && selection.Contains(GetCreatureScreenPosition(trilobite)))
                    {
                        _selectionResultBuffer.Add(trilobite);
                    }
                }
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

        return _selectedObject is Creature or Building or Vehicle ? _selectedObject : null;
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
            case Vehicle { Cave: null }:
            case Vehicle { Location: null }:
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
            Vehicle vehicle when vehicle.Location is not null => ToFrameworkVector(vehicle.GetWorldCenter()),
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

    private static string FormatRoleTimingMetric(double averageMsPerTrilobite, RoleTimingSnapshot lastTiming)
    {
        return $"avg {averageMsPerTrilobite:0.00} ms/tri   last X{lastTiming.Count} = {lastTiming.TotalMs:0.00} ms";
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
            Vehicle vehicle => $"Vehicle:{vehicle.Name}@{vehicle.Location?.ToString() ?? "none"}",
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
        if (_floatingBuilding is null)
        {
            return "none";
        }

        return $"{_floatingBuilding.Name} -> {_floatingBuilding.TargetBuilding.Name} rot {_floatingBuilding.GetDisplayRotationTurns()}";
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

    private void AppendSessionCrashDiagnostics(StringBuilder builder)
    {
        builder.AppendLine("[Session]");
        builder.AppendLine($"TickCount: {_session.TickCount}");
        builder.AppendLine($"Danger: {_session.Danger}");
        builder.AppendLine($"DebugEnemyCount: {_session.Runtime.PeekNextDebugEnemyId()}");
        builder.AppendLine($"Resources: {FormatResources()}");

        var cave = _session.Cave;
        if (cave is null)
        {
            builder.AppendLine("Cave: null");
            return;
        }

        builder.AppendLine($"RevealedTiles: {cave.RevealedTiles.Count}");
        builder.AppendLine($"ReachableTiles: {cave.ReachableTiles.Count}");
        builder.AppendLine($"Trilobites: {cave.Trilobites.Count}");
        builder.AppendLine($"Enemies: {cave.Enemies.Count}");
        builder.AppendLine($"Buildings: {cave.Buildings.Count}");
        builder.AppendLine($"TickProfilerLast: {FormatTickProfile(_session.Runtime.TickProfiler.Last, "last")}");
        builder.AppendLine($"TickProfilerAvg: {FormatTickProfile(_session.Runtime.TickProfiler.Average, "avg")}");

        var queen = cave.GetQueenBuilding();
        builder.AppendLine(queen is null
            ? "Queen: missing"
            : $"Queen: {queen.Location?.ToString() ?? "none"} HP {queen.Health}/{queen.MaxHealth}");

        builder.AppendLine($"BuildingSummary: {FormatBuildingSummary(cave)}");
        builder.AppendLine($"TrilobiteSummary: {FormatTrilobiteSummary(cave)}");
        builder.AppendLine($"EnemySummary: {FormatEnemySummary(cave)}");
    }

    private string FormatResources()
    {
        return string.Join(", ", _session.Resources.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
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
            if (candidate.CanBeDirectlySelected() && GetCreatureHitBounds(candidate).Contains(point))
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
            if (candidate.CanBeDirectlySelected() && GetCreatureHitBounds(candidate).Contains(point))
            {
                creature = candidate;
                return true;
            }
        }

        foreach (var candidate in cave.GetEnemyList())
        {
            if (candidate.CanBeDirectlySelected() && GetCreatureHitBounds(candidate).Contains(point))
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

    private bool TryHitVehicle(Point point, out Vehicle vehicle)
    {
        foreach (var candidate in _session.Cave?.GetVehicles() ?? [])
        {
            if (candidate.TileArray.Any(tile =>
            {
                var tilePoint = GridPoint.Parse(tile.Key);
                return GetTileHitBounds(tilePoint).Contains(point);
            }))
            {
                vehicle = candidate;
                return true;
            }
        }

        vehicle = null!;
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

    private void TryRegisterTexture(SpriteFactory sprites, string key, string assetName)
    {
        try
        {
            RegisterTexture(sprites, key, assetName);
        }
        catch (ContentLoadException)
        {
        }
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
        var cave = _session.Cave;
        if (cave is null)
        {
            return;
        }

        var occupiedKeys = cave.GetCreatures()
            .Where(creature => creature.IsTrackedInTileSystem)
            .Select(creature => creature.Location.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var reachable = cave.GetReachableTiles().Where(tile => tile.CreatureFits() && !occupiedKeys.Contains(tile.Key)).ToArray();
        if (reachable.Length == 0)
        {
            return;
        }

        var spawnTile = reachable[RandomUtil.NextInt(reachable.Length)];
        cave.Spawn(new Enemy($"Debug Enemy {_session.Runtime.AllocateDebugEnemyId()}", GridPoint.Parse(spawnTile.Key), _session), spawnTile);
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

    private enum DebugMenuAction
    {
        TogglePause,
        SingleTick,
        SpeedSlow,
        SpeedNormal,
        SpeedFast,
        SpeedFastest,
        ShowQueenField,
        ShowEnemyField,
        ShowColonyField,
        ClearField,
        ToggleRoleLabels,
        RestartGame,
        SpawnEnemy,
        SpawnTrilobite,
        PlaceAntHole,
        Close
    }

    private readonly record struct DebugMenuButton(
        DebugMenuAction Action,
        string Label,
        Rectangle Bounds,
        bool Enabled,
        bool Selected);

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
