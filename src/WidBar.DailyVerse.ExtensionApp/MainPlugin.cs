using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WidBar.DailyVerse.Core.Models;
using WidBar.DailyVerse.Core.Services;
using WidBar.DailyVerse.ExtensionApp.UI;
using WidBar.SDK;

namespace WidBar.DailyVerse.ExtensionApp;

public sealed class MainPlugin :
    WidgetPluginBase,
    IConfigurableWidgetPlugin,
    IWidgetFlyoutLifecycle
{
    private BibleRepository? _repository;
    private DailyVerseCoordinator? _coordinator;
    private WidgetSettings _settings = new();
    private DailyVerseSnapshot? _latestSnapshot;
    private DispatcherQueue? _dispatcher;
    private TaskbarPreviewControl? _preview;
    private VerseFlyoutView? _flyout;
    private bool _subscribed;
    private bool _disposed;

    public override string Id => "com.widbar.dailyverse";
    public override string Name => "Versículo Diário";
    public override string Version => "1.0.0";

    public override bool HighlightPreviewOnHover => true;
    public override int PreviewLogicalWidth => _settings.ComputePreviewWidth();
    public override int FlyoutWidth => 388;
    public override int FlyoutHeight => 420;
    public override int TileHeight => 340;

    public override async Task InitializeAsync(IWidgetContext context)
    {
        await base.InitializeAsync(context);
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();

        (_repository, _coordinator) = SharedVerseService.GetOrCreate(context.SharedDataDirectory);
        _settings = WidgetSettings.FromJson(context.SettingsJson);

        _coordinator.StateChanged += OnCoordinatorStateChanged;
        _subscribed = true;
        context.PreviewVisibilityChanged += OnPreviewVisibilityChanged;

        _latestSnapshot = _coordinator.EnsureTodayVerse(_settings.PoolMode);
    }

    public override UIElement? CreatePreviewContent()
    {
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        _preview = new TaskbarPreviewControl();
        if (_latestSnapshot is not null)
        {
            _preview.Update(_settings, _latestSnapshot);
        }
        return _preview;
    }

    public override UIElement? CreateFlyoutContent()
    {
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        if (_repository is null || _coordinator is null)
        {
            return null;
        }

        _flyout = new VerseFlyoutView(_repository, _coordinator);
        _flyout.ShuffleRequested += () => _coordinator.ShuffleNow(_settings.PoolMode);
        _flyout.FavoriteToggleRequested += key => _coordinator.ToggleFavorite(key);
        _flyout.SelectVerseRequested += key => _coordinator.SelectVerse(key);

        if (_latestSnapshot is not null)
        {
            _flyout.Update(_settings, _latestSnapshot);
        }

        return _flyout;
    }

    public UIElement? CreateSettingsContent(IWidgetSettingsContext context)
    {
        if (_repository is null || _coordinator is null)
        {
            return null;
        }

        return new VerseSettingsView(context, _repository, _coordinator);
    }

    public override void OnSettingsDraftChanged(string settingsJson)
    {
        _settings = WidgetSettings.FromJson(settingsJson);
        RenderAll();
    }

    public void OnFlyoutShown()
    {
        if (_coordinator is null) return;
        _latestSnapshot = _coordinator.EnsureTodayVerse(_settings.PoolMode);
        RenderAll();
    }

    public void OnFlyoutHidden()
    {
        _flyout?.ResetToMainPage();
    }

    public override ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        if (_subscribed && _coordinator is not null)
        {
            _coordinator.StateChanged -= OnCoordinatorStateChanged;
            _subscribed = false;
        }

        if (Context is not null)
        {
            Context.PreviewVisibilityChanged -= OnPreviewVisibilityChanged;
        }

        _preview = null;
        _flyout = null;
        return ValueTask.CompletedTask;
    }

    private void OnPreviewVisibilityChanged(object? sender, bool isVisible)
    {
        if (isVisible && _coordinator is not null)
        {
            _latestSnapshot = _coordinator.EnsureTodayVerse(_settings.PoolMode);
            RenderAll();
        }
    }

    private void OnCoordinatorStateChanged(DailyVerseSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        _dispatcher?.TryEnqueue(() =>
        {
            if (_disposed) return;
            RenderAll();
        });
    }

    private void RenderAll()
    {
        if (_latestSnapshot is null) return;
        _preview?.Update(_settings, _latestSnapshot);
        _flyout?.Update(_settings, _latestSnapshot);
    }
}
