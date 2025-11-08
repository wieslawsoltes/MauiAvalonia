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
1. [ ] `Map` handler — render pins, shapes, and camera updates through Avalonia mapping primitives (or document a desktop substitute).
2. [ ] `WebView` handler — host WebView2 (Win32) or Avalonia web components with MAUI navigation events, JS interop, and cookie management.
3. [ ] `MediaElement` handler — wire audio/video playback with transport controls, transport commands, and buffering events.
4. [x] `MenuItem`/`MenuFlyout` parity — surface MAUI flyout menus (including contexts) via Avalonia menu infrastructure.
5. [ ] `BlazorWebView` + `TabbedPage`/`ShellSection` surfaces — embed the Blazor desktop host and ensure tab/shell chrome renders with Avalonia navigation root.
6. [x] Toolkit primitives — stand up `Popup`, `Expander`, and `DrawingView` gesture extensions so CommunityToolkit components behave the same as on Android/iOS.
7. [x] Virtualization-aware `CollectionView`/`ListView` layouts — add item recycling, header/footer, empty view, and incremental loading parity with Android/iOS implementations.
8. [x] Layout fidelity — bring `FlexLayout`, `AbsoluteLayout`, `Grid` spacing, and safe-area handling in line with MAUI semantics under high-DPI and live window resize.
9. [x] Visual state + styling parity — ensure VSM triggers, styles, and `AppThemeBinding` updates propagate to Avalonia controls like they do on other backends.

> **2024-12-09 update:** Avalonia-native handlers now exist for `ActivityIndicator` (indeterminate spinner backed by `ProgressBar`), `BoxView`/`ShapeView` (translating MAUI fills, corner radii, and stroke info onto an Avalonia `Border`), `Stepper` (wrapping `NumericUpDown` for min/max/value/interval updates), `Picker` (binding MAUI items to an Avalonia `ComboBox`, including title/placeholder colors and drop-down state), `SearchBar` (TextBox-backed entry honoring fonts/colors/keyboards and raising `SearchButtonPressed`), and `IndicatorView` (StackPanel of ellipse/rectangle glyphs with selection highlighting and windowing). These are registered via `UseMauiAvaloniaHost()`, so basic loading spinners, rectangular accents, numeric steppers, text pickers, search inputs, and carousel indicators render on the desktop host. Remaining controls listed above still need coverage.

> **2025-02-14 update:** `RefreshView`, `CarouselView`, and `SwipeView` now run on Avalonia. Pull-to-refresh is backed by `RefreshContainer` (respecting `IsRefreshing`, `RefreshColor`, and gesture enabling), `CarouselView` uses the native `Carousel` control with MAUI data templates, and `SwipeView` exposes per-edge swipe items via overlays plus menu-item handlers (including the new `AvaloniaSwipeItemMenuItemHandler`). These handlers are wired up through `UseMauiAvaloniaHost()`, so apps relying on pull-to-refresh, carousels, and swipe actions no longer hit `HandlerNotFound` on the desktop backend. Gesture animations and custom swipe view layouts still need refinement, but the major controls are now available for testing.

> **2025-02-15 update:** CommunityToolkit primitives are now wired up: `Popup` displays through an Avalonia `Popup` host (anchors, sizing, light dismiss, and dismiss callbacks), `Expander` wraps Avalonia’s native `Expander` so header/content toggling and directions stay in sync, and `DrawingView` continues to reuse the `GraphicsView` pipeline for gesture capture. Toolkit controls no longer fall back to the default handler or throw on load.

> **2025-11-08 update:** `CollectionView` now renders through a virtualized Avalonia `ListBox` that composes MAUI headers, footers, empty views, and item templates into a recycling `VirtualizingStackPanel`. The handler translates MAUI data into adapter items so header/footer views participate in layout without breaking selection, surfaces the `EmptyView` when the source runs dry, and forwards `ItemsViewScrolled` + `RemainingItemsThreshold` events after tracking realized indexes from the panel. Incremental loading callbacks therefore fire at the same time as Android/iOS, and virtualization keeps scrolling smooth even with thousands of rows.

> **2025-11-08 update:** Avalonia's `ActualThemeVariant` now drives MAUI's theme lifecycle. `AvaloniaMauiApplication` immediately calls `IApplication.ThemeChanged()` during startup and whenever Avalonia raises `ActualThemeVariantChanged`, which updates `Application.PlatformAppTheme` and emits the `__MAUI_ApplicationTheme__` dynamic resource. `AppThemeBinding`, dynamic styles, and VisualState setters now refresh against light/dark toggles exactly like the other MAUI backends.

> **2025-11-09 update:** Menu infrastructure is now parity-complete: `AvaloniaMenuBuilder` translates `MenuBar`/`MenuFlyout` hierarchies into Avalonia menus with icons, keyboard accelerators, sub-menus, and separators, and `IContextFlyoutElement.ContextFlyout` is wired so right-click flyouts display the same MAUI menu tree.

> **2025-11-10 update:** Grid spacing and safe-area behavior now mirror the other backends. `AvaloniaGridLayoutHandler` propagates `RowSpacing`/`ColumnSpacing` directly to the native grid so high-DPI resizes keep consistent gutters, and the navigation root exposes title/menu/toolbar chrome as safe-area insets. Pages receive those insets via `SetSafeAreaInsets`, while non-page content gets padding automatically, so toggling `UseSafeArea`/`IgnoreSafeArea` produces the expected edge-to-edge experience even when custom chrome is enabled.
> 
> **2026-03-29 update:** `TabbedPage` now rides on a dedicated Avalonia `TabControl` handler. The mapper keeps MAUI’s `Children`, `CurrentPage`, titles, icons, and bar colors synchronized with native headers, and Shell toolbars update as tabs change. `BlazorWebView` remains unimplemented, so hybrid tabs still need the web host work tracked below before this checklist item can be marked complete.

#### Remaining work / next steps
- **Web content:** Upgrade every `csproj` to Avalonia 11.2+ so `Avalonia.WebView` and `Avalonia.BlazorWebView` packages can be referenced, then stand up dedicated `AvaloniaWebViewHandler`/`AvaloniaBlazorWebViewHandler` instances that forward MAUI navigation events, JS interop, cookie storage, and hybrid tab scenarios. This unlocks checklist items 2 and 5.
- **Media playback:** After the dependency bump, add an `AvaloniaMediaElementHandler` backed by `Avalonia.Controls.MediaPlayerElement` (from `Avalonia.Controls.MediaPlayer`) so `IMediaElement` exposes buffering state, transport commands, and media source updates equivalent to Android/iOS.
- **Map:** Evaluate `XAML.MapControl.Avalonia` (net9) as a drop-in for pins/polygons and, if it falls short, prototype a Skia-based tile renderer that plugs into `GraphicsView`. Choose the better path, build a `MapHandler`, and document any desktop-only limitations so 6.1 item 1 can be checked off with confidence.

### 6.2 Navigation, Shell, and Windowing
- [ ] Finish Shell support: keep `CurrentPage`, flyout selection, tab bar selection, toolbar items, and route navigation events (navigating/navigated) in sync with MAUI’s shell controller.
- [ ] Implement the full `NavigationPage` stack (back button visibility, navigation transitions, modal stack) and wire drag rectangles/title bar customization through `IWindow.TitleBar`.
- [ ] Support `AppThemeBinding`, multi-window scenarios (opening secondary windows, closing, focus) and host-level services such as `IWindowLifecycleHooks`.
- [ ] Add menu bar parity (platform menu merging, keyboard shortcuts) and custom chrome (drag rectangles, passthrough elements, caption buttons).
- [x] Provide a default Shell flyout presenter so Shell apps render navigation items, track selection, and invoke `OnFlyoutItemSelectedAsync` even without a custom flyout view.

#### Remaining work / next steps
- **Shell orchestration:** Build an `AvaloniaShellPresenter` that mirrors WinUI’s `ShellSectionNavigationManager`, keeping `CurrentPage`, flyout selection, tab headers, and toolbar items synchronized with MAUI’s routing events so Shell apps no longer desync when tabs/flyouts change.
- **Navigation stack + transitions:** Replace the current “last page wins” swapper with a per-window navigation stack that tracks push/pop/modal operations, surfaces back button visibility, and plays Avalonia page transitions while honoring `NavigationPageHandler` APIs (title view, toolbar, drag rectangles).
- **Multi-window + chrome:** Extend `AvaloniaWindowHost` so each MAUI `IWindow` gets its own navigation root, menu bar merge, and caption controls. This infrastructure should also expose `IWindowLifecycleHooks`, AppThemeBinding updates, and window focus events so multi-window parity matches WinUI/MacCatalyst.

> **2026-05-10 update:** The sample app now exercises Shell tabs using a `TabBar` with multiple `ShellContent` entries mapped onto the new Avalonia TabControl handler. Nested `TabbedPage` instances remain unsupported as Shell content (the MAUI core throws before the Avalonia handler can intervene), so the sample keeps the `TabsPage` demo available as a routed page instead. Parity work therefore focuses on real Shell presenters, not on bypassing upstream restrictions.

### 6.3 Platform Services & Essentials APIs
- [ ] Bridge MAUI Essentials APIs to Avalonia or desktop equivalents: AppActions, Browser, Clipboard rich formats, Email/Sms, FilePicker/FolderPicker, Launcher, MediaPicker, Permissions, Preferences, SecureStorage, Share, Vibration, Geolocation, Geocoding, Connectivity, DeviceInformation, Haptics, Speech, and Sensors.
- [ ] Provide fallbacks/feature flags where Avalonia or desktop OS lacks primitives, and document limitations clearly.
- [ ] Flesh out accessibility: semantics tree export, focus visuals, screen reader hints, text scaling, and high-contrast theme propagation.
- [x] Supply desktop-aware `DeviceInfo` and `AppInfo` implementations so MAUI Essentials APIs return correct model/OS/app metadata on the Avalonia host.
- [x] Update `DeviceDisplay` to report the active window’s scaled size and refresh when screen configuration changes.

#### Remaining work / next steps
- **Essentials driver map:** Inventory each MAUI Essentials API, decide whether it can be implemented purely in managed code (e.g., Preferences, SecureStorage) or needs platform shims (Connectivity, FilePicker, MediaPicker), and scaffold the corresponding Avalonia service registrations so calls stop throwing.
- **Capability detection + fallbacks:** Add feature flags/capability queries to `UseMauiAvaloniaHost()` so apps can opt into APIs that depend on OS hooks (e.g., Launcher, Share) or gracefully disable them when unavailable, and document those trade-offs in the repo.
- **Accessibility investments:** Export Avalonia’s accessibility tree into MAUI semantics, ensure focus visuals and screen-reader hints stay in sync with theme/high-contrast toggles, and add regression tests that cover text scaling plus semantic descriptions.

### 6.4 Input, Gestures, and Drag/Drop
- [x] Finish gesture routing: pointer pressed/moved/released events, hover, pinch, pan, swipe, long-press, double-tap, and drag gestures must raise the same recognizer callbacks as existing backends.
- [x] Support IME/text input options (capitalization, suggestions, keyboard types) and hardware keyboard shortcuts.
- [x] Implement drag/drop between MAUI views and the desktop, including data transfer formats and drop targets.

> **2025-02-15 update:** `AvaloniaInputAdapter` now tracks pointer contacts so tap, double-tap, pan, pinch, swipe, and drag recognizers fire with the same semantics as WinUI/MacCatalyst, while drop targets consume text, files, URIs, and bitmaps from Avalonia's `IDataObject`. Menu/Flyout accelerators feed into Avalonia `HotKey`/`InputGesture` so hardware shortcuts are wired alongside the existing IME/TextInput option plumbing. (A dedicated long-press recognizer still isn't exposed by the MAUI 8 APIs, so there is no public surface to attach yet.)

#### Remaining work / next steps
- **Rich drag/drop payloads:** The current adapter serializes basic text/files only; wire Avalonia’s custom bitmap/URI format IDs into MAUI’s `DataPackageView` so `Contains*`/`Get*Async` surface bitmaps, streams, and custom payloads identical to WinUI/macOS.
- **IME + accelerators:** Hook Avalonia’s IME composition events and text-input accelerators into `ITextInput` so `Entry`/`Editor` honor capitalization, suggestion, and shortcut hints, and ensure hardware accelerators show up on menu/tooltips the same way they do on other backends.
- **Regression coverage:** Extend the gesture/drag-drop sample page (and future automated tests) to cover these richer payloads plus IME scenarios so we can prove parity before flipping the remaining checkboxes permanently.

### 6.5 Graphics, Media, and Performance
- [ ] Validate `GraphicsView` under hardware acceleration, high-DPI, and frame throttling. Add headless tests using Avalonia’s compositor.
- [ ] Implement multimedia primitives: audio playback, video, camera preview (where Avalonia supports it) or documented limitations.
- [ ] Tighten render loops (invalidate/measure) to avoid layout thrash on window resize and support per-monitor DPI awareness.

#### Remaining work / next steps
- **GraphicsView stress harness:** Add headless compositor tests that render `GraphicsView` scenes at multiple DPIs/backends, compare output bitmaps, and measure frame timing so we can tune throttling logic when the window is occluded or resized rapidly.
- **Media pipeline:** Reuse the `Avalonia.Controls.MediaPlayerElement` integration from 6.1 to deliver full `IMediaElement` coverage here, including buffering/command events, DRM or codec limitations, and sample pages that play audio/video streams on each OS.
- **Perf instrumentation:** Tie Avalonia’s per-monitor DPI/change events into layout invalidation, profile measure/arrange spikes during live resize, and add EventSource/ETW counters so we can document render loop timing regressions relative to WinUI.

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

---

## 2024-12-09 – MAUI Integration Review

Latest audit against the upstream `dotnet/maui` Windows/MacCatalyst hosts shows that the Avalonia backend still only offers a thin slice of the MAUI surface. Several roadmap items marked as “done” above are either partial or missing entirely. The notes below capture the concrete deltas followed by a remediation plan with numbered checkable tasks.

### Detailed Findings

#### Hosting & Lifetime
- `AvaloniaApplicationHandler` still logs `IApplication.OpenWindow`/`CloseWindow` as “not implemented”, so MAUI APIs that open secondary windows or close them programmatically are broken, whereas `MauiWinUIApplication`/`MauiAppBuilder` on WinUI wire those commands into the system window manager (`src/Microsoft.Maui.Avalonia/Handlers/Application/AvaloniaApplicationHandler.cs:42-49`).
- `AvaloniaWindowHost` always creates a single placeholder `AvaloniaWindow` inside `IClassicDesktopStyleApplicationLifetime` and never keeps a lookup from MAUI `IWindow` to Avalonia native windows. Desktop windows therefore cannot be reopened, destroyed, or tracked the way WinUI/MacCatalyst hosts do, and the `ISingleViewApplicationLifetime` code path just shows static text instead of the MAUI visual tree (`src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:25-105`).
- The custom `IAvaloniaNavigationRoot` chrome uses raw MAUI drag rectangles and does not adjust for monitor DPI or window resize, so caption buttons/drag regions drift on scaled displays (`src/Microsoft.Maui.Avalonia/Navigation/AvaloniaNavigationRoot.cs:74-214`).

#### Handler Coverage & Quality
- `UseMauiAvaloniaHost()` registers only a subset of handlers (buttons, labels, pickers, CollectionView, Toolbar). Core MAUI controls such as `ActivityIndicator`, `BoxView`, `Border`, `Frame`, `IndicatorView`, `CarouselView`, `RefreshView`, `SwipeView`, `Stepper`, `Picker`, `SearchBar`, `ListView`, `Map`, `WebView`, `MediaElement`, `TabbedPage`, `BlazorWebView`, and toolkit primitives are not wired at all (`src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs:69-121`).
- `AvaloniaButtonHandler` maps text/stroke but the `ImageSourcePartLoader` intentionally no-ops, so `ImageButton`, `ContentLayout`, and combined text+icon scenarios are unsupported (`src/Microsoft.Maui.Avalonia/Handlers/Button/AvaloniaButtonHandler.cs:14-158`).
- `AvaloniaCollectionViewHandler` wraps a plain Avalonia `ListBox` with no item recycling, header/footer, `ItemsLayout`, or empty-view support, causing large data sets to stutter compared to MAUI’s virtualization on other platforms (`src/Microsoft.Maui.Avalonia/Handlers/CollectionView/AvaloniaCollectionViewHandler.cs:21-205`).
- The navigation stack manager simply replaces the `ContentControl` with the last view in the stack; there is no back-stack tracking, navigation animation, modal support, or integration with `NavigationPage`/Shell navigation events (`src/Microsoft.Maui.Avalonia/Handlers/Navigation/AvaloniaStackNavigationManager.cs:8-55`).
- `AvaloniaToolbar` is a bespoke layout that ignores `ToolbarItem.Order`, keyboard accelerators, overflow menus, and dynamic toolbar updates. It also loads toolbar icons without caching or error handling (`src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs:21-277`).
- `AvaloniaScrollViewHandler.MapRequestScrollTo` disregards `ScrollToPosition` and animation semantics, so scroll-to requests jump instantly regardless of the `ScrollToRequest` arguments (`src/Microsoft.Maui.Avalonia/Handlers/ScrollView/AvaloniaScrollViewHandler.cs:17-150`).

#### Essentials & Platform Services
- Only `AppInfo`, `DeviceInfo`, `DeviceDisplay`, `Clipboard`, and `SemanticScreenReader` are implemented; the rest of MAUI Essentials (Connectivity, AppActions, Browser, Launcher, FilePicker, Permissions, Sensors, Share, Preferences, SecureStorage, etc.) are unimplemented, so any call to those APIs throws (`src/Microsoft.Maui.Avalonia/ApplicationModel` and `src/Microsoft.Maui.Avalonia/Devices`).
- `DeviceDisplay.KeepScreenOn` simply stores a boolean and never engages the OS, so that API is non-functional on Avalonia hosts (`src/Microsoft.Maui.Avalonia/Devices/AvaloniaDeviceDisplay.cs:23-78`).
- `AvaloniaTicker` exposes `SystemEnabled` but never toggles it or reacts to suspend/resume, so animations continue running even when the window is backgrounded (`src/Microsoft.Maui.Avalonia/Animations/AvaloniaTicker.cs:10-58`).
- `AvaloniaImageSourceLoader` loads every image into memory using a static `HttpClient` and never caches bitmaps or honors MAUI’s caching policies, which is a mismatch with the official handlers and leaks unmanaged resources on repeated updates (`src/Microsoft.Maui.Avalonia/Handlers/Image/AvaloniaImageSourceLoader.cs:10-109`).

#### Input, Gestures, and Accessibility
- `AvaloniaInputAdapter` forwards pointer entered/moved/pressed events but does not raise MAUI’s higher-level gestures (tap, double-tap, swipe, pinch, pan, long-press). Drag/drop is limited to plain text payloads, so the gesture surface still trails WinUI/macOS (`src/Microsoft.Maui.Avalonia/Input/AvaloniaInputAdapter.cs:17-408`).
- `GestureExtensions` relies on reflection to call private `Send*` methods and only targets `PointerGestureRecognizer`/drag recognizers. Tap, swipe, and pinch recognizers therefore never fire on Avalonia (`src/Microsoft.Maui.Avalonia/Input/GestureExtensions.cs:8-85`).
- Menu/toolbar accessibility metadata (accelerators, checkmarks) is missing, and no tests exist to verify screen-reader output beyond the basic `SemanticScreenReader`.

#### Tooling, Samples, and Quality Gates
- The `tests/` folder is empty, so there is no automated validation comparable to the handler/unit tests in `dotnet/maui`.
- `samples/MauiAvalonia.SampleApp` only exercises a single `ContentPage`; there is no Shell, navigation, multi-window, or Essentials sample coverage.
- The roadmap in this file reports Phase 4 as complete even though the handler and service gaps above demonstrate it is still in progress. Without a refreshed plan contributors do not know which pieces are actually outstanding.

### Remediation Tasks
1. [ ] **Implement multi-window lifetime parity** — Wire `IApplication.OpenWindow`/`CloseWindow`, keep a registry of MAUI `IWindow` to Avalonia `Window`, and make the `ISingleViewApplicationLifetime` host render the MAUI visual tree. Update `AvaloniaWindowHost`/`AvaloniaApplicationHandler` to mirror the WinUI `WindowManager` flow (window scopes, `LifecycleEvents`, theme propagation).
2. [ ] **Ship a handler parity matrix and cover missing core controls** — catalog every handler in `dotnet/maui` and add Avalonia implementations for ActivityIndicator, BoxView/Border/Frame, Flex/AbsoluteLayout, RefreshView/CarouselView/IndicatorView/SwipeView, Picker/SearchBar/Stepper, WebView/MediaElement/Map, TabbedPage, BlazorWebView, MenuFlyout, and toolkit primitives, registering them in `UseMauiAvaloniaHost()`.
3. [ ] **Refine existing infrastructure** — bring `AvaloniaButtonHandler`, `CollectionView`, `StackNavigation`, `Toolbar`, `MenuBuilder`, and `ScrollView` up to parity (image/text content layouts, virtualization + header/footer, real navigation back stack and animations, toolbar overflow & accelerators, menu icons/shortcuts, animated scroll-to).
4. [ ] **Bridge Essentials and device services** — implement (or document fallbacks for) Connectivity, AppActions, Browser, Launcher, File/Folder pickers, MediaPicker, Permissions, Preferences/SecureStorage, Share, Sensors, Haptics, Vibration, Speech, and make `DeviceDisplay.KeepScreenOn` plus ticker suspension actually affect the OS.
5. [ ] **Complete gesture/input/drag-drop support** — raise tap/double-tap/swipe/pinch/pan/long-press recognizers, expose keyboard accelerators, add rich drag/drop payloads (files, URIs, custom formats), and cover IME/text-input options equal to WinUI/macOS.
6. [ ] **Harden graphics/media/perf** — add image caching/disposal, ensure `GraphicsView` throttles on background threads, align animation tickers with `CompositionTarget`, make drag rectangles DPI-aware, and document media support/limitations.
7. [ ] **Establish tooling, samples, and quality gates** — populate `tests/` with handler + regression tests, create multi-window/Shell/Essentials sample apps, wire CI to run Avalonia headless tests, and keep this plan in sync with reality (checked items must have corresponding shipped code + tests).
