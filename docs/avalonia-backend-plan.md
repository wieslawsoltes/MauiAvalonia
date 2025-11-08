# Avalonia Backend Implementation Plan

The goal is to host MAUI applications on top of Avalonia without modifying the upstream `dotnet/maui` repository. This plan assumes we build companion assemblies inside this repo that reference the official MAUI packages (netstandard TFMs) and the Avalonia UI stack.

## Phase 1 – Foundations & Scaffolding
1. [x] Define the solution layout (`src/Microsoft.Maui.Avalonia`, `samples`, `tests`) and ensure it builds against the MAUI submodule plus the Avalonia submodule.
2. [x] Create a baseline MAUI sample application that will eventually run on Avalonia for rapid regression testing.
3. [x] Document code-style, dependency, and versioning policies (MAUI + Avalonia alignment, minimum .NET version) so future contributions stay compatible.

## Phase 2 – Hosting & Runtime Infrastructure
1. [x] Implement `AvaloniaMauiApplication`/`AvaloniaMauiLifetime` that derives from `Avalonia.Application`, implements `IPlatformApplication`, and mirrors the boot sequence in `MauiApplication`/`MauiWinUIApplication` (builds `MauiApp`, creates an `IMauiContext`, sets the MAUI `IApplication` handler).
2. [x] Provide `UseMauiAvaloniaHost()` builder extensions that register Avalonia-specific services (dispatcher, animation manager bridge, window/context factories) and expose a single entry point similar to `MauiApp.CreateBuilder()`.
3. [x] Create an `AvaloniaWindowHost` that wraps an `Avalonia.Controls.Window`, partners with `IMauiContext.MakeWindowScope`, and tracks multiple windows via Avalonia’s desktop lifetime APIs (multi-window parity with Windows/iOS hosts).
4. [x] Implement dispatcher/animation/timing adapters (`IDispatcher`, `IAnimationManager`, `IDeviceDisplay` subsets) backed by Avalonia’s `Dispatcher`, `CompositionTarget`, and platform services.
5. [x] Bridge MAUI lifecycle events (`ILifecycleBuilder`) to Avalonia events (application start/exit, window opened/closed, theme changes) to keep `LifecycleEvents` subscriptions functional.

## Phase 3 – Handler & Layout Foundation
1. [x] Create a reusable `AvaloniaViewHandler<TVirtualView, TControl>` base class that derives from `ViewHandler` (netstandard build) but adds strongly-typed accessors, container management, and measurement/arrange translation to Avalonia’s layout system.
2. [x] Implement platform services needed by handlers: `IAvaloniaNavigationRoot` (equivalent of `NavigationRootManager`), toolbar/menu hosts, title bar provider, safe-area provider, and gesture recognizer shims.
3. [x] Build the essential handler set for application infrastructure (`ApplicationHandler`, `WindowHandler`, `PageHandler`, `LayoutHandler`, `ContentViewHandler`) so MAUI visual trees can render and size correctly inside an Avalonia visual root.
4. [x] Map MAUI graphics primitives (brushes, fonts, shapes) onto Avalonia resources by implementing the necessary `Update*` extension methods (`UpdateBackground`, `UpdateFont`, `UpdateStroke`, etc.) and ensuring `IFontManager` returns Avalonia-compatible `FontFamily`.

> **2024-11-20 update:** Avalonia-specific `Application`, `Window`, `Page`, `ContentView`, and `Layout` handlers now exist and are registered by `UseMauiAvaloniaHost()`. The window host injects the native `Window`/`Application` instances into each `IMauiContext`, so calling `CreateWindow` from a MAUI app produces an Avalonia visual tree backed by the new layout panel. Task 4 is now complete as well—Avalonia brush/font/stroke conversion helpers, plus a custom `FontManager`/embedded font loader, apply MAUI styling data to Avalonia controls. The next milestone for Phase 3 is expanding those primitives into control-level handlers (Phase 4 item 1).

> **2024-12-05 update:** Navigation primitives now route through Avalonia-native surfaces. `UseMauiAvaloniaHost()` wires an `AvaloniaNavigationRoot` that owns custom chrome (title bar buttons, drag rectangles), an `AvaloniaToolbar` host, and a true menu bar. New handlers cover `NavigationPage`/stack navigation, `FlyoutPage`/Shell, and the high-level toolbar/menu abstractions so MAUI toolbars, menu bars, and flyouts render inside Avalonia `SplitView`/`ContentControl` instances without touching dotnet/maui. This completes Phase 4 task 3.

> **2025-11-07 update:** `GraphicsView` (and toolkit `DrawingView`) now run on Avalonia via a dedicated control that leases the Skia canvas from Avalonia’s renderer and renders MAUI `IDrawable` content through `Microsoft.Maui.Graphics.Skia`. The handler also forwards hover/touch gestures so signature pads and custom charts get the same interaction stream as on other backends.

## Phase 4 – Control Coverage & Feature Parity
1. [x] Port the high-priority interactive controls (Label, Button, Image, Entry/Editor, CheckBox, RadioButton, Slider, ProgressBar, Switch, Date/Time pickers) by creating Avalonia-specific handler classes and registering them via `ConfigureMauiHandlers`.
2. [x] Add layout-aware controls and containers (StackLayout, Grid, ScrollView, CollectionView) ensuring virtualization or scrolling behavior uses native Avalonia controls where possible.
3. [x] Implement navigation surfaces (NavigationPage, FlyoutPage/Shell scaffolding) plus menu/toolbar integration so desktop UX features (menu bar, title bar buttons, drag rectangles) work similarly to Windows/macOS backends.
4. [x] Host `GraphicsView`/`DrawingView` via Avalonia’s Skia renderer (leveraging `extern/Avalonia` and `Microsoft.Maui.Graphics.Skia`) to enable custom drawing, charts, and signature pads.
5. [x] Ensure input, accessibility, and platform services (pointer gestures, keyboard focus, text input, clipboard, drag & drop) route through Avalonia APIs and raise MAUI’s `IView` events.

> **2026-02-14 update:** Input and accessibility plumbing is now complete. `AvaloniaInputAdapter` bridges pointer, drag & drop, and focus gestures from Avalonia controls into MAUI `GestureRecognizer`s, while the text-entry handlers drive `TextInputOptions` so keyboard state stays in sync with `IView`. `UseMauiAvaloniaHost()` now registers clipboard and semantic screen-reader services backed by Avalonia’s dispatcher, and the shared view mapper applies automation identifiers plus semantics to every control. This closes Phase 4 task 5.

## Phase 5 – Samples, Tooling, and Distribution
1. [ ] Produce developer-facing docs and quick-start guides (how to wrap an existing MAUI project with `UseMauiAvaloniaHost`, platform requirements, known gaps).
2. [ ] Create sample templates (dotnet new or project scaffolds) that show a working MAUI-on-Avalonia app, including multi-window and theme-switching scenarios.
3. [ ] Add automated tests: unit tests for handler mapping, UI tests leveraging Avalonia’s headless test framework, and smoke tests that launch the sample app on CI.
4. [ ] Define build/pack pipelines (CI scripts, NuGet packaging metadata, versioning) so the backend can be shipped independently of dotnet/maui yet stay in lockstep with new MAUI releases.
5. [ ] Establish a stabilization checklist (API review, breaking-change doc, release notes) to gate each milestone before publishing preview packages.

## Phase 6 – Parity Gap Inventory & Execution Plan
Even with the milestones above, the Avalonia backend still trails the Android/iOS/MacCatalyst/Windows hosts in multiple areas. This phase tracks everything required to reach “drop-in replacement” parity.

### 6.1 Control & Layout Coverage
- [ ] Complete the remaining handler surface: `ActivityIndicator`, `RefreshView`, `CarouselView`, `IndicatorView`, `Stepper`, `Picker`/`PickerBase`, `SearchBar`, `Map`, `WebView`, `MediaElement`, `SwipeView`, `BlazorWebView`, `MenuItem`, `TabbedPage`/`ShellSection` specific views, `MenuFlyout`, and toolkit primitives (e.g., `Popup`, `Expander`, `DrawingView` gesture extensions).
- [ ] Implement virtualization-aware layouts for `CollectionView`/`ListView` equivalent to Android/iOS (item recycling, header/footer, empty views, incremental loading).
- [ ] Ensure custom layout primitives (`FlexLayout`, `AbsoluteLayout`, `Grid` spacing, safe-area insets) match MAUI semantics under high-DPI and window-resize scenarios.
- [ ] Audit visual state management (VSM) and styles so XAML triggers update Avalonia controls the same way they do on other backends.

### 6.2 Navigation, Shell, and Windowing
- [ ] Finish Shell support: keep `CurrentPage`, flyout selection, tab bar selection, toolbar items, and route navigation events (navigating/navigated) in sync with MAUI’s shell controller.
- [ ] Implement the full `NavigationPage` stack (back button visibility, navigation transitions, modal stack) and wire drag rectangles/title bar customization through `IWindow.TitleBar`.
- [ ] Support `AppThemeBinding`, multi-window scenarios (opening secondary windows, closing, focus) and host-level services such as `IWindowLifecycleHooks`.
- [ ] Add menu bar parity (platform menu merging, keyboard shortcuts) and custom chrome (drag rectangles, passthrough elements, caption buttons).

### 6.3 Platform Services & Essentials APIs
- [ ] Bridge MAUI Essentials APIs to Avalonia or desktop equivalents: AppActions, Browser, Clipboard rich formats, Email/Sms, FilePicker/FolderPicker, Launcher, MediaPicker, Permissions, Preferences, SecureStorage, Share, Vibration, Geolocation, Geocoding, Connectivity, DeviceInformation, Haptics, Speech, and Sensors.
- [ ] Provide fallbacks/feature flags where Avalonia or desktop OS lacks primitives, and document limitations clearly.
- [ ] Flesh out accessibility: semantics tree export, focus visuals, screen reader hints, text scaling, and high-contrast theme propagation.
- [x] Supply desktop-aware `DeviceInfo` and `AppInfo` implementations so MAUI Essentials APIs return correct model/OS/app metadata on the Avalonia host.

### 6.4 Input, Gestures, and Drag/Drop
- [ ] Finish gesture routing: pointer pressed/moved/released events, hover, pinch, pan, swipe, long-press, double-tap, and drag gestures must raise the same recognizer callbacks as existing backends.
- [ ] Support IME/text input options (capitalization, suggestions, keyboard types) and hardware keyboard shortcuts.
- [ ] Implement drag/drop between MAUI views and the desktop, including data transfer formats and drop targets.

### 6.5 Graphics, Media, and Performance
- [ ] Validate `GraphicsView` under hardware acceleration, high-DPI, and frame throttling. Add headless tests using Avalonia’s compositor.
- [ ] Implement multimedia primitives: audio playback, video, camera preview (where Avalonia supports it) or documented limitations.
- [ ] Tighten render loops (invalidate/measure) to avoid layout thrash on window resize and support per-monitor DPI awareness.

### 6.6 Tooling, Hot Reload, Diagnostics
- [ ] Verify XAML + .NET hot reload across large edits and multiple windows; fix any Avalonia-specific limitations.
- [ ] Expose diagnostics toggles (DevTools, tracing, layout visualizers) via builder APIs and CLI flags.
- [ ] Provide VS / VS Code launch configs and debugging instructions mirroring the official MAUI workloads.

### 6.7 Testing, Samples, and Distribution
- [ ] Build sample matrix (Shell navigation, tabs, flyout, data grids, drag/drop, graphics) and ensure they run on CI.
- [ ] Add automated regression tests: handler unit tests, integration tests using Avalonia’s headless environment, and UI smoke tests via Appium or Avalonia UITest harnesses.
- [ ] Stand up CI pipelines for Linux/macOS/Windows, publish nightly NuGet builds, and document versioning/servicing policy.
- [ ] Deliver `dotnet new maui-avalonia` template plus migration guidance for existing MAUI apps.

### 6.8 Stabilization Checklist
- [ ] Performance profiling vs. Windows/macOS backends (layout time, memory, input latency).
- [ ] Accessibility audit (screen reader, keyboard navigation) and WCAG compliance notes.
- [ ] Security review (sandboxing, clipboard/data access), localization/globalization audit, and failure-mode coverage (graceful fallback when services missing).
- [ ] Define release criteria: test coverage %, blocking bug categories, documentation readiness, and support policy before declaring preview/GA.
