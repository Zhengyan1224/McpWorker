using System.Globalization;
using System.Text;
using Terminal.Gui;
using Zhengyan.ChatUI.TUI.Models;
using Zhengyan.ChatUI.TUI.Services;

namespace Zhengyan.ChatUI.TUI.UI;

public sealed class ChatTuiApp
{
    private const int NormalComposeFrameHeight = 15;
    private const int CompactComposeFrameHeight = 8;
    private const int MinimumConversationHeight = 10;
    private const int NarrowLayoutColumnThreshold = 120;

    private static readonly List<string> SupportedImageFileTypes =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp"
    ];

    private readonly McpHostChatClient _chatClient = new();
    private readonly List<ChatTurn> _chatHistory = [];
    private readonly List<ChatImageAttachment> _pendingAttachments = [];
    private readonly List<string> _availableModels = [];

    private TuiAppSettings _appSettings = new();
    private ConfigModels? _configModels;
    private int _selectedTurnIndex = -1;
    private int _turnPreviewLength = 18;
    private bool _isLoadingModels;
    private bool _isSending;
    private string? _startupStatusMessage;
    private StatusKind _startupStatusKind = StatusKind.Info;

    private Window? _window;
    private FrameView? _connectionFrame;
    private FrameView? _conversationFrame;
    private FrameView? _composeFrame;
    private FrameView? _turnsFrame;
    private TabView? _mainTabView;
    private TabView? _detailTabView;
    private TextField? _serverField;
    private TextField? _apiKeyField;
    private TextField? _modelField;
    private TextField? _maxTokensField;
    private TextField? _temperatureField;
    private TextField? _topPField;
    private TextField? _imageUrlField;
    private TextField? _localImagePathField;
    private CheckBox? _useResponsesApiCheckBox;
    private ListView? _availableModelsListView;
    private ListView? _turnsListView;
    private ListView? _pendingAttachmentsListView;
    private TextView? _shortcutsReferenceView;
    private TextView? _userMessageView;
    private TextView? _thinkingMessageView;
    private TextView? _assistantMessageView;
    private TextView? _additionalPropertiesView;
    private TextView? _inputMessageView;
    private Label? _statusLabel;
    private Label? _inputLabel;
    private Label? _imageUrlLabel;
    private Label? _localImagePathLabel;
    private Label? _pendingAttachmentsLabel;
    private Button? _getModelsButton;
    private Button? _useSelectedModelButton;
    private Button? _browseLocalImageButton;
    private Button? _addImageUrlButton;
    private Button? _addLocalImageButton;
    private Button? _removeAttachmentButton;
    private Button? _clearAttachmentsButton;
    private Button? _sendButton;
    private Button? _retryButton;
    private Button? _clearChatButton;
    private Button? _saveSettingsButton;
    private Button? _copyUserButton;
    private Button? _copyThinkingButton;
    private Button? _copyAssistantButton;
    private Button? _copyAdditionalPropertiesButton;
    private TabView.Tab? _chatMainTab;
    private TabView.Tab? _settingsMainTab;
    private TabView.Tab? _shortcutsMainTab;
    private TabView.Tab? _userTab;
    private TabView.Tab? _thinkingTab;
    private TabView.Tab? _assistantTab;
    private TabView.Tab? _additionalPropertiesTab;

    private ColorScheme? _topLevelScheme;
    private ColorScheme? _panelScheme;
    private ColorScheme? _inputScheme;
    private ColorScheme? _readOnlyTextScheme;
    private ColorScheme? _accentScheme;
    private ColorScheme? _secondaryScheme;
    private ColorScheme? _dangerScheme;
    private ColorScheme? _userScheme;
    private ColorScheme? _thinkingScheme;
    private ColorScheme? _assistantScheme;
    private ColorScheme? _additionalPropertiesScheme;
    private ColorScheme? _menuScheme;
    private ColorScheme? _statusBarScheme;
    private ColorScheme? _statusInfoScheme;
    private ColorScheme? _statusSuccessScheme;
    private ColorScheme? _statusErrorScheme;

    public void Run()
    {
        Application.Init();

        try
        {
            LoadAppSettings();
            ConfigureTheme();

            var top = Application.Top;
            top.ColorScheme = _topLevelScheme;
            top.MenuBar = BuildMenuBar();
            top.StatusBar = BuildStatusBar();

            if (top.MenuBar is not null)
            {
                top.MenuBar.ColorScheme = _menuScheme;
            }

            if (top.StatusBar is not null)
            {
                top.StatusBar.ColorScheme = _statusBarScheme;
            }

            _window = new ShortcutWindow("Chat Test UI", HandleGlobalShortcut)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                ColorScheme = _topLevelScheme
            };

            BuildLayout(_window);
            ApplySettingsToFields(_appSettings);
            top.Add(_window);

            RefreshAvailableModels();
            RefreshTurnList();
            RefreshPendingAttachments();
            RefreshConversationViews();
            UpdateControlStates();
            SetStatus(_startupStatusMessage ?? "Ready. Configure McpHost, load models, then chat.", _startupStatusMessage is null ? StatusKind.Info : _startupStatusKind);

            Application.Run();
        }
        finally
        {
            _chatClient.Dispose();
            Application.Shutdown();
        }
    }

    private void ConfigureTheme()
    {
        _topLevelScheme = CreateScheme(Color.Gray, Color.Black, Color.White, Color.DarkGray, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _panelScheme = CreateScheme(Color.Gray, Color.Black, Color.White, Color.DarkGray, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _inputScheme = CreateScheme(Color.White, Color.DarkGray, Color.Black, Color.BrightCyan, Color.BrightCyan, Color.DarkGray, Color.Gray, Color.Black);
        _readOnlyTextScheme = CreateFlatScheme(Color.White, Color.Black);
        _accentScheme = CreateScheme(Color.Black, Color.BrightCyan, Color.Black, Color.White, Color.Black, Color.BrightCyan, Color.DarkGray, Color.Black);
        _secondaryScheme = CreateScheme(Color.Black, Color.Gray, Color.Black, Color.White, Color.Black, Color.Gray, Color.DarkGray, Color.Black);
        _dangerScheme = CreateScheme(Color.White, Color.Red, Color.White, Color.BrightRed, Color.White, Color.Red, Color.DarkGray, Color.Black);
        _userScheme = CreateScheme(Color.Gray, Color.Black, Color.White, Color.DarkGray, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _thinkingScheme = CreateFlatScheme(Color.BrightCyan, Color.Black);
        _assistantScheme = CreateFlatScheme(Color.White, Color.Black);
        _additionalPropertiesScheme = CreateScheme(Color.BrightMagenta, Color.Black, Color.White, Color.DarkGray, Color.BrightMagenta, Color.Black, Color.DarkGray, Color.Black);
        _menuScheme = CreateScheme(Color.Gray, Color.Black, Color.Black, Color.BrightCyan, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _statusBarScheme = CreateScheme(Color.Gray, Color.Black, Color.Black, Color.BrightCyan, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _statusInfoScheme = CreateScheme(Color.BrightCyan, Color.Black, Color.BrightCyan, Color.Black, Color.BrightCyan, Color.Black, Color.DarkGray, Color.Black);
        _statusSuccessScheme = CreateScheme(Color.BrightGreen, Color.Black, Color.BrightGreen, Color.Black, Color.BrightGreen, Color.Black, Color.DarkGray, Color.Black);
        _statusErrorScheme = CreateScheme(Color.BrightRed, Color.Black, Color.BrightRed, Color.Black, Color.BrightRed, Color.Black, Color.DarkGray, Color.Black);
    }

    private void BuildLayout(Window window)
    {
        var useCompactComposeLayout = ShouldUseCompactComposeLayout();
        var useNarrowConversationLayout = Application.Driver.Cols < NarrowLayoutColumnThreshold;
        var composeFrameHeight = useCompactComposeLayout ? CompactComposeFrameHeight : NormalComposeFrameHeight;
        var compactFieldX = useCompactComposeLayout ? 8 : 1;
        _turnPreviewLength = useNarrowConversationLayout ? 14 : 18;

        _mainTabView = new TabView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        var chatTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        var settingsTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        var shortcutsTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        var chatShortcutHintLabel = CreateShortcutHintLabel("F1 Help | F11 Tabs | Ctrl+PgUp/PgDn | Ctrl+1-4 | Ctrl+D Send | Ctrl+R Retry | Ctrl+K Clear");
        var settingsShortcutHintLabel = CreateShortcutHintLabel("F1 Help | F11 Tabs | Ctrl+PgUp/PgDn | Ctrl+G Models | Ctrl+U Apply | Ctrl+B Browse | Ctrl+W Save");
        var shortcutsHintLabel = CreateShortcutHintLabel("Ctrl+PgUp/PgDn tabs | Ctrl+1-4 panes | Ctrl+Q quit");

        _connectionFrame = new FrameView("Settings")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = _panelScheme
        };

        var serverLabel = new Label("Server Endpoint")
        {
            X = 1,
            Y = 0,
            ColorScheme = _panelScheme
        };

        var apiKeyLabel = new Label("API Key")
        {
            X = Pos.Right(serverLabel) + 3,
            Y = 0,
            ColorScheme = _panelScheme
        };

        _serverField = new TextField("http://localhost:9083/mcphost/api/v1")
        {
            X = 1,
            Y = 1,
            Width = 48,
            ColorScheme = _inputScheme
        };

        _apiKeyField = new TextField(string.Empty)
        {
            X = 52,
            Y = 1,
            Width = 24,
            Secret = true,
            ColorScheme = _inputScheme
        };

        _getModelsButton = new Button("Get Models")
        {
            X = 79,
            Y = 1,
            ColorScheme = _accentScheme
        };
        _getModelsButton.Clicked += () => _ = RunActionAsync(GetModelsAsync);

        _useSelectedModelButton = new Button("Use Selected")
        {
            X = Pos.Right(_getModelsButton) + 2,
            Y = 1,
            ColorScheme = _secondaryScheme
        };
        _useSelectedModelButton.Clicked += () => _ = RunActionAsync(UseSelectedModelAsync);

        var modelLabel = new Label("Model")
        {
            X = 1,
            Y = 3,
            ColorScheme = _panelScheme
        };

        var maxTokensLabel = new Label("Max Tokens")
        {
            X = 52,
            Y = 3,
            ColorScheme = _panelScheme
        };

        var temperatureLabel = new Label("Temperature")
        {
            X = 1,
            Y = 5,
            ColorScheme = _panelScheme
        };

        var topPLabel = new Label("Top P")
        {
            X = 20,
            Y = 5,
            ColorScheme = _panelScheme
        };

        _modelField = new TextField(string.Empty)
        {
            X = 1,
            Y = 4,
            Width = 48,
            ColorScheme = _inputScheme
        };

        _maxTokensField = new TextField("4096")
        {
            X = 52,
            Y = 4,
            Width = 12,
            ColorScheme = _inputScheme
        };

        _temperatureField = new TextField("0.9")
        {
            X = 1,
            Y = 6,
            Width = 14,
            ColorScheme = _inputScheme
        };

        _topPField = new TextField("0.9")
        {
            X = 20,
            Y = 6,
            Width = 14,
            ColorScheme = _inputScheme
        };

        _useResponsesApiCheckBox = new CheckBox("Use /v1/responses (off = /v1/chat/completions)")
        {
            X = 67,
            Y = 4,
            Checked = false,
            ColorScheme = _panelScheme
        };

        _saveSettingsButton = new Button("Save Settings")
        {
            X = 38,
            Y = 6,
            ColorScheme = _accentScheme
        };
        _saveSettingsButton.Clicked += SaveSettings;

        var availableModelsLabel = new Label("Available Models")
        {
            X = 1,
            Y = 8,
            ColorScheme = _panelScheme
        };

        _availableModelsListView = new ListView()
        {
            X = 1,
            Y = 9,
            Width = Dim.Fill(2),
            Height = Dim.Fill(5),
            ColorScheme = _inputScheme
        };
        _availableModelsListView.SelectedItemChanged += _ => SyncSelectedModelFromList();

        _connectionFrame.Add(
            serverLabel,
            apiKeyLabel,
            _serverField,
            _apiKeyField,
            _getModelsButton,
            _useSelectedModelButton,
            modelLabel,
            maxTokensLabel,
            temperatureLabel,
            topPLabel,
            _modelField,
            _maxTokensField,
            _temperatureField,
            _topPField,
            _useResponsesApiCheckBox,
            _saveSettingsButton,
            availableModelsLabel,
            _availableModelsListView);
        settingsTabView.Add(settingsShortcutHintLabel, _connectionFrame);

        _shortcutsReferenceView = new ContrastTextView(_readOnlyTextScheme ?? _panelScheme!)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = _readOnlyTextScheme,
            Text = BuildShortcutReferenceText()
        };
        shortcutsTabView.Add(shortcutsHintLabel, _shortcutsReferenceView);

        _conversationFrame = new FrameView("Conversation")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(composeFrameHeight + 1),
            ColorScheme = _panelScheme
        };

        _turnsFrame = new FrameView("Turns")
        {
            X = 0,
            Y = 0,
            Width = useNarrowConversationLayout ? 20 : 24,
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        _turnsListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _inputScheme
        };
        _turnsListView.SelectedItemChanged += _ => RefreshConversationViews();
        _turnsFrame.Add(_turnsListView);

        _detailTabView = new TabView()
        {
            X = Pos.Right(_turnsFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _panelScheme
        };

        var userTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _userScheme
        };

        _copyUserButton = new Button(useNarrowConversationLayout ? "Copy" : "Copy User")
        {
            X = 1,
            Y = 0,
            ColorScheme = _accentScheme
        };
        _copyUserButton.Clicked += CopySelectedUser;

        _userMessageView = new TextView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = _userScheme
        };
        userTabView.Add(_copyUserButton, _userMessageView);

        var thinkingTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _thinkingScheme
        };

        _copyThinkingButton = new Button(useNarrowConversationLayout ? "Copy" : "Copy Thinking")
        {
            X = 1,
            Y = 0,
            ColorScheme = _secondaryScheme
        };
        _copyThinkingButton.Clicked += CopySelectedThinking;

        _thinkingMessageView = new TextView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = _thinkingScheme
        };
        thinkingTabView.Add(_copyThinkingButton, _thinkingMessageView);

        var assistantTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _assistantScheme
        };

        _copyAssistantButton = new Button(useNarrowConversationLayout ? "Copy" : "Copy Assistant")
        {
            X = 1,
            Y = 0,
            ColorScheme = _secondaryScheme
        };
        _copyAssistantButton.Clicked += CopySelectedAssistant;

        _assistantMessageView = new ContrastTextView(_assistantScheme ?? _panelScheme!)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = _assistantScheme
        };
        assistantTabView.Add(_copyAssistantButton, _assistantMessageView);

        var additionalPropertiesTabView = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = _additionalPropertiesScheme
        };

        _copyAdditionalPropertiesButton = new Button("Copy JSON")
        {
            X = 1,
            Y = 0,
            ColorScheme = _secondaryScheme
        };
        _copyAdditionalPropertiesButton.Clicked += CopySelectedAdditionalProperties;

        _additionalPropertiesView = new TextView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
            ColorScheme = _additionalPropertiesScheme
        };
        additionalPropertiesTabView.Add(_copyAdditionalPropertiesButton, _additionalPropertiesView);

        _userTab = new TabView.Tab("User", userTabView);
        _thinkingTab = new TabView.Tab("Thinking", thinkingTabView);
        _assistantTab = new TabView.Tab("Assistant", assistantTabView);
        _additionalPropertiesTab = new TabView.Tab("Additional", additionalPropertiesTabView);

        _detailTabView.AddTab(_userTab, true);
        _detailTabView.AddTab(_thinkingTab, false);
        _detailTabView.AddTab(_assistantTab, false);
        _detailTabView.AddTab(_additionalPropertiesTab, false);

        _conversationFrame.Add(_turnsFrame, _detailTabView);

        _composeFrame = new FrameView("Compose")
        {
            X = 0,
            Y = Pos.Bottom(_conversationFrame),
            Width = Dim.Fill(),
            Height = composeFrameHeight,
            ColorScheme = _panelScheme
        };

        _inputLabel = new Label("Input")
        {
            X = 1,
            Y = 0,
            ColorScheme = _panelScheme
        };

        _inputMessageView = new TextView()
        {
            X = useCompactComposeLayout ? compactFieldX : 1,
            Y = useCompactComposeLayout ? 0 : 1,
            Width = Dim.Fill(2),
            Height = useCompactComposeLayout ? 2 : 3,
            WordWrap = true,
            ColorScheme = _inputScheme
        };

        _imageUrlLabel = new Label(useCompactComposeLayout ? "URL" : "Image URL")
        {
            X = 1,
            Y = useCompactComposeLayout ? 2 : 4,
            ColorScheme = _panelScheme
        };

        _imageUrlField = new TextField(string.Empty)
        {
            X = compactFieldX,
            Y = useCompactComposeLayout ? 2 : 5,
            Width = useCompactComposeLayout
                ? Dim.Fill(useNarrowConversationLayout ? 10 : 12)
                : Dim.Fill(16),
            ColorScheme = _inputScheme
        };

        _addImageUrlButton = new Button(useCompactComposeLayout ? "Add" : "Add URL")
        {
            X = Pos.Right(_imageUrlField) + 1,
            Y = useCompactComposeLayout ? 2 : 5,
            ColorScheme = _secondaryScheme
        };
        _addImageUrlButton.Clicked += AddImageUrl;

        _localImagePathLabel = new Label(useCompactComposeLayout ? "File" : "Local Image Path")
        {
            X = 1,
            Y = useCompactComposeLayout ? 3 : 6,
            ColorScheme = _panelScheme
        };

        _localImagePathField = new TextField(string.Empty)
        {
            X = compactFieldX,
            Y = useCompactComposeLayout ? 3 : 7,
            Width = useCompactComposeLayout
                ? Dim.Fill(useNarrowConversationLayout ? 18 : 20)
                : Dim.Fill(30),
            ColorScheme = _inputScheme
        };

        _browseLocalImageButton = new Button(useCompactComposeLayout ? "Browse" : "Browse...")
        {
            X = Pos.Right(_localImagePathField) + 1,
            Y = useCompactComposeLayout ? 3 : 7,
            ColorScheme = _secondaryScheme
        };
        _browseLocalImageButton.Clicked += BrowseLocalImageFile;

        _addLocalImageButton = new Button(useCompactComposeLayout ? "Add" : "Add Path")
        {
            X = Pos.Right(_browseLocalImageButton) + 1,
            Y = useCompactComposeLayout ? 3 : 7,
            ColorScheme = _secondaryScheme
        };
        _addLocalImageButton.Clicked += AddLocalImage;

        _pendingAttachmentsLabel = new Label(useCompactComposeLayout ? "Imgs" : "Pending Attachments")
        {
            X = 1,
            Y = useCompactComposeLayout ? 4 : 8,
            ColorScheme = _panelScheme
        };

        _pendingAttachmentsListView = new ListView()
        {
            X = compactFieldX,
            Y = useCompactComposeLayout ? 4 : 9,
            Width = useCompactComposeLayout
                ? Dim.Fill(useNarrowConversationLayout ? 16 : 18)
                : Dim.Fill(22),
            Height = useCompactComposeLayout ? 1 : 2,
            ColorScheme = _inputScheme
        };

        _removeAttachmentButton = new Button(useCompactComposeLayout ? "Remove" : "Remove Selected")
        {
            X = Pos.Right(_pendingAttachmentsListView) + 1,
            Y = useCompactComposeLayout ? 4 : 9,
            ColorScheme = _dangerScheme
        };
        _removeAttachmentButton.Clicked += RemoveSelectedPendingAttachment;

        _clearAttachmentsButton = new Button(useCompactComposeLayout ? "Clear" : "Clear Images")
        {
            X = Pos.Right(_pendingAttachmentsListView) + 1,
            Y = useCompactComposeLayout ? 4 : 10,
            ColorScheme = _dangerScheme
        };
        _clearAttachmentsButton.Clicked += ClearPendingAttachments;

        _sendButton = new Button("Send")
        {
            X = 1,
            Y = useCompactComposeLayout ? 5 : 11,
            ColorScheme = _accentScheme
        };
        _sendButton.Clicked += () => _ = RunActionAsync(SendMessageAsync);

        _retryButton = new Button("Retry")
        {
            X = Pos.Right(_sendButton) + 2,
            Y = useCompactComposeLayout ? 5 : 11,
            ColorScheme = _secondaryScheme
        };
        _retryButton.Clicked += () => _ = RunActionAsync(RetryMessageAsync);

        _clearChatButton = new Button("Clear")
        {
            X = Pos.Right(_retryButton) + 2,
            Y = useCompactComposeLayout ? 5 : 11,
            ColorScheme = _dangerScheme
        };
        _clearChatButton.Clicked += ClearChat;

        _statusLabel = new Label(string.Empty)
        {
            X = Pos.Right(_clearChatButton) + (useCompactComposeLayout ? 2 : 4),
            Y = useCompactComposeLayout ? 5 : 11,
            Width = Dim.Fill(),
            ColorScheme = _statusInfoScheme
        };

        _composeFrame.Add(
            _inputLabel,
            _inputMessageView,
            _imageUrlLabel,
            _imageUrlField,
            _addImageUrlButton,
            _localImagePathLabel,
            _localImagePathField,
            _browseLocalImageButton,
            _addLocalImageButton,
            _pendingAttachmentsLabel,
            _pendingAttachmentsListView,
            _removeAttachmentButton,
            _clearAttachmentsButton,
            _sendButton,
            _retryButton,
            _clearChatButton,
            _statusLabel);

        chatTabView.Add(chatShortcutHintLabel, _conversationFrame, _composeFrame);

        _chatMainTab = new TabView.Tab("Chat", chatTabView);
        _settingsMainTab = new TabView.Tab("Settings", settingsTabView);
        _shortcutsMainTab = new TabView.Tab("Shortcuts", shortcutsTabView);
        _mainTabView.AddTab(_chatMainTab, true);
        _mainTabView.AddTab(_settingsMainTab, false);
        _mainTabView.AddTab(_shortcutsMainTab, false);
        WireShortcutPassthrough(
            _mainTabView,
            _detailTabView,
            _serverField,
            _apiKeyField,
            _availableModelsListView,
            _modelField,
            _maxTokensField,
            _temperatureField,
            _topPField,
            _shortcutsReferenceView,
            _turnsListView,
            _userMessageView,
            _thinkingMessageView,
            _assistantMessageView,
            _additionalPropertiesView,
            _inputMessageView,
            _imageUrlField,
            _localImagePathField,
            _pendingAttachmentsListView);
        window.Add(_mainTabView);
    }

    private MenuBar BuildMenuBar()
    {
        return new MenuBar(new MenuBarItem[]
        {
            new(
                "_Connection",
                new MenuItem[]
                {
                    CreateMenuItem("_Get Models", "Fetch models from McpHost.", () => _ = RunActionAsync(GetModelsAsync), CanGetModels, Key.CtrlMask | Key.G),
                    CreateMenuItem("_Use Selected Model", "Switch McpHost to the selected model.", () => _ = RunActionAsync(UseSelectedModelAsync), CanUseSelectedModel, Key.CtrlMask | Key.U),
                    CreateMenuItem("_Save Settings", "Persist the current settings to disk.", SaveSettings, CanSaveSettings, Key.CtrlMask | Key.W),
                    CreateMenuItem("_Toggle Responses API", "Toggle /v1/responses mode.", ToggleResponsesApi, () => _useResponsesApiCheckBox is not null && !_isSending),
                    CreateMenuItem("_Quit", "Exit the TUI.", QuitApplication, () => true, Key.CtrlMask | Key.Q)
                },
                null),
            new(
                "_Chat",
                new MenuItem[]
                {
                    CreateMenuItem("_Send", "Send the current draft.", () => _ = RunActionAsync(SendMessageAsync), CanSendMessage, Key.CtrlMask | Key.D),
                    CreateMenuItem("_Retry", "Retry the latest turn.", () => _ = RunActionAsync(RetryMessageAsync), CanRetryMessage, Key.CtrlMask | Key.R),
                    CreateMenuItem("_Browse Local Image", "Open a file picker for local image attachments.", BrowseLocalImageFile, () => !_isSending, Key.CtrlMask | Key.B),
                    CreateMenuItem("_Clear Chat", "Remove chat history and draft state.", ClearChat, CanClearChat, Key.CtrlMask | Key.K)
                },
                null),
            new(
                "_View",
                new MenuItem[]
                {
                    CreateMenuItem("Switch To _Chat Tab", "Jump to the Chat tab.", ShowChatTab, () => _mainTabView is not null, Key.CtrlMask | Key.PageUp),
                    CreateMenuItem("Switch To _Settings Tab", "Jump to the Settings tab.", ShowSettingsTab, () => _mainTabView is not null, Key.CtrlMask | Key.PageDown),
                    CreateMenuItem("Switch To _Shortcuts Tab", "Jump to the Shortcuts tab.", ShowShortcutsTab, () => _mainTabView is not null, Key.F1),
                    CreateMenuItem("_Cycle Top Tabs", "Cycle Chat / Settings / Shortcuts tabs.", ToggleMainTab, () => _mainTabView is not null, Key.F11),
                    CreateMenuItem("Show _User Pane", "Jump to the User detail tab.", ShowUserDetailTab, () => _detailTabView is not null, Key.CtrlMask | Key.D1),
                    CreateMenuItem("Show T_hinking Pane", "Jump to the Thinking detail tab.", ShowThinkingDetailTab, () => _detailTabView is not null, Key.CtrlMask | Key.D2),
                    CreateMenuItem("Show _Assistant Pane", "Jump to the Assistant detail tab.", ShowAssistantDetailTab, () => _detailTabView is not null, Key.CtrlMask | Key.D3),
                    CreateMenuItem("Show Addit_ional Pane", "Jump to the Additional detail tab.", ShowAdditionalDetailTab, () => _detailTabView is not null, Key.CtrlMask | Key.D4)
                },
                null),
            new(
                "_Copy",
                new MenuItem[]
                {
                    CreateMenuItem("Copy _User", "Copy the selected user's message.", CopySelectedUser, CanCopySelectedUser),
                    CreateMenuItem("Copy _Thinking", "Copy the selected reasoning content.", CopySelectedThinking, CanCopySelectedThinking),
                    CreateMenuItem("Copy _Assistant", "Copy the selected assistant message.", CopySelectedAssistant, CanCopySelectedAssistant),
                    CreateMenuItem("Copy _Additional Properties", "Copy the selected additional properties.", CopySelectedAdditionalProperties, CanCopySelectedAdditionalProperties),
                    CreateMenuItem("Copy _Transcript", "Copy the full conversation transcript.", CopyFullTranscript, CanCopyTranscript)
                },
                null),
            new(
                "_Help",
                new MenuItem[]
                {
                    CreateMenuItem("_Shortcuts", "Show the shortcuts reference tab.", ShowShortcutsTab, () => _mainTabView is not null, Key.F1),
                    CreateMenuItem("_About", "Show TUI feature summary.", ShowAbout, () => true)
                },
                null)
        });
    }

    private StatusBar BuildStatusBar()
    {
        return new StatusBar(new[]
        {
            new StatusItem(Key.F1, "~F1~ Keys", ShowShortcutsTab, () => _mainTabView is not null),
            new StatusItem(Key.CtrlMask | Key.G, "~Ctrl+G~ Models", () => _ = RunActionAsync(GetModelsAsync), CanGetModels),
            new StatusItem(Key.CtrlMask | Key.U, "~Ctrl+U~ Use", () => _ = RunActionAsync(UseSelectedModelAsync), CanUseSelectedModel),
            new StatusItem(Key.CtrlMask | Key.B, "~Ctrl+B~ Browse", BrowseLocalImageFile, () => !_isSending),
            new StatusItem(Key.CtrlMask | Key.D, "~Ctrl+D~ Send", () => _ = RunActionAsync(SendMessageAsync), CanSendMessage),
            new StatusItem(Key.CtrlMask | Key.R, "~Ctrl+R~ Retry", () => _ = RunActionAsync(RetryMessageAsync), CanRetryMessage),
            new StatusItem(Key.CtrlMask | Key.W, "~Ctrl+W~ Save", SaveSettings, CanSaveSettings),
            new StatusItem(Key.CtrlMask | Key.K, "~Ctrl+K~ Clear", ClearChat, CanClearChat),
            new StatusItem(Key.F11, "~F11~ Top Tabs", ToggleMainTab, () => _mainTabView is not null),
            new StatusItem(Key.CtrlMask | Key.Q, "~Ctrl+Q~ Quit", QuitApplication, () => true)
        });
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
            QueueUi(() => MessageBox.ErrorQuery("Error", ex.Message, "OK"));
        }
    }

    private void LoadAppSettings()
    {
        try
        {
            _appSettings = TuiSettingsStore.Load();
        }
        catch (Exception ex)
        {
            _appSettings = new TuiAppSettings();
            _startupStatusMessage = $"Failed to load settings: {ex.Message}";
            _startupStatusKind = StatusKind.Error;
        }
    }

    private void ApplySettingsToFields(TuiAppSettings settings)
    {
        SetFieldText(_serverField, settings.ServerEndpoint);
        SetFieldText(_apiKeyField, settings.ApiKey);
        SetFieldText(_modelField, settings.Model);
        SetFieldText(_maxTokensField, settings.MaxTokens.ToString(CultureInfo.InvariantCulture));
        SetFieldText(_temperatureField, settings.Temperature.ToString("0.###", CultureInfo.InvariantCulture));
        SetFieldText(_topPField, settings.TopP.ToString("0.###", CultureInfo.InvariantCulture));

        if (_useResponsesApiCheckBox is not null)
        {
            _useResponsesApiCheckBox.Checked = settings.UseResponsesApi;
        }
    }

    private TuiAppSettings CaptureSettingsFromFields()
    {
        return new TuiAppSettings
        {
            ServerEndpoint = GetServerEndpoint(),
            ApiKey = GetApiKey(),
            Model = GetCurrentModel(),
            MaxTokens = ParseMaxTokens(),
            Temperature = ParseTemperature(),
            TopP = ParseTopP(),
            UseResponsesApi = GetUseResponsesApi()
        };
    }

    private async Task GetModelsAsync()
    {
        if (!CanGetModels())
        {
            return;
        }

        SetLoadingModels(true, "Loading models...");

        try
        {
            var config = await _chatClient.GetModelsAsync(GetServerEndpoint(), GetApiKey());
            var preferredModel = GetCurrentModel();
            await InvokeOnUiAsync(() =>
            {
                _configModels = config;
                _availableModels.Clear();
                _availableModels.AddRange(config.Models.Select(item => item.Name));

                var selectedIndex = _availableModels.FindIndex(item => string.Equals(item, preferredModel, StringComparison.Ordinal));
                if (selectedIndex < 0)
                {
                    selectedIndex = Math.Clamp(config.Current, 0, Math.Max(_availableModels.Count - 1, 0));
                }

                RefreshAvailableModels();
                if (_availableModels.Count > 0)
                {
                    SetModelFieldValue(_availableModels[selectedIndex]);
                    _availableModelsListView!.SelectedItem = selectedIndex;
                }

                SetStatus($"Loaded {_availableModels.Count} models.", StatusKind.Success);
            });
        }
        finally
        {
            await InvokeOnUiAsync(() => SetLoadingModels(false));
        }
    }

    private async Task UseSelectedModelAsync()
    {
        if (!CanUseSelectedModel())
        {
            SetStatus("No models loaded.", StatusKind.Error);
            return;
        }

        var selectedIndex = _availableModelsListView!.SelectedItem;
        if (selectedIndex < 0 || selectedIndex >= _availableModels.Count)
        {
            SetStatus("Select a model from the list.", StatusKind.Error);
            return;
        }

        SetModelFieldValue(_availableModels[selectedIndex]);
        if (_configModels!.Current == selectedIndex)
        {
            SetStatus("Model already selected.", StatusKind.Info);
            return;
        }

        SetLoadingModels(true, "Switching model...");

        try
        {
            await _chatClient.SwitchModelAsync(GetServerEndpoint(), GetApiKey(), selectedIndex);
            await InvokeOnUiAsync(() =>
            {
                _configModels!.Current = selectedIndex;
                SetStatus($"Switched model to {_availableModels[selectedIndex]}.", StatusKind.Success);
            });
        }
        finally
        {
            await InvokeOnUiAsync(() => SetLoadingModels(false));
        }
    }

    private async Task SendMessageAsync()
    {
        if (_isSending)
        {
            return;
        }

        var userMessage = GetText(_inputMessageView).Trim();
        if (string.IsNullOrWhiteSpace(userMessage) && _pendingAttachments.Count == 0)
        {
            SetStatus("Input message and attachments are both empty.", StatusKind.Error);
            return;
        }

        var model = GetCurrentModel();
        if (string.IsNullOrWhiteSpace(model))
        {
            SetStatus("No model selected.", StatusKind.Error);
            return;
        }

        var attachments = _pendingAttachments.Select(item => item.Clone()).ToList();
        ClearComposerInputs();

        await ProcessChatTurnAsync(userMessage, attachments, model);
    }

    private async Task RetryMessageAsync()
    {
        if (_isSending)
        {
            return;
        }

        if (_chatHistory.Count == 0)
        {
            SetStatus("No chat history available for regeneration.", StatusKind.Error);
            return;
        }

        var lastTurn = _chatHistory[^1];
        var userMessage = lastTurn.UserMessage;
        var attachments = lastTurn.Attachments.Select(item => item.Clone()).ToList();

        _chatHistory.RemoveAt(_chatHistory.Count - 1);
        if (_selectedTurnIndex >= _chatHistory.Count)
        {
            _selectedTurnIndex = _chatHistory.Count - 1;
        }

        RefreshTurnList();
        RefreshConversationViews();

        await ProcessChatTurnAsync(userMessage, attachments, GetCurrentModel());
    }

    private async Task ProcessChatTurnAsync(string userMessage, List<ChatImageAttachment> attachments, string model)
    {
        var turn = new ChatTurn
        {
            UserMessage = userMessage,
            AssistantMessage = string.Empty
        };

        foreach (var attachment in attachments)
        {
            turn.Attachments.Add(attachment);
        }

        _chatHistory.Add(turn);
        _selectedTurnIndex = _chatHistory.Count - 1;

        RefreshTurnList();
        RefreshConversationViews();
        SetSendingState(true, "Streaming response...");

        try
        {
            await _chatClient.StreamChatAsync(
                GetServerEndpoint(),
                GetApiKey(),
                model,
                GetUseResponsesApi(),
                ParseMaxTokens(),
                ParseTemperature(),
                ParseTopP(),
                _chatHistory,
                turn,
                QueueConversationRefresh);

            await InvokeOnUiAsync(() => SetStatus("Response completed.", StatusKind.Success));
        }
        finally
        {
            await InvokeOnUiAsync(() => SetSendingState(false));
        }
    }

    private void AddImageUrl()
    {
        try
        {
            var attachment = _chatClient.CreateUrlAttachment(GetFieldText(_imageUrlField));
            AddPendingAttachment(attachment);
            SetFieldText(_imageUrlField, string.Empty);
            SetStatus("Image URL added.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
        }
    }

    private void AddLocalImage()
    {
        try
        {
            var attachment = _chatClient.CreateLocalAttachment(GetFieldText(_localImagePathField));
            AddPendingAttachment(attachment);
            SetFieldText(_localImagePathField, string.Empty);
            SetStatus("Local image added.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
        }
    }

    private void BrowseLocalImageFile()
    {
        if (_isSending)
        {
            return;
        }

        var openDialog = new OpenDialog(
            "Select Local Image",
            "Choose an image to attach.",
            new List<string>(SupportedImageFileTypes),
            OpenDialog.OpenMode.File)
        {
            CanChooseDirectories = false,
            CanChooseFiles = true,
            AllowsMultipleSelection = false,
            AllowsOtherFileTypes = false,
            DirectoryPath = ResolveInitialImageDirectory()
        };

        Application.Run(openDialog);

        var selectedPath = openDialog.FilePaths?.FirstOrDefault();
        if (openDialog.Canceled || string.IsNullOrWhiteSpace(selectedPath))
        {
            SetStatus("Local image selection canceled.", StatusKind.Info);
            return;
        }

        SetFieldText(_localImagePathField, selectedPath);
        AddLocalImage();
    }

    private void AddPendingAttachment(ChatImageAttachment attachment)
    {
        if (_pendingAttachments.Any(existing => string.Equals(existing.Source, attachment.Source, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("This image has already been added.", StatusKind.Error);
            return;
        }

        _pendingAttachments.Add(attachment);
        RefreshPendingAttachments();
    }

    private void RemoveSelectedPendingAttachment()
    {
        if (_pendingAttachments.Count == 0)
        {
            SetStatus("No pending attachments.", StatusKind.Error);
            return;
        }

        var selectedIndex = _pendingAttachmentsListView!.SelectedItem;
        if (selectedIndex < 0 || selectedIndex >= _pendingAttachments.Count)
        {
            SetStatus("Select an attachment to remove.", StatusKind.Error);
            return;
        }

        _pendingAttachments.RemoveAt(selectedIndex);
        RefreshPendingAttachments();
        SetStatus("Attachment removed.", StatusKind.Success);
    }

    private void ClearPendingAttachments()
    {
        _pendingAttachments.Clear();
        RefreshPendingAttachments();
        SetFieldText(_imageUrlField, string.Empty);
        SetFieldText(_localImagePathField, string.Empty);
        SetStatus("Pending attachments cleared.", StatusKind.Success);
    }

    private void ClearChat()
    {
        _chatHistory.Clear();
        _selectedTurnIndex = -1;

        ClearComposerInputs();
        RefreshTurnList();
        RefreshConversationViews();
        SetStatus("Chat history cleared.", StatusKind.Success);
    }

    private void CopySelectedUser()
    {
        var turn = GetSelectedTurn();
        if (turn is null)
        {
            SetStatus("No selected turn to copy.", StatusKind.Error);
            return;
        }

        CopyToClipboard(BuildUserDisplayText(turn), "Copied selected user content.");
    }

    private void CopySelectedThinking()
    {
        var turn = GetSelectedTurn();
        if (turn is null || string.IsNullOrWhiteSpace(turn.AssistantReasoning))
        {
            SetStatus("No reasoning content to copy.", StatusKind.Error);
            return;
        }

        CopyToClipboard(turn.AssistantReasoning, "Copied selected reasoning content.");
    }

    private void CopySelectedAssistant()
    {
        var turn = GetSelectedTurn();
        if (turn is null || string.IsNullOrWhiteSpace(turn.AssistantMessage))
        {
            SetStatus("No assistant message to copy.", StatusKind.Error);
            return;
        }

        CopyToClipboard(turn.AssistantMessage, "Copied selected assistant message.");
    }

    private void CopySelectedAdditionalProperties()
    {
        var turn = GetSelectedTurn();
        if (turn is null || string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
        {
            SetStatus("No additional properties to copy.", StatusKind.Error);
            return;
        }

        CopyToClipboard(turn.AssistantAdditionalProperties, "Copied selected additional properties.");
    }

    private void CopyFullTranscript()
    {
        if (_chatHistory.Count == 0)
        {
            SetStatus("No chat history to copy.", StatusKind.Error);
            return;
        }

        CopyToClipboard(BuildFullTranscript(), "Copied full transcript.");
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Nothing to copy.", StatusKind.Error);
            return;
        }

        if (!Clipboard.IsSupported)
        {
            SetStatus("Clipboard is not available in this terminal session.", StatusKind.Error);
            return;
        }

        if (!Clipboard.TrySetClipboardData(text))
        {
            SetStatus("Failed to write to the OS clipboard.", StatusKind.Error);
            return;
        }

        SetStatus(successMessage, StatusKind.Success);
    }

    private void ToggleResponsesApi()
    {
        if (_useResponsesApiCheckBox is null || _isSending)
        {
            return;
        }

        _useResponsesApiCheckBox.Checked = !_useResponsesApiCheckBox.Checked;
        SetStatus(
            _useResponsesApiCheckBox.Checked == true
                ? "Responses API enabled."
                : "Chat completions API enabled.",
            StatusKind.Info);
    }

    private void SaveSettings()
    {
        try
        {
            _appSettings = CaptureSettingsFromFields();
            var path = TuiSettingsStore.Save(_appSettings);
            SetStatus($"Settings saved: {path}", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
        }
    }

    private void ShowChatTab()
    {
        SelectMainTab(_chatMainTab, _turnsListView, "Switched to Chat tab.");
    }

    private void ShowSettingsTab()
    {
        SelectMainTab(_settingsMainTab, _serverField, "Switched to Settings tab.");
    }

    private void ShowShortcutsTab()
    {
        SelectMainTab(_shortcutsMainTab, _shortcutsReferenceView, "Switched to Shortcuts tab.");
    }

    private void ToggleMainTab()
    {
        if (_mainTabView?.SelectedTab == _chatMainTab)
        {
            ShowSettingsTab();
            return;
        }

        if (_mainTabView?.SelectedTab == _settingsMainTab)
        {
            ShowShortcutsTab();
            return;
        }

        if (_mainTabView?.SelectedTab == _shortcutsMainTab)
        {
            ShowChatTab();
            return;
        }

        ShowChatTab();
    }

    private void ShowUserDetailTab()
    {
        SelectDetailTab(_userTab, _userMessageView, "Switched to User pane.");
    }

    private void ShowThinkingDetailTab()
    {
        SelectDetailTab(_thinkingTab, _thinkingMessageView, "Switched to Thinking pane.");
    }

    private void ShowAssistantDetailTab()
    {
        SelectDetailTab(_assistantTab, _assistantMessageView, "Switched to Assistant pane.");
    }

    private void ShowAdditionalDetailTab()
    {
        SelectDetailTab(_additionalPropertiesTab, _additionalPropertiesView, "Switched to Additional pane.");
    }

    private string BuildShortcutReferenceText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Top Tabs");
        builder.AppendLine("F1         Open Shortcuts tab");
        builder.AppendLine("F11        Cycle Chat / Settings / Shortcuts tabs");
        builder.AppendLine("Ctrl+PgUp  Switch to Chat tab");
        builder.AppendLine("Ctrl+PgDn  Switch to Settings tab");
        builder.AppendLine();
        builder.AppendLine("Detail Panes");
        builder.AppendLine("Ctrl+1     Show User pane");
        builder.AppendLine("Ctrl+2     Show Thinking pane");
        builder.AppendLine("Ctrl+3     Show Assistant pane");
        builder.AppendLine("Ctrl+4     Show Additional pane");
        builder.AppendLine();
        builder.AppendLine("Chat Actions");
        builder.AppendLine("Ctrl+G     Load models from McpHost");
        builder.AppendLine("Ctrl+U     Apply selected model");
        builder.AppendLine("Ctrl+B     Browse local image file");
        builder.AppendLine("Ctrl+D     Send current draft");
        builder.AppendLine("Ctrl+R     Retry latest turn");
        builder.AppendLine("Ctrl+K     Clear chat");
        builder.AppendLine("Ctrl+W     Save settings");
        builder.AppendLine("Ctrl+Q     Quit program");
        builder.AppendLine("F12        Quit program (terminal fallback)");
        builder.AppendLine();
        builder.AppendLine("Copy Actions");
        builder.AppendLine("F7         Copy selected user message");
        builder.AppendLine("F8         Copy selected assistant message");
        builder.AppendLine("F9         Copy selected additional properties");
        builder.AppendLine();
        builder.AppendLine("Notes");
        builder.AppendLine("Alt hotkeys open the menu bar.");
        builder.AppendLine("Tab / Shift+Tab moves focus across controls.");
        builder.AppendLine("Clipboard support depends on the host OS tools.");
        return builder.ToString().TrimEnd();
    }

    private void ShowAbout()
    {
        const string message =
            "Zhengyan.ChatUI.TUI mirrors the desktop/web test UI for McpHost.\n\n" +
            "- Separate Chat / Settings / Shortcuts tabs for smaller displays\n" +
            "- Settings panel for endpoint, API key, model, token limit, temperature and top_p\n" +
            "- Persistent config file with manual save button\n" +
            "- Keyboard-first shortcuts via menu bar and status bar\n" +
            "- Explicit top-tab and detail-pane shortcuts for keyboard navigation\n" +
            "- User / Thinking / Assistant / Additional Properties panes with direct copy actions\n" +
            "- Image URL, local image path, and popup file picker attachments\n" +
            "- Streaming support for both chat completions and responses APIs";

        MessageBox.Query("About", message, "OK");
    }

    private void QuitApplication()
    {
        Application.RequestStop(Application.Top);
    }

    private void ClearComposerInputs()
    {
        _pendingAttachments.Clear();
        SetFieldText(_imageUrlField, string.Empty);
        SetFieldText(_localImagePathField, string.Empty);
        SetText(_inputMessageView, string.Empty);
        RefreshPendingAttachments();
    }

    private void QueueConversationRefresh()
    {
        QueueUi(() =>
        {
            RefreshTurnList();
            RefreshConversationViews();
        });
    }

    private void RefreshAvailableModels()
    {
        _availableModelsListView?.SetSource(_availableModels);
        if (_availableModelsListView is not null && _availableModels.Count > 0 && _availableModelsListView.SelectedItem < 0)
        {
            _availableModelsListView.SelectedItem = 0;
        }
    }

    private void RefreshTurnList()
    {
        var items = _chatHistory
            .Select((turn, index) => BuildTurnSummary(turn, index))
            .ToList();

        _turnsListView?.SetSource(items);

        if (_turnsListView is null)
        {
            return;
        }

        _turnsFrame!.Title = items.Count == 0 ? "Turns" : $"Turns ({items.Count})";

        if (items.Count == 0)
        {
            _turnsListView.SelectedItem = -1;
            return;
        }

        var selectedIndex = _selectedTurnIndex;
        if (selectedIndex < 0 || selectedIndex >= items.Count)
        {
            selectedIndex = items.Count - 1;
            _selectedTurnIndex = selectedIndex;
        }

        _turnsListView.SelectedItem = selectedIndex;
    }

    private void RefreshPendingAttachments()
    {
        var items = _pendingAttachments
            .Select(attachment => $"{(attachment.IsLocalFile ? "[File]" : "[URL]")} {attachment.DisplayName} | {attachment.Source}")
            .ToList();

        _pendingAttachmentsListView?.SetSource(items);
        UpdateControlStates();
    }

    private void RefreshConversationViews()
    {
        var selectedTurn = GetSelectedTurn();

        SetText(_userMessageView, selectedTurn is null ? "No turn selected." : BuildUserDisplayText(selectedTurn));
        SetText(
            _thinkingMessageView,
            selectedTurn is null
                ? "No turn selected."
                : string.IsNullOrWhiteSpace(selectedTurn.AssistantReasoning)
                    ? "(no reasoning captured)"
                    : selectedTurn.AssistantReasoning);
        SetText(
            _assistantMessageView,
            selectedTurn is null
                ? "No turn selected."
                : string.IsNullOrWhiteSpace(selectedTurn.AssistantMessage)
                    ? "(waiting for response)"
                    : selectedTurn.AssistantMessage);
        SetText(_additionalPropertiesView, selectedTurn?.AssistantAdditionalProperties ?? string.Empty);

        UpdateConversationTitles(selectedTurn);
        UpdateControlStates();
    }

    private void UpdateConversationTitles(ChatTurn? selectedTurn)
    {
        if (_userTab is null || _thinkingTab is null || _assistantTab is null || _additionalPropertiesTab is null)
        {
            return;
        }

        var maxTitleWidth = GetTabTitleMaxDisplayWidth();

        if (selectedTurn is null)
        {
            _userTab.Text = EllipsizeDisplayText("User", maxTitleWidth);
            _thinkingTab.Text = EllipsizeDisplayText("Thinking", maxTitleWidth);
            _assistantTab.Text = EllipsizeDisplayText("Assistant", maxTitleWidth);
            _additionalPropertiesTab.Text = EllipsizeDisplayText("Additional", maxTitleWidth);
            _detailTabView?.SetNeedsDisplay();
            return;
        }

        var turnNumber = Math.Max(_selectedTurnIndex + 1, 1);
        _userTab.Text = EllipsizeDisplayText($"User #{turnNumber}", maxTitleWidth);
        _thinkingTab.Text = EllipsizeDisplayText($"Thinking #{turnNumber}", maxTitleWidth);
        _assistantTab.Text = EllipsizeDisplayText($"Assistant #{turnNumber}", maxTitleWidth);
        _additionalPropertiesTab.Text = EllipsizeDisplayText($"Additional #{turnNumber}", maxTitleWidth);
        _detailTabView?.SetNeedsDisplay();
    }

    private void SyncSelectedModelFromList()
    {
        if (_availableModelsListView is null)
        {
            return;
        }

        var selectedIndex = _availableModelsListView.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _availableModels.Count)
        {
            SetModelFieldValue(_availableModels[selectedIndex]);
        }
    }

    private ChatTurn? GetSelectedTurn()
    {
        if (_turnsListView is not null)
        {
            var selectedIndex = _turnsListView.SelectedItem;
            if (selectedIndex >= 0 && selectedIndex < _chatHistory.Count)
            {
                _selectedTurnIndex = selectedIndex;
                return _chatHistory[selectedIndex];
            }
        }

        if (_selectedTurnIndex >= 0 && _selectedTurnIndex < _chatHistory.Count)
        {
            return _chatHistory[_selectedTurnIndex];
        }

        return _chatHistory.Count > 0 ? _chatHistory[^1] : null;
    }

    private void UpdateControlStates()
    {
        if (_getModelsButton is not null)
        {
            _getModelsButton.Enabled = CanGetModels();
        }

        if (_useSelectedModelButton is not null)
        {
            _useSelectedModelButton.Enabled = CanUseSelectedModel();
        }

        if (_addImageUrlButton is not null)
        {
            _addImageUrlButton.Enabled = !_isSending;
        }

        if (_addLocalImageButton is not null)
        {
            _addLocalImageButton.Enabled = !_isSending;
        }

        if (_browseLocalImageButton is not null)
        {
            _browseLocalImageButton.Enabled = !_isSending;
        }

        if (_removeAttachmentButton is not null)
        {
            _removeAttachmentButton.Enabled = CanRemovePendingAttachment();
        }

        if (_clearAttachmentsButton is not null)
        {
            _clearAttachmentsButton.Enabled = !_isSending && _pendingAttachments.Count > 0;
        }

        if (_sendButton is not null)
        {
            _sendButton.Enabled = CanSendMessage();
        }

        if (_retryButton is not null)
        {
            _retryButton.Enabled = CanRetryMessage();
        }

        if (_clearChatButton is not null)
        {
            _clearChatButton.Enabled = CanClearChat();
        }

        if (_copyUserButton is not null)
        {
            _copyUserButton.Enabled = CanCopySelectedUser();
        }

        if (_copyThinkingButton is not null)
        {
            _copyThinkingButton.Enabled = CanCopySelectedThinking();
        }

        if (_copyAssistantButton is not null)
        {
            _copyAssistantButton.Enabled = CanCopySelectedAssistant();
        }

        if (_copyAdditionalPropertiesButton is not null)
        {
            _copyAdditionalPropertiesButton.Enabled = CanCopySelectedAdditionalProperties();
        }
    }

    private void SetLoadingModels(bool value, string? statusMessage = null)
    {
        _isLoadingModels = value;
        UpdateControlStates();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            SetStatus(statusMessage, StatusKind.Info);
        }
    }

    private void SetSendingState(bool value, string? statusMessage = null)
    {
        _isSending = value;
        UpdateControlStates();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            SetStatus(statusMessage, StatusKind.Info);
        }
    }

    private void SetStatus(string statusMessage, StatusKind statusKind)
    {
        QueueUi(() =>
        {
            if (_statusLabel is null)
            {
                return;
            }

            _statusLabel.Text = statusMessage;
            _statusLabel.ColorScheme = statusKind switch
            {
                StatusKind.Success => _statusSuccessScheme,
                StatusKind.Error => _statusErrorScheme,
                _ => _statusInfoScheme
            };

            _window?.SetNeedsDisplay();
        });
    }

    private string BuildUserDisplayText(ChatTurn turn)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrWhiteSpace(turn.UserMessage) ? "(empty)" : turn.UserMessage.TrimEnd());

        if (turn.Attachments.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Attached Images");
            foreach (var attachment in turn.Attachments)
            {
                builder.Append("- ");
                builder.Append(attachment.SourceLabel);
                builder.Append(" | ");
                builder.Append(attachment.DisplayName);
                builder.Append(" | ");
                builder.AppendLine(attachment.Source);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildFullTranscript()
    {
        if (_chatHistory.Count == 0)
        {
            return "No chat history yet.";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < _chatHistory.Count; index++)
        {
            var turn = _chatHistory[index];
            builder.AppendLine($"===== Turn {index + 1} =====");
            builder.AppendLine("User");
            builder.AppendLine(BuildUserDisplayText(turn));
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(turn.AssistantReasoning))
            {
                builder.AppendLine("Thinking");
                builder.AppendLine(turn.AssistantReasoning.TrimEnd());
                builder.AppendLine();
            }

            builder.AppendLine("Assistant");
            builder.AppendLine(string.IsNullOrWhiteSpace(turn.AssistantMessage) ? "(waiting for response)" : turn.AssistantMessage.TrimEnd());

            if (!string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
            {
                builder.AppendLine();
                builder.AppendLine("Additional Properties");
                builder.AppendLine(turn.AssistantAdditionalProperties.TrimEnd());
            }

            if (index < _chatHistory.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private bool ShouldUseCompactComposeLayout()
    {
        var bodyHeight = Math.Max(Application.Driver.Rows - 2, 0);
        return bodyHeight < NormalComposeFrameHeight + MinimumConversationHeight;
    }

    private bool HandleGlobalShortcut(Key key)
    {
        if (key == Key.F1)
        {
            ShowShortcutsTab();
            return true;
        }

        if (key == Key.F11)
        {
            ToggleMainTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.PageUp))
        {
            ShowChatTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.PageDown))
        {
            ShowSettingsTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.D1))
        {
            ShowUserDetailTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.D2))
        {
            ShowThinkingDetailTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.D3))
        {
            ShowAssistantDetailTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.D4))
        {
            ShowAdditionalDetailTab();
            return true;
        }

        if (key == (Key.CtrlMask | Key.G))
        {
            _ = RunActionAsync(GetModelsAsync);
            return true;
        }

        if (key == (Key.CtrlMask | Key.U))
        {
            _ = RunActionAsync(UseSelectedModelAsync);
            return true;
        }

        if (key == (Key.CtrlMask | Key.B))
        {
            BrowseLocalImageFile();
            return true;
        }

        if (key == (Key.CtrlMask | Key.D))
        {
            _ = RunActionAsync(SendMessageAsync);
            return true;
        }

        if (key == (Key.CtrlMask | Key.R))
        {
            _ = RunActionAsync(RetryMessageAsync);
            return true;
        }

        if (key == (Key.CtrlMask | Key.K))
        {
            ClearChat();
            return true;
        }

        if (key == (Key.CtrlMask | Key.W))
        {
            SaveSettings();
            return true;
        }

        if (key == Key.F12 || key == (Key.CtrlMask | Key.Q))
        {
            QuitApplication();
            return true;
        }

        return false;
    }

    private string BuildTurnSummary(ChatTurn turn, int index)
    {
        var preview = string.IsNullOrWhiteSpace(turn.UserMessage)
            ? turn.Attachments.Count > 0
                ? $"{turn.Attachments.Count} image(s)"
                : "(empty)"
            : turn.UserMessage.ReplaceLineEndings(" ").Trim();

        var prefix = $"{index + 1:00}. ";
        var previewWidth = Math.Max(GetTurnSummaryMaxDisplayWidth() - GetDisplayWidth(prefix), 4);
        return prefix + EllipsizeDisplayText(preview, previewWidth);
    }

    private int GetTurnSummaryMaxDisplayWidth()
    {
        if (_turnsListView is not null && _turnsListView.Bounds.Width > 0)
        {
            return Math.Max(_turnsListView.Bounds.Width - 1, 8);
        }

        if (_turnsFrame is not null && _turnsFrame.Bounds.Width > 2)
        {
            return Math.Max(_turnsFrame.Bounds.Width - 3, 8);
        }

        return _turnPreviewLength + 4;
    }

    private int GetTabTitleMaxDisplayWidth()
    {
        if (_detailTabView is not null && _detailTabView.Bounds.Width > 0)
        {
            return Math.Clamp((_detailTabView.Bounds.Width - 10) / 4, 7, 16);
        }

        return 10;
    }

    private static string EllipsizeDisplayText(string value, int maxWidth)
    {
        if (string.IsNullOrEmpty(value) || maxWidth <= 0)
        {
            return string.Empty;
        }

        if (GetDisplayWidth(value) <= maxWidth)
        {
            return value;
        }

        if (maxWidth <= 3)
        {
            return new string('.', maxWidth);
        }

        var ellipsis = "...";
        var allowedWidth = maxWidth - ellipsis.Length;
        var builder = new StringBuilder();
        var currentWidth = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            var runeWidth = GetRuneDisplayWidth(rune);
            if (currentWidth + runeWidth > allowedWidth)
            {
                break;
            }

            builder.Append(rune.ToString());
            currentWidth += runeWidth;
        }

        builder.Append(ellipsis);
        return builder.ToString();
    }

    private void SelectMainTab(TabView.Tab? tab, View? focusTarget, string statusMessage)
    {
        if (_mainTabView is null || tab is null)
        {
            return;
        }

        if (!ReferenceEquals(_mainTabView.SelectedTab, tab))
        {
            _mainTabView.SelectedTab = tab;
            _mainTabView.EnsureSelectedTabIsVisible();
            _mainTabView.SetNeedsDisplay();
        }

        focusTarget?.SetFocus();
        SetStatus(statusMessage, StatusKind.Info);
    }

    private void SelectDetailTab(TabView.Tab? tab, View? focusTarget, string statusMessage)
    {
        if (_detailTabView is null || tab is null)
        {
            return;
        }

        if (!ReferenceEquals(_detailTabView.SelectedTab, tab))
        {
            _detailTabView.SelectedTab = tab;
            _detailTabView.EnsureSelectedTabIsVisible();
            _detailTabView.SetNeedsDisplay();
        }

        focusTarget?.SetFocus();
        SetStatus(statusMessage, StatusKind.Info);
    }

    private static int GetDisplayWidth(string value)
    {
        var width = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            width += GetRuneDisplayWidth(rune);
        }

        return width;
    }

    private static int GetRuneDisplayWidth(System.Text.Rune rune)
    {
        if (System.Text.Rune.IsControl(rune))
        {
            return 0;
        }

        var unicodeCategory = System.Text.Rune.GetUnicodeCategory(rune);
        if (unicodeCategory is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.EnclosingMark
            or System.Globalization.UnicodeCategory.Format)
        {
            return 0;
        }

        var value = rune.Value;
        return IsWideRune(value) ? 2 : 1;
    }

    private static bool IsWideRune(int value)
    {
        return value switch
        {
            >= 0x1100 and <= 0x115F => true,
            >= 0x2329 and <= 0x232A => true,
            >= 0x2E80 and <= 0xA4CF and not 0x303F => true,
            >= 0xAC00 and <= 0xD7A3 => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0xFE10 and <= 0xFE19 => true,
            >= 0xFE30 and <= 0xFE6F => true,
            >= 0xFF00 and <= 0xFF60 => true,
            >= 0xFFE0 and <= 0xFFE6 => true,
            >= 0x1F300 and <= 0x1FAFF => true,
            >= 0x20000 and <= 0x3FFFD => true,
            _ => false
        };
    }

    private bool CanGetModels()
    {
        return !_isLoadingModels && !_isSending && !string.IsNullOrWhiteSpace(GetFieldText(_serverField));
    }

    private bool CanUseSelectedModel()
    {
        return !_isLoadingModels && !_isSending && _configModels is not null && _availableModels.Count > 0;
    }

    private bool CanSaveSettings()
    {
        return !_isLoadingModels && !_isSending;
    }

    private bool CanSendMessage()
    {
        return !_isSending && !string.IsNullOrWhiteSpace(GetCurrentModel());
    }

    private bool CanRetryMessage()
    {
        return !_isSending && _chatHistory.Count > 0 && !string.IsNullOrWhiteSpace(GetCurrentModel());
    }

    private bool CanClearChat()
    {
        return !_isSending && (_chatHistory.Count > 0 || _pendingAttachments.Count > 0 || !string.IsNullOrWhiteSpace(GetText(_inputMessageView)));
    }

    private bool CanRemovePendingAttachment()
    {
        return !_isSending
            && _pendingAttachments.Count > 0
            && _pendingAttachmentsListView is not null
            && _pendingAttachmentsListView.SelectedItem >= 0
            && _pendingAttachmentsListView.SelectedItem < _pendingAttachments.Count;
    }

    private bool CanCopySelectedUser()
    {
        return GetSelectedTurn() is not null;
    }

    private bool CanCopySelectedThinking()
    {
        return GetSelectedTurn() is { AssistantReasoning.Length: > 0 };
    }

    private bool CanCopySelectedAssistant()
    {
        return GetSelectedTurn() is { AssistantMessage.Length: > 0 };
    }

    private bool CanCopySelectedAdditionalProperties()
    {
        return GetSelectedTurn() is { AssistantAdditionalProperties.Length: > 0 };
    }

    private bool CanCopyTranscript()
    {
        return _chatHistory.Count > 0;
    }

    private string ResolveInitialImageDirectory()
    {
        var rawValue = GetFieldText(_localImagePathField).Trim();
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            if (File.Exists(rawValue))
            {
                return Path.GetDirectoryName(Path.GetFullPath(rawValue)) ?? Environment.CurrentDirectory;
            }

            if (Directory.Exists(rawValue))
            {
                return Path.GetFullPath(rawValue);
            }
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(homeDirectory) && Directory.Exists(homeDirectory))
        {
            return homeDirectory;
        }

        return Environment.CurrentDirectory;
    }

    private string GetCurrentModel()
    {
        var model = GetFieldText(_modelField).Trim();
        if (!string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        if (_availableModelsListView is not null)
        {
            var selectedIndex = _availableModelsListView.SelectedItem;
            if (selectedIndex >= 0 && selectedIndex < _availableModels.Count)
            {
                return _availableModels[selectedIndex];
            }
        }

        return string.Empty;
    }

    private string GetServerEndpoint()
    {
        var endpoint = GetFieldText(_serverField).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Server URL cannot be empty.");
        }

        return endpoint;
    }

    private string GetApiKey()
    {
        return GetFieldText(_apiKeyField).Trim();
    }

    private bool GetUseResponsesApi()
    {
        return _useResponsesApiCheckBox?.Checked == true;
    }

    private int ParseMaxTokens()
    {
        return int.TryParse(GetFieldText(_maxTokensField), out var value) ? value : 4096;
    }

    private float ParseTemperature()
    {
        var rawValue = GetFieldText(_temperatureField).Trim();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0.9f;
        }

        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Temperature must be a valid number.");
        }

        if (value < 0f || value > 2f)
        {
            throw new InvalidOperationException("Temperature must be between 0 and 2.");
        }

        return value;
    }

    private float ParseTopP()
    {
        var rawValue = GetFieldText(_topPField).Trim();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0.9f;
        }

        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Top P must be a valid number.");
        }

        if (value < 0f || value > 1f)
        {
            throw new InvalidOperationException("Top P must be between 0 and 1.");
        }

        return value;
    }

    private void SetModelFieldValue(string value)
    {
        SetFieldText(_modelField, value);
        UpdateControlStates();
    }

    private static string GetFieldText(TextField? field)
    {
        return field?.Text?.ToString() ?? string.Empty;
    }

    private static string GetText(TextView? view)
    {
        return view?.Text?.ToString() ?? string.Empty;
    }

    private static void SetFieldText(TextField? field, string value)
    {
        if (field is not null)
        {
            field.Text = value;
        }
    }

    private static void SetText(TextView? view, string value)
    {
        if (view is not null)
        {
            view.Text = value;
        }
    }

    private Label CreateShortcutHintLabel(string text)
    {
        return new Label(text)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            ColorScheme = _statusInfoScheme
        };
    }

    private void WireShortcutPassthrough(params View?[] views)
    {
        foreach (var view in views)
        {
            if (view is null)
            {
                continue;
            }

            view.KeyPress += args =>
            {
                if (HandleGlobalShortcut(args.KeyEvent.Key))
                {
                    args.Handled = true;
                }
            };
        }
    }

    private static MenuItem CreateMenuItem(string title, string help, Action action, Func<bool> canExecute)
    {
        return new MenuItem(title, help, action, canExecute, null, Key.Null);
    }

    private static MenuItem CreateMenuItem(string title, string help, Action action, Func<bool> canExecute, Key shortcut)
    {
        return new MenuItem(title, help, action, canExecute, null, shortcut);
    }

    private static void QueueUi(Action action)
    {
        if (Application.MainLoop is null)
        {
            action();
            return;
        }

        Application.MainLoop.Invoke(action);
    }

    private static Task InvokeOnUiAsync(Action action)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        QueueUi(() =>
        {
            try
            {
                action();
                completionSource.SetResult();
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        });

        return completionSource.Task;
    }

    private static ColorScheme CreateScheme(
        Color normalForeground,
        Color normalBackground,
        Color focusForeground,
        Color focusBackground,
        Color hotForeground,
        Color hotBackground,
        Color disabledForeground,
        Color disabledBackground)
    {
        return new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(normalForeground, normalBackground),
            Focus = Application.Driver.MakeAttribute(focusForeground, focusBackground),
            HotNormal = Application.Driver.MakeAttribute(hotForeground, hotBackground),
            HotFocus = Application.Driver.MakeAttribute(hotForeground, focusBackground),
            Disabled = Application.Driver.MakeAttribute(disabledForeground, disabledBackground)
        };
    }

    private static ColorScheme CreateFlatScheme(Color foreground, Color background)
    {
        var attribute = Application.Driver.MakeAttribute(foreground, background);
        return new ColorScheme
        {
            Normal = attribute,
            Focus = attribute,
            HotNormal = attribute,
            HotFocus = attribute,
            Disabled = attribute
        };
    }

    private enum StatusKind
    {
        Info,
        Success,
        Error
    }

    private sealed class ShortcutWindow(string title, Func<Key, bool> shortcutHandler) : Window(title)
    {
        public override bool ProcessKey(KeyEvent keyEvent)
        {
            return shortcutHandler(keyEvent.Key) || base.ProcessKey(keyEvent);
        }

        public override bool ProcessColdKey(KeyEvent keyEvent)
        {
            return shortcutHandler(keyEvent.Key) || base.ProcessColdKey(keyEvent);
        }
    }

    private sealed class ContrastTextView(ColorScheme contentScheme) : TextView
    {
        protected override void SetNormalColor()
        {
            Driver.SetAttribute(contentScheme.Normal);
        }

        protected override void SetNormalColor(List<System.Rune> line, int idx)
        {
            Driver.SetAttribute(contentScheme.Normal);
        }

        protected override void SetSelectionColor(List<System.Rune> line, int idx)
        {
            Driver.SetAttribute(contentScheme.Focus);
        }

        protected override void SetReadOnlyColor(List<System.Rune> line, int idx)
        {
            Driver.SetAttribute(contentScheme.Focus);
        }

        protected override void SetUsedColor(List<System.Rune> line, int idx)
        {
            Driver.SetAttribute(contentScheme.Focus);
        }
    }
}
