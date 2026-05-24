---
phase: 08-per-language-model-selection
plan: 04
subsystem: ui
tags: [settings-ui, flyout, model-picker, hud, per-language]
dependency_graph:
  requires: [08-01, 08-02, 08-03]
  provides: [language-model-picker-ui, gear-button-language-row, loading-model-hud-affordance]
  affects: [settings-ui, hud-ui]
tech_stack:
  added: []
  patterns: [CommunityToolkit.Mvvm ObservableObject/RelayCommand, Avalonia ShowDialog, BlockerDialogWindow analog]
key_files:
  created:
    - src/QuickSType.UI/ViewModels/LanguageModelFlyoutViewModel.cs
    - src/QuickSType.UI/Views/LanguageModelFlyoutWindow.axaml
    - src/QuickSType.UI/Views/LanguageModelFlyoutWindow.axaml.cs
  modified:
    - src/QuickSType.UI/ViewModels/SettingsViewModel.cs
    - src/QuickSType.UI/Views/MainWindow.axaml
    - src/QuickSType.UI/Views/HudWindow.axaml
decisions:
  - Gear button IsVisible=IsEnabled (positive binding) so it only shows on enabled languages (D-01)
  - CanDownload evaluated from IsInstalled (ModelCatalog.IsInstalled) — re-notified after download completes
  - LoadingModel HUD affordance: amber Ellipse (Fill=#FFB300) overlapping RecDot position, separate IsVisible binding
  - LanguageModelFlyoutWindow follows BlockerDialogWindow pattern exactly (AvaloniaXamlLoader.Load, two ctors)
  - No OnClosing override — Cancel/X just returns from ShowDialog naturally
metrics:
  duration: "~12 min"
  completed: "2026-05-24T10:41:44Z"
  tasks_completed: 3
  files_changed: 6
---

# Phase 8 Plan 04: Language Model Picker UI Summary

Per-language model picker UI wired end-to-end: gear button on each enabled language row → modal flyout listing language-specific models with inline download + progress → save persists via AppHost.UpdateConfig(AppConfig.WithLanguageModel) → HUD visually distinguishes LoadingModel state with amber dot.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | LanguageModelFlyoutViewModel + Window + SettingsViewModel gear | f82c7fd | LanguageModelFlyoutViewModel.cs, LanguageModelFlyoutWindow.axaml, .axaml.cs, SettingsViewModel.cs |
| 2 | Gear button in Languages tab DataTemplate + HUD amber dot | 6fe0ddc | MainWindow.axaml, HudWindow.axaml |
| 3 | Manual UI checkpoint (YOLO auto-approved) | — | — |

## What Was Built

### LanguageModelFlyoutViewModel (new)
Sealed partial ObservableObject with:
- `LangCode` public getter
- `AvailableLanguageModels` ObservableCollection populated from `ModelCatalog.LanguageModels.TryGetValue(langCode)`
- `GlobalModelId` computed from `_host.Config.Model`
- `SelectedModelId` [ObservableProperty] initialized from `_host.Config.LanguageModels[langCode]` or empty string (= use global)
- `CanDownload` computed property: `!string.IsNullOrEmpty(SelectedModelId) && !ModelCatalog.IsInstalled(SelectedModelId)`
- `OnSelectedModelIdChanged` partial method calls `OnPropertyChanged(nameof(CanDownload))`
- `[RelayCommand] async Task Download(CancellationToken ct)`: copies DownloadSelectedModelCommand pattern from SettingsViewModel; re-notifies CanDownload in finally block
- `[RelayCommand] void SaveAssignment()`: writes `_host.UpdateConfig(_host.Config.WithLanguageModel(LangCode, modelId))`

### LanguageModelFlyoutWindow.axaml (new)
- x:DataType="vm:LanguageModelFlyoutViewModel" compiled binding
- ListBox for language-specific models (D-01, D-03)
- RadioButton for "Use global model" fallback (D-07)
- ProgressBar + DownloadStatus TextBlock bound to IsDownloading (positive binding)
- Download button with `IsVisible="{Binding CanDownload}"` — positive binding, no negation
- Cancel (IsCancel=True) + Save (Classes="accent", IsDefault=True, Command=SaveAssignmentCommand)

### LanguageModelFlyoutWindow.axaml.cs (new)
- Parameterless designer ctor + `LanguageModelFlyoutWindow(string langCode, AppHost host) : this()` ctor
- AOT-safe: `InitializeComponent()` calls `AvaloniaXamlLoader.Load(this)`

### SettingsViewModel.cs (modified)
- LanguageRow extended with `Func<Task>? _openModelPicker` field and optional ctor parameter
- LanguageRow gains `[RelayCommand] private async Task OpenModelPicker()`
- `AvailableLanguages` construction now passes `openModelPicker: () => OpenLanguageModelPickerAsync(l.Code)`
- New method `private async Task OpenLanguageModelPickerAsync(string langCode)` resolves MainWindow via ApplicationLifetime and calls `flyout.ShowDialog(owner)`

### MainWindow.axaml (modified)
- Languages tab DataTemplate replaced with DockPanel layout: gear Button docked right with `Command="{Binding OpenModelPickerCommand}"` and `IsVisible="{Binding IsEnabled}"`, CheckBox + TextBlock fill remaining width

### HudWindow.axaml (modified)
- Added amber Ellipse (Fill=#FFB300) with `IsVisible="{Binding IsLoadingModel}"` adjacent to the existing RecDot for LoadingModel visual affordance

## Deviations from Plan

None — plan executed exactly as written.

## Verification

- `dotnet build src/QuickSType.UI/QuickSType.UI.csproj` exits 0 (2 pre-existing NU1903 warnings only)
- `dotnet test tests/QuickSType.Core.Tests/` exits 0 — 249 tests passed, 0 failed
- `grep -F '{Binding !' *.axaml` returns 0 lines across all 3 modified/created XAML files
- `grep -c 'OpenModelPickerCommand' MainWindow.axaml` = 1
- `grep -c 'IsLoadingModel' HudWindow.axaml` = 1
- `grep -c 'CanDownload' LanguageModelFlyoutViewModel.cs` = 4

## Known Stubs

None — all bindings are wired to real data sources.

## Self-Check: PASSED
