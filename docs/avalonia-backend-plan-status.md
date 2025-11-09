# Avalonia Backend Implementation Plan

The goal of `Microsoft.Maui.Avalonia` is to let any .NET MAUI app run on top of Avalonia without forking `dotnet/maui`. The backend is wired up through `.UseMauiAvaloniaHost()` (`src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs`) and ships its own handlers, services, and host builders. This document captures the current implementation status (May 2026), compares it to the built-in Android/iOS/macOS/Windows backends, and lists the work required to reach feature parity.

## 2026-05-12 – Backend Status Review

We reviewed the Avalonia host in `src/Microsoft.Maui.Avalonia/**` together with the upstream MAUI hosts in `extern/maui/src/Core/src/Platform/*`. The table below summarizes where Avalonia stands relative to the in-box backends.

### Comparative Status Table

| Feature area | Android | iOS / MacCatalyst | Windows | Avalonia (preview) |
| --- | --- | --- | --- | --- |
| Bootstrapping & DI | `MauiApplication` builds the app, calls `MakeApplicationScope`, and registers `ActivityLifecycleCallbacks` to keep `IMauiContext` scopes aligned with Activities (extern/maui/src/Core/src/Platform/Android/MauiApplication.cs:16-124, extern/maui/src/Core/src/MauiContextExtensions.cs:38-74). | `MauiUIApplicationDelegate`/`MauiUISceneDelegate` create a `MauiApp`, spawn one scope per scene/window, and surface those scopes through `IPlatformApplication` (extern/maui/src/Core/src/Platform/iOS/MauiUIApplicationDelegate.cs:12-191, extern/maui/src/Core/src/Platform/iOS/ApplicationExtensions.cs:25-118). | `MauiWinUIApplication` builds the app once, then `ApplicationExtensions.CreatePlatformWindow` creates a WinUI window scope via `MakeWindowScope` for each MAUI `Window` (extern/maui/src/Core/src/Platform/Windows/MauiWinUIApplication.cs:21-87, extern/maui/src/Core/src/Platform/Windows/ApplicationExtensions.cs:8-33). | `AvaloniaMauiApplication` wires MAUI into an Avalonia `Application`, but only `IClassicDesktopStyleApplicationLifetime` is fully supported—`ISingleViewApplicationLifetime` renders a static placeholder and window scopes are created manually instead of using `MakeWindowScope` (src/Microsoft.Maui.Avalonia/AvaloniaMauiApplication.cs:16-122, src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:36-153). |
| Window management & lifetime events | `ApplicationExtensions.CreatePlatformWindow` attaches each `Activity` to a MAUI `Window`, raises Android lifecycle events, and implements multi-window requests via intents (extern/maui/src/Core/src/Platform/Android/ApplicationExtensions.cs:14-95). | `ApplicationExtensions.RequestNewWindow` hooks into UIKit scenes and uses `WindowStateManager` to keep per-scene state and lifecycle callbacks in sync (extern/maui/src/Core/src/Platform/iOS/ApplicationExtensions.cs:18-118). | `MauiWinUIWindow` + `NavigationRootManager` manage chrome, drag rectangles, and `WindowsLifecycle` events for every window (extern/maui/src/Core/src/Platform/Windows/NavigationRootManager.cs:8-199). | `AvaloniaWindowHost` manually spins up `AvaloniaWindow` instances, injects them into MAUI via `SetWindowHandler`, and tracks them in a private registry, but `ISingleView` lifetimes, `MakeWindowScope`, and a host-managed `CloseWindow` flow are still missing (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:36-205, src/Microsoft.Maui.Avalonia/Hosting/IAvaloniaWindowHost.cs:7-24). |
| Navigation root & shell chrome | Android relies on `NavigationRootManager`/`DrawerLayout` to host Shell, Flyout, toolbars, and system insets (extern/maui/src/Core/src/Platform/Android/Navigation/NavigationRootManager.cs:14-189). | UIKit-based hosts use `WindowStateManager`, `NavigationRenderer`, and scene delegates to manage Shell/Flyout chrome and toolbars with native transitions. | Windows uses `NavigationRootManager` + `WindowRootView` to map title bars, drag rectangles, and menu bars to WinUI primitives (extern/maui/src/Core/src/Platform/Windows/NavigationRootManager.cs:8-199). | Avalonia ships `AvaloniaNavigationRoot`/`AvaloniaToolbar`/`AvaloniaStackNavigationManager`, but drag rectangles are manually hit-tested, toolbar overflow/back-ordering does not match MAUI semantics, and only a single cross-fade transition exists (src/Microsoft.Maui.Avalonia/Navigation/AvaloniaNavigationRoot.cs:22-533, src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs:21-277, src/Microsoft.Maui.Avalonia/Handlers/Navigation/AvaloniaStackNavigationManager.cs:13-220). |
| Handler coverage & quality | All MAUI controls have handler partials specialized per TFM; Android implementations live alongside platform helpers for background/font/gesture support. | iOS/MacCatalyst share handlers with platform-specific partial classes to reach full coverage (Handlers + Platform/iOS). | Windows implements every core handler plus WinUI-only features (pointer hover, menu bar host, status bar). | `.UseMauiAvaloniaHost()` now registers WebView/BlazorWebView, MediaElement, Map, ListView, MenuFlyout, and TwoPaneView handlers in addition to the existing ~30 controls, but several of them still lack advanced features (pin elements, grouped cells, toolbar semantics, stream-based media, etc.) (src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs:70-150, src/Microsoft.Maui.Avalonia/Handlers/**). |
| Essentials & device services | Uses the official Essentials implementations (Connectivity, Launcher, Sensors, AppActions, etc.) per platform (`extern/maui/src/Essentials/src/**`). | Same Essentials stack, plus scene-aware APIs (iOS/macOS). | Same Essentials stack with WinUI projections (Windowing, Notifications). | Avalonia layer only provides `AppInfo`, `DeviceInfo`, `DeviceDisplay`, `Clipboard`, and `SemanticScreenReader`; everything else throws `NotImplementedException` (src/Microsoft.Maui.Avalonia/ApplicationModel/*.cs, src/Microsoft.Maui.Avalonia/Devices/*.cs). |
| Input, gestures & drag/drop | Android uses native gesture detectors and translates them through `GestureManager`. | UIKit gestures, IME hints, drag/drop, and pointer APIs are mapped via platform renderers. | WinUI pointer/keyboard events feed MAUI recognizers and accessibility patterns. | `AvaloniaInputAdapter` forwards pointer/tap/pan/pinch/drag events and basic drag/drop payloads, but there is no long-press, haptics, hardware IME mapping, or semantics integration yet (src/Microsoft.Maui.Avalonia/Input/AvaloniaInputAdapter.cs:31-800). |
| Graphics, media & composition | `GraphicsView` renders through native GPU surfaces; `MediaElement` and `WebView` wrap platform controls (Android Views, WKWebView, WebView2). | Same pattern with Metal/CoreAnimation surfaces and AVFoundation. | Uses WinUI composition, SwapChain panels, WebView2, and MediaPlayerElement. | `AvaloniaGraphicsView` leases the Skia canvas from Avalonia, `AvaloniaWebViewHandler` drives Avalonia.WebView, and LibVLCSharp hosts `MediaElement`, but there are still no graphics/Media stress tests and image loader caching is missing (src/Microsoft.Maui.Avalonia/Handlers/GraphicsView/AvaloniaGraphicsView.cs:15-225, src/Microsoft.Maui.Avalonia/Handlers/WebView/AvaloniaWebViewHandler.cs, src/Microsoft.Maui.Avalonia/Handlers/MediaElement/AvaloniaMediaElementHandler.cs). |
| Tooling, tests & packaging | `dotnet/maui` ships templates, workloads, CI, handler/device tests, and sample galleries (`extern/maui/src/Templates`, `extern/maui/src/Controls/tests`). | Same shared infrastructure. | Same shared infrastructure. | `tests/` is empty, CI/workloads/templates are missing, and only `samples/MauiAvalonia.SampleApp` exists. Publishing, nightly feeds, and documentation have not been defined. |

### Detailed Findings

#### Hosting & Lifetime
- `IAvaloniaWindowHost.AttachLifetime` only sets up real windows when the Avalonia app uses `IClassicDesktopStyleApplicationLifetime`; `ISingleViewApplicationLifetime` falls back to a `TextBlock` placeholder and never surfaces the MAUI visual tree (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:36-64).
- `CreateWindowInternal` builds a raw `MauiContext` by hand, never calls `MakeWindowScope`, and therefore skips the standard MAUI window registrations that upstream handlers and DI extensions expect (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:128-178 vs. extern/maui/src/Core/src/Platform/Windows/ApplicationExtensions.cs:18-31).
- `IAvaloniaWindowHost` exposes only `AttachLifetime` and `OpenWindow`, so `IApplication.CloseWindow` bypasses the host and there is no single place to observe/override MAUI’s window lifecycle (src/Microsoft.Maui.Avalonia/Hosting/IAvaloniaWindowHost.cs:7-24), unlike the Windows `WindowManager` that coordinates activation, drag rectangles, and toolbar updates (extern/maui/src/Core/src/Platform/Windows/NavigationRootManager.cs:8-199).

#### Navigation, Toolbar & Chrome
- `AvaloniaNavigationRoot.SetDragRectangles` simply stores MAUI `Rect` values and compares pointer positions without applying Avalonia’s render scaling or monitor DPI, so custom title bars drift on high-DPI monitors; Windows converts drag rectangles using the display density (src/Microsoft.Maui.Avalonia/Navigation/AvaloniaNavigationRoot.cs:311-468 vs. extern/maui/src/Core/src/Platform/Windows/NavigationRootManager.cs:22-69).
- `AvaloniaToolbar.UpdateToolbarItems` iterates the raw `ToolbarItems` collection and renders a stack of buttons; it ignores `ToolbarItem.Order`, primary/secondary overflow, and keyboard accelerator metadata (src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs:181-227).
- `AvaloniaStackNavigationManager` always cross-fades the last non-modal page, never exposes platform transitions, and does not integrate with Shell navigation events or back-stack semantics beyond calling `NavigationFinished` (src/Microsoft.Maui.Avalonia/Handlers/Navigation/AvaloniaStackNavigationManager.cs:27-220).

#### Handler Coverage & Fidelity
- `.UseMauiAvaloniaHost()` now wires the WebView, BlazorWebView, Map, ListView, MediaElement, MenuFlyout, and TwoPaneView handlers, but these first iterations intentionally skip some parity features (traffic overlays, grouped cells, stream media sources, narrator hints) that are called out below (src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs:70-150).
- `AvaloniaButtonHandler` purposely leaves `ImageSourcePartSetter.SetImageSource` empty, so `ImageButton`, `ContentLayout`, and text+icon buttons never render their image content (src/Microsoft.Maui.Avalonia/Handlers/Button/AvaloniaButtonHandler.cs:34-158).
- `AvaloniaCollectionViewHandler` always hosts items inside an Avalonia `ListBox` with a `VirtualizingStackPanel`, ignoring MAUI’s `IItemsLayout`, horizontal layouts, and grid/vertical spacing options (src/Microsoft.Maui.Avalonia/Handlers/CollectionView/AvaloniaCollectionViewHandler.cs:23-207).
- `AvaloniaScrollViewHandler.MapRequestScrollTo` sets the target offset directly and discards `ScrollToPosition`, `IsAnimated`, and platform animation semantics, so `ScrollView.ScrollToAsync` never animates (src/Microsoft.Maui.Avalonia/Handlers/ScrollView/AvaloniaScrollViewHandler.cs:109-126).

#### Platform Services & Essentials
- Only `AppInfo`, `DeviceInfo`, `DeviceDisplay`, `Clipboard`, and `SemanticScreenReader` are implemented under `src/Microsoft.Maui.Avalonia/ApplicationModel` and `src/Microsoft.Maui.Avalonia/Devices`; everything else from `Microsoft.Maui.Essentials` (Connectivity, Launcher, Preferences, SecureStorage, Sensors, AppActions, etc.) throws.
- `DeviceDisplay.KeepScreenOn` is a boolean flag that never touches the desktop OS, so `IDeviceDisplay.KeepScreenOn = true` has no effect (src/Microsoft.Maui.Avalonia/Devices/AvaloniaDeviceDisplay.cs:23-27).
- `AvaloniaTicker.SystemEnabled` is never toggled when the window is suspended or backgrounded, causing animations to keep ticking off-screen (src/Microsoft.Maui.Avalonia/Animations/AvaloniaTicker.cs:7-69).
- `AvaloniaImageSourceLoader` reads every source into memory with no caching, decoding throttles, or reuse, so image-heavy apps can easily thrash RAM compared to the native handlers that lean on platform caches (src/Microsoft.Maui.Avalonia/Handlers/Image/AvaloniaImageSourceLoader.cs:13-168).

#### Input, Gestures, Accessibility
- `AvaloniaInputAdapter` handles pointer/tap/pan/pinch/drag gestures but never inspects `LongPressGestureRecognizer`, haptics, or IME options, so long-press commands, tactile feedback, and hardware keyboard hints never fire (src/Microsoft.Maui.Avalonia/Input/AvaloniaInputAdapter.cs:31-800).
- Menu and toolbar accessibility metadata (automation names, toggle state, checked icons) are not propagated—`AvaloniaMenuBuilder` maps text and hotkeys only (src/Microsoft.Maui.Avalonia/Navigation/AvaloniaMenuBuilder.cs:15-142), and `AvaloniaToolbar` presents raw Avalonia buttons without exposing semantics to screen readers (src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs:21-277).
- There is no equivalent to the upstream semantics tree export (`SemanticManager`), so `AutomationProperties`/`SemanticProperties` set on MAUI views do not reach Avalonia accessibility APIs.

#### Tooling, Samples & QA
- `tests/` contains no unit tests, integration tests, or headless UI automation.
- Only `samples/MauiAvalonia.SampleApp` exists, and it exercises a small subset of controls (basic tabs, text, and buttons).
- There are no CI workflows, nightly packages, or `dotnet new maui-avalonia` templates, so contributors cannot validate changes the way they can on `dotnet/maui`.

## Completion Plan (2026)

The numbered checklist below is the actionable plan to bring the Avalonia backend to parity. Tasks are intentionally detailed so they can be split across contributors while keeping the end goal clear.

1. [x] **Finish lifetime & window scopes**
   - ✅ `ISingleViewApplicationLifetime` now builds a full MAUI window tree and routes lifecycle events through the shared host (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:39-210).
   - ✅ `AvaloniaMauiContextExtensions` provides `MakeApplicationScope`/`MakeWindowScope` so every window scope is initialized identically to the in-box backends (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaMauiContextExtensions.cs:11-38, src/Microsoft.Maui.Avalonia/AvaloniaMauiApplication.cs:49-71).
   - ✅ `IAvaloniaWindowHost` exposes `Open/Close/Enumerate/TryGet` APIs and `AvaloniaApplicationHandler.MapCloseWindow` now delegates to the centralized host, matching the WinUI `WindowManager` flow (src/Microsoft.Maui.Avalonia/Hosting/IAvaloniaWindowHost.cs:12-44, src/Microsoft.Maui.Avalonia/Handlers/Application/AvaloniaApplicationHandler.cs:43-64).
   - ✅ Window activation, deactivation, theme changes, and lifecycle wiring surface through `AvaloniaLifecycle` so `ILifecycleBuilder` parity matches Android/iOS/WinUI (src/Microsoft.Maui.Avalonia/Lifecycle/AvaloniaLifecycle*.cs, src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:290-339).

2. [x] **Ship navigation + chrome parity**
   - ✅ `AvaloniaNavigationRoot` now watches `Window.ScalingChanged`, scales drag rectangles to render DPI, and raises safe-area updates so `AvaloniaWindowHandler` can reapply padding (src/Microsoft.Maui.Avalonia/Navigation/AvaloniaNavigationRoot.cs:200-520).
   - ✅ `AvaloniaToolbar` sorts toolbar items by `Order`/`Priority`, renders primary buttons + a menu-based overflow for secondary items, keeps icons/text/hotkeys/checked-state in sync, and reacts to runtime changes (src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs).
   - ✅ `AvaloniaStackNavigationManager` maintains an explicit non-modal stack, selects push/pop/replace transitions, and plays slide animations that mirror the WinUI host while leaving Shell-driven re-rooting glitch-free (src/Microsoft.Maui.Avalonia/Handlers/Navigation/AvaloniaStackNavigationManager.cs).

3. [x] **Complete handler surface**
   - New Avalonia handlers light up `WebView`, `BlazorWebView`, `Map`, `ListView`, `MenuFlyout`, `TwoPaneView`, and `CommunityToolkit.Maui.Views.MediaElement`. These are registered automatically by `.UseMauiAvaloniaHost()` so apps no longer crash when those controls are present (src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs).
   - Each handler focuses on the core MAUI contract: Maps render OpenStreetMap tiles and clickable pins, ListView materializes data templates inside a scroll viewer, MenuFlyout mirrors accelerators/icons, TwoPaneView splits panes based on size, and MediaElement uses LibVLCSharp for file/URI playback. Advanced features (traffic overlays, grouped cells, system transport controls, resource/stream media sources, etc.) are still called out as limitations below.
   - Added the first handler smoke tests (`tests/Microsoft.Maui.Avalonia.Tests`) so regressions in the Map handler or host initialization will fail fast while we expand coverage.

4. [x] **Improve existing handlers**
   - Button + toolbar icons now flow through concrete `ImageSourcePartSetter` implementations with cancellation-aware loaders, so text+icon combinations stay in sync and cached `Bitmap`s are disposed (src/Microsoft.Maui.Avalonia/Handlers/Button/AvaloniaButtonHandler.cs, src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs).
   - `CollectionView` swaps its panel template and scroll direction when `ItemsLayout` changes, so horizontal lists and grids no longer fall back to the default vertical `VirtualizingStackPanel` (src/Microsoft.Maui.Avalonia/Handlers/CollectionView/AvaloniaCollectionViewHandler.cs).
   - `AvaloniaScrollViewHandler.MapRequestScrollTo` now understands both offset-based and `ScrollToRequestedEventArgs` requests, preserving MAUI’s `ScrollToPosition` semantics and animation flags (src/Microsoft.Maui.Avalonia/Handlers/ScrollView/AvaloniaScrollViewHandler.cs).
   - Mapper audit: SwipeView maps the cross-platform background brush, TabbedView honors `IView.Background` alongside bar colors, and Picker/RefreshView were verified against the current MAUI contracts (the planned `IsOpen`/`IsRefreshEnabled` properties do not exist in the GA packages yet, so they’ll be lit up when the upstream APIs land).

5. [ ] **Bridge Essentials & device services**
   - Implement (or document limitations for) Connectivity, AppActions, Browser, Launcher, File/Folder pickers, MediaPicker, Permissions, Preferences, SecureStorage, Share, Sensors, Haptics, Vibration, and Speech APIs, wiring them through Avalonia or OS-specific shims (src/Microsoft.Maui.Avalonia/ApplicationModel, src/Microsoft.Maui.Avalonia/Devices).
   - Make `DeviceDisplay.KeepScreenOn` actually interact with the host OS (e.g., Windows power requests, macOS assertions) instead of storing a boolean (src/Microsoft.Maui.Avalonia/Devices/AvaloniaDeviceDisplay.cs:23-78).
   - Update `AvaloniaTicker` to pause/resume when the app loses focus, mirroring the scheduler logic of other platforms (src/Microsoft.Maui.Avalonia/Animations/AvaloniaTicker.cs:7-69).
   - Add caching/pooling to `AvaloniaImageSourceLoader` and respect `ImageSource` caching directives to avoid re-downloading bitmaps (src/Microsoft.Maui.Avalonia/Handlers/Image/AvaloniaImageSourceLoader.cs:13-168).

6. [x] **Finish input, gestures & accessibility**
   - `AvaloniaInputAdapter` now tracks press durations and motion thresholds so `LongPressGestureRecognizer` instances fire reliably, cancels long presses when drags/pans begin, and centralizes focus adorners for keyboard/touch parity (src/Microsoft.Maui.Avalonia/Input/AvaloniaInputAdapter.cs, src/Microsoft.Maui.Avalonia/Input/LongPressGestureProxy.cs, src/Microsoft.Maui.Avalonia/Input/FocusVisualManager.cs).
   - Introduced `AvaloniaSemanticNode` to push `SemanticProperties`/`AutomationProperties` into Avalonia’s automation tree, and applied it across core UI (view mapper, toolbars, menu flyouts) so screen readers pick up names and hints consistently (src/Microsoft.Maui.Avalonia/Accessibility/AvaloniaSemanticNode.cs, src/Microsoft.Maui.Avalonia/Platform/AvaloniaControlExtensions.cs, src/Microsoft.Maui.Avalonia/Navigation/AvaloniaMenuBuilder.cs, src/Microsoft.Maui.Avalonia/Handlers/Toolbar/AvaloniaToolbar.cs). A focused unit test (`SemanticsTests`) guards this bridge.
   - Added `IHapticFeedback`/`IVibration` implementations plus DI registration so apps can query tactile support without throwing, and documented the current LibVLC dependency gap in host-run tests (src/Microsoft.Maui.Avalonia/Devices/AvaloniaHapticFeedback.cs, src/Microsoft.Maui.Avalonia/Devices/AvaloniaVibration.cs, src/Microsoft.Maui.Avalonia/MauiAvaloniaHostBuilderExtensions.cs, tests/Microsoft.Maui.Avalonia.Tests/SemanticsTests.cs).

7. [ ] **Graphics, media & performance**
   - Add headless `AvaloniaGraphicsView` tests (similar to MAUI’s drawing tests) that render at multiple DPIs/back-ends and validate frame timing/invalidations (src/Microsoft.Maui.Avalonia/Handlers/GraphicsView/AvaloniaGraphicsView.cs:15-225).
   - Implement media primitives (`MediaElement`, audio/video, camera preview) or document platform limitations, ensuring playback state events align with MAUI expectations.
   - Profile layout/measure loops during live resize and idle to ensure the Avalonia host matches WinUI/macOS performance envelopes; tune dispatcher throttling accordingly.

8. [ ] **Tooling, tests & packaging**
   - Populate `tests/` with handler unit tests, Essentials shims tests, and integration tests using Avalonia’s headless runner so parity regressions are caught before release (current folder is empty).
   - Expand `samples/` into a matrix (Shell + Flyout, CollectionView, GraphicsView, drag/drop, Essentials) to demonstrate coverage and serve as manual test beds.
   - Stand up CI (Windows/macOS/Linux), nightly NuGet feeds, and a `dotnet new maui-avalonia` template so developers can try the backend without cloning the repo.
   - Keep this plan in sync with the codebase—checked items must correspond to shipped, tested features, and the comparison table should be refreshed whenever major milestones land.

## Feature Coverage Matrix

| Area | Feature | MAUI expectation | Avalonia status | Notes / gaps |
| --- | --- | --- | --- | --- |
| Hosting & DI | Application host + DI bootstrapping | `MauiApp` builds once, `IPlatformApplication` exposes services, `MakeApplicationScope` wires DI | ⚠️ Partial | `AvaloniaMauiApplication` builds the app but shortcuts the standard scope helpers (`MakeApplicationScope`/`MakeWindowScope`). |
| Hosting & DI | `ISingleViewApplicationLifetime` | `MainView` hosts MAUI content even without desktop lifetime | ❌ Missing | Current implementation shows a static placeholder TextBlock (src/Microsoft.Maui.Avalonia/Hosting/AvaloniaWindowHost.cs:52-63). |
| Hosting & DI | Multi-window open/close | `OpenWindow`/`CloseWindow` route through host, windows tracked and disposed | ⚠️ Partial | `OpenWindow` works through `AvaloniaWindowHost`, but `CloseWindow` bypasses the host and scopes are managed manually. |
| Hosting & DI | Lifecycle/theme propagation | `ILifecycleBuilder` events (startup, resume, theme change) raised per platform | ⚠️ Partial | Startup/theme events fire, but window activation/deactivation + suspend/resume notifications are not wired to Avalonia dispatcher. |
| Hosting & DI | `IMauiContext.MakeWindowScope` parity | Each window scope registers platform services (NavigationRoot, dispatcher, etc.) | ❌ Missing | Windows/Android use `MakeWindowScope`; Avalonia builds a `MauiContext` manually and skips scoped service initialization. |
| Windowing & chrome | Shell/Flyout support | Full Shell navigation with Flyout behavior, menu integration | ⚠️ Partial | `AvaloniaFlyoutViewHandler` exists, but Shell still lacks animations/back-stack parity. |
| Windowing & chrome | Tabbed navigation | `TabbedPage`/`ITabbedView` host toolbars + content with per-tab navigation stacks | ⚠️ Partial | Handler exists but transitions/back-stack logic in `AvaloniaStackNavigationManager` only cross-fades last view. |
| Windowing & chrome | Navigation stack animations & modals | Push/pop animations + modal overlay stack | ⚠️ Partial | Cross-fade only; modal stack is drawn via overlays without animation or Shell event parity. |
| Windowing & chrome | Toolbar/menu parity | Toolbar items honor order, overflow, icons, accelerators | ⚠️ Partial | Items render but ignore `ToolbarItem.Order`, overflow, primary/secondary grouping, and accelerator metadata. |
| Windowing & chrome | Custom title bar + drag rectangles | DPI-aware drag regions, passthrough controls, system buttons | ⚠️ Partial | Custom chrome works but drag rectangles ignore `RenderScaling`, causing drift on DPI-scaled monitors. |
| Windowing & chrome | Multi-window chrome updates | Theme/drag rectangles updated per window, window registry exposed | ❌ Missing | No registry APIs; host cannot broadcast updates across open windows. |
| Handler coverage | Core primitives (Label/Button/Entry/etc.) | All base controls render with text, font, background, focus, states | ⚠️ Partial | Handlers exist but Button images/content layouts are unimplemented. |
| Handler coverage | Layout primitives (StackLayout/Grid/ScrollView) | Layout measurement/margin/padding/ScrollTo semantics | ⚠️ Partial | Layout renders, but `ScrollToPosition` + animated scrolling are ignored. |
| Handler coverage | CollectionView/virtualization | Horizontal/vertical/grid layouts, empty view, item recycling | ⚠️ Partial | Uses `ListBox` with single vertical layout, limited virtualization, no `IItemsLayout` support. |
| Handler coverage | CarouselView/IndicatorView/SwipeView/RefreshView | Same behavior as other backends | ⚠️ Partial | Basic rendering works but lacks gestures/animations parity and SwipeView command consistency. |
| Handler coverage | WebView | Full navigation stack, JS interop, message handlers | ⚠️ Partial | Avalonia.WebView host supports navigation/GoBack/JS execution, but custom user-agent, hybrid message channels, and cookie synchronization are still missing. |
| Handler coverage | MediaElement | Audio/video playback with transport controls/events | ⚠️ Partial | LibVLC-backed handler covers file/URI playback, looping, and `Play/Pause/Seek`, but platform transport controls, resource/stream sources, and keep-screen-on semantics are unimplemented. |
| Handler coverage | Map | Map control with pins, gestures, geolocation integration | ⚠️ Partial | OpenStreetMap tiles and clickable pins render, but there is no traffic overlay, location service, map elements, or `MoveToRegion` animation parity. |
| Handler coverage | BlazorWebView | Embedded WebView host with BlazorWebView service provider | ⚠️ Partial | Avalonia.BlazorWebView manager is registered and handles root components, but static web asset hot reload and multi-window scenarios remain unverified. |
| Handler coverage | MenuFlyout/MenuBar | Menu items with icons, accelerators, check states | ⚠️ Partial | Context menus now rebuild through `AvaloniaMenuFlyoutHandler`, yet accessibility metadata and dynamic item updates still need work. |
| Handler coverage | GraphicsView/DrawingView | Hardware-accelerated drawing surface with gestures | ✅ Done | `AvaloniaGraphicsView` renders via Skia lease and forwards gestures. |
| Handler coverage | DualScreen/TwoPaneView & other Toolkit primitives | Feature parity with official Toolkit views | ⚠️ Partial | TwoPaneView handler splits panes and respects priority, but hinge detection, tall mode rules, and other Toolkit views remain missing. |
| Essentials | AppInfo/DeviceInfo/Clipboard/SemanticScreenReader | Provide platform data + clipboard + semantics | ✅ Done | Implemented and registered via `.UseMauiAvaloniaHost()`. |
| Essentials | DeviceDisplay (info + keep screen on) | Report display metrics + enforce keep-awake flag | ⚠️ Partial | Metrics work, but `KeepScreenOn` is a no-op. |
| Essentials | Preferences/SecureStorage | Persistent key-value storage and encrypted secrets | ❌ Missing | No implementation in `src/Microsoft.Maui.Avalonia/ApplicationModel`. |
| Essentials | Connectivity | Network reachability reporting | ❌ Missing | Unimplemented. |
| Essentials | Launcher/Browser/Share | Launch URIs, open browser, share payloads | ❌ Missing | Unimplemented. |
| Essentials | File/Folder/Media pickers | Cross-platform pickers for files/folders/media | ❌ Missing | Unimplemented. |
| Essentials | MediaPicker/Camera | Capture photos/videos with permissions | ❌ Missing | Unimplemented. |
| Essentials | AppActions | Register and respond to shortcuts | ❌ Missing | Unimplemented. |
| Essentials | Sensors (accelerometer, compass, etc.) | Real-time sensor APIs | ❌ Missing | Unimplemented. |
| Essentials | Geolocation/Geocoding | GPS position + reverse geocoding | ❌ Missing | Unimplemented. |
| Essentials | Permissions | Unified permission requests/results | ❌ Missing | Unimplemented. |
| Essentials | Vibration/Haptics | Trigger device vibration/haptic feedback | ❌ Missing | Unimplemented. |
| Essentials | Speech/Speech-to-text | Speech APIs | ❌ Missing | Unimplemented. |
| Input & accessibility | Pointer/tap/pinch/pan gestures | Raise MAUI gesture recognizers with same semantics | ⚠️ Partial | Adapter covers tap/pan/pinch/drag but not long press or full accelerator mapping. |
| Input & accessibility | Long-press gesture | `LongPressGestureRecognizer` events | ❌ Missing | No mapping in `AvaloniaInputAdapter`. |
| Input & accessibility | Swipe gestures | `SwipeGestureRecognizer` with directional detection | ⚠️ Partial | Swipe detection runs but lacks velocity/direction parity and Shell integration. |
| Input & accessibility | Drag/drop payload richness | Text, files, URIs, bitmaps, custom data | ⚠️ Partial | Text/files/bitmap supported; custom payloads limited. |
| Input & accessibility | Keyboard accelerators + IME hints | Hardware shortcut mapping, IME properties | ❌ Missing | No IME integration; accelerators only applied to menu items. |
| Input & accessibility | Accessibility semantics | `SemanticProperties` exported to platform tree | ❌ Missing | No semantics bridge; menu/toolbar accessibility metadata missing. |
| Graphics, media & perf | GraphicsView hardware acceleration tests | Headless tests run across DPIs/back-ends | ❌ Missing | No automated tests/stress harness. |
| Graphics, media & perf | Image caching/disposal | Image loader honors caching, reuses bitmaps | ❌ Missing | Loader re-downloads/re-decodes every request. |
| Graphics, media & perf | Media pipeline | MediaElement, audio/video playback parity | ❌ Missing | Handler absent. |
| Graphics, media & perf | Web rendering | Modern WebView (WebView2/WebKit) equivalent | ❌ Missing | Handler absent. |
| Graphics, media & perf | DPI-aware layout/resizing | Layout/drag rectangles respond to per-monitor DPI | ⚠️ Partial | Layout works but drag rectangles ignore DPI and resize throttling. |
| Graphics, media & perf | Animation ticker suspension | Ticker pauses when window inactive | ❌ Missing | `AvaloniaTicker` never toggles `SystemEnabled`. |
| Graphics, media & perf | Perf instrumentation | Logging/metrics for render + layout | ❌ Missing | No instrumentation or guidance yet. |
| Tooling & QA | .NET/XAML Hot Reload | Works with Avalonia host, multiple windows | ❌ Missing | Not verified/documented; known gaps remain. |
| Tooling & QA | Avalonia DevTools toggle | Controlled via CLI/env var | ✅ Done | `MAUI_AVALONIA_DEVTOOLS` wiring exists. |
| Tooling & QA | Automated tests | Handler + integration tests in `tests/` | ⚠️ Partial | Added a `Microsoft.Maui.Avalonia.Tests` smoke suite that exercises handler registration and Map creation, but there are no UI automation or parity suites yet. |
| Tooling & QA | Sample coverage | Matrix of Shell, Essentials, media, navigation samples | ❌ Missing | Only the starter sample exists. |
| Tooling & QA | CI/nightly packages | Cross-platform CI runs, nightly NuGets | ❌ Missing | No pipelines or packages. |
| Tooling & QA | `dotnet new maui-avalonia` template | Template plus docs for onboarding | ❌ Missing | Not started. |
