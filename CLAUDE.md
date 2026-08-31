# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Screen Translator: a Windows 10/11 (x64) WPF desktop app. The user drags a selection over any
part of the screen (like the Snipping Tool), the app OCRs the text in that region, translates
it, and overlays the translation directly above the original text in a result window.

## Commands

```powershell
# One-time setup: download Tesseract trained-data files (not committed to git — see .gitignore)
pwsh scripts/download-tessdata.ps1

dotnet build                                                             # build entire solution
dotnet run --project src/ScreenTranslator.App/ScreenTranslator.App.csproj  # run the app
dotnet test tests/ScreenTranslator.Tests/ScreenTranslator.Tests.csproj   # run all tests
dotnet test --filter "FullyQualifiedName~PhraseGroupingServiceTests"     # run a single test class
```

There is no separate lint step; treat compiler warnings as errors to fix (the solution builds
with 0 warnings today — keep it that way).

## Architecture

Five projects under `ScreenTranslator.sln`, in strict dependency order (each layer only
depends on the ones to its left):

`Domain` ← `Application` ← `Infrastructure` ← `App`, plus `Tests` referencing `Domain` + `Application`.

- **`src/ScreenTranslator.Domain`** (`net8.0`, no Windows dependency): plain records —
  `OcrWord`, `OcrBlock`, `TranslationBlock`, `BoundingBox`, `Language` (a data-driven registry,
  not an enum, so new languages don't require touching switch statements elsewhere),
  `AppSettings`, `OperationResult<T>` / `ScreenTranslatorErrorCode`.

- **`src/ScreenTranslator.Application`** (`net8.0`): the testable core, no Windows APIs.
  - `Abstractions/` — every external dependency the pipeline needs is an interface here
    (`IScreenCaptureService`, `IOcrService`, `ILanguageDetectionService`, `ITranslationService`,
    `ITranslationCache`, `ITranslationOverlayService`, `ITextMeasurer`, `IAppSettingsStore`).
    `ITranslationService` in particular is deliberately provider-agnostic — see Infrastructure.
  - `PhraseGroupingService` — groups raw OCR words into phrase-level `OcrBlock`s (by line, then
    by horizontal gap) so translation happens once per sentence, not once per word.
  - `OverlayLayoutCalculator` / `TranslationOverlayService` — pure positioning math:
    `TranslationY = OriginalY - TranslationHeight - Margin`, falling back below the original
    when there's no room above, then nudging labels apart so they never overlap.
  - `MemoryTranslationCache` — keyed by (source lang, target lang, text), with a recency TTL.
  - `CaptureTranslationOrchestrator` — the one place that knows the pipeline order: capture →
    OCR → group → detect language (if "auto") → translate (cached) → compute overlay layout.
    Maps failures to `ScreenTranslatorErrorCode` (no text found, OCR failed, connection failed,
    translation service unavailable, API limit reached, empty/cancelled capture).

- **`src/ScreenTranslator.Infrastructure`** (`net8.0-windows`): concrete implementations.
  - `Win32ScreenCaptureService` — GDI capture (`Graphics.CopyFromScreen`) across the whole
    virtual desktop, in physical pixels.
  - `TesseractOcrService` — local/offline OCR via the `Tesseract` NuGet package. Requires
    trained-data files under `src/ScreenTranslator.App/tessdata/` (see Commands above);
    "auto-detect" language is implemented by asking Tesseract to recognize all supported
    scripts at once (`eng+spa+chi_sim`) rather than guessing beforehand.
  - `HeuristicLanguageDetectionService` — CJK-range detection for Chinese, stopword counting
    for English vs. Spanish. Deliberately simple; swap for a real model later if needed.
  - `GTranslateService` — wraps the [GTranslate](https://github.com/d4n3436/GTranslate) library's
    `AggregateTranslator`, which tries 5 free, key-less engines in order (Google Web, Google
    RPC, Microsoft/Bing, Yandex) and falls back automatically when one fails or is
    rate-limited. A single free endpoint (the original approach) turned out to rate-limit
    quickly on shared/datacenter IPs; this fallback chain — the same fix the open-source
    OverTranslate project uses — is what actually makes "zero cost, zero signup" reliable.
    None of these are official/supported APIs. Because callers only see `ITranslationService`,
    swapping in Azure Translator/DeepL/OpenAI/a local model later is a one-class change.
  - `JsonAppSettingsStore` — persists `AppSettings` to `%AppData%\ScreenTranslator\settings.json`.

- **`src/ScreenTranslator.App`** (WPF, `net8.0-windows`, `x64`): composition root is
  `App.xaml.cs`, which builds a `Microsoft.Extensions.Hosting` `IHost` and wires every
  interface above to its Infrastructure implementation.
  - `ToolbarWindow` — the small always-on-top toolbar (capture button, language pickers, copy
    buttons, settings, close). Hides itself while a capture is in progress so it doesn't sit
    on top of the fullscreen selection overlay, and minimizes to tray instead of closing.
  - `SelectionOverlayWindow` — the fullscreen drag-to-select UI. **DPI note:** its HWND is
    positioned via a raw `SetWindowPos` P/Invoke call in physical pixels (see
    `Interop/NativeMethods.cs`), sidestepping WPF's per-monitor DPI virtualization of
    `Window.Left/Top/Width/Height`, which can't correctly size one window spanning monitors
    with different DPI scale factors. The actual drag measurement uses
    `System.Windows.Forms.Cursor.Position` (always physical pixels for a Per-Monitor-V2-aware
    process — see `app.manifest`); WPF's own DPI scale is only used to draw the visual
    selection rectangle, never for the final captured coordinates.
  - `ResultWindow` — shows the capture with translation labels overlaid; copy/save/recapture.
  - `SettingsWindow`, `TrayIconService` (WinForms `NotifyIcon` hosted inside the WPF dispatcher
    thread — this works without a separate message loop), `GlobalHotkeyManager` (Win32
    `RegisterHotKey` via a hidden `HwndSource` message-only window).
  - `CaptureFlowController` — the only class that wires trigger sources (toolbar button, tray
    menu, global hotkey) to `SelectionOverlayWindow` and `CaptureTranslationOrchestrator`, and
    tracks the last result for "Última captura" / Ctrl+Shift+L.
  - **Namespace gotcha, don't reintroduce it:** the App project's `RootNamespace` is
    `ScreenTranslator.Desktop`, not `ScreenTranslator`. If it's ever renamed back to bare
    `ScreenTranslator`, unqualified `Application` inside `App.xaml.cs` starts resolving to the
    sibling `ScreenTranslator.Application` project namespace instead of `System.Windows.Application`
    (C# searches enclosing-namespace members before usings), which breaks the WPF `App` class.
    Similarly, inside any `Window` subclass, bare `Language` resolves to the inherited
    `FrameworkElement.Language` (`XmlLanguage`) property, not `ScreenTranslator.Domain.Language`
    — those files alias it as `AppLanguage` instead of fighting the shadow.
  - **`UseWindowsForms` + `UseWPF` gotcha:** `UseWindowsForms=true` auto-adds `System.Drawing`
    and `System.Windows.Forms` as implicit global usings, which collide with WPF types of the
    same name (`Application`, `MessageBox`, `Clipboard`, `FontFamily`, `Color`, `Brushes`,
    `ColorConverter`, `FlowDirection`, `MouseEventArgs`, `KeyEventArgs`, ...). Both are removed
    from implicit usings in the `.csproj` (`<Using Remove="..." />`); files that need WinForms
    (`TrayIconService`, `Win32ScreenCaptureService`, `SelectionOverlayWindow`'s cursor reads)
    add the namespace explicitly or fully-qualify instead.

- **`tests/ScreenTranslator.Tests`** (xUnit, `net8.0`): covers `Domain` + `Application` only —
  phrase grouping, bounding-box math, overlay positioning/collision, cache behavior
  (including expiry), language selection, OCR text normalization, and the orchestrator's
  success/failure paths (via hand-written fakes in `Fakes/`, no mocking library). UI code in
  `App` has no automated tests; it was checked manually during development (see README's
  "Limitações conhecidas" section).

## Key design decisions worth knowing before changing things

- **Translation is provider-agnostic on purpose.** Don't hardcode Google-specific behavior
  outside `Infrastructure/Translation/`; new providers implement `ITranslationService`.
- **Phrases, not words, get translated** — `PhraseGroupingService` exists specifically to
  avoid one API call per word. Don't bypass it when adding new capture flows.
- **All translation-cache reads/writes are keyed by (source lang, target lang, text)** —
  see `MemoryTranslationCache.BuildKey`. Keep any new cache implementation consistent with this.
- **Never do DPI-sensitive math with WPF's `Window.Left/Top/Width/Height` for the actual
  captured region** — always compute from physical pixels (`Cursor.Position`, `SetWindowPos`,
  `SystemInformation.VirtualScreen`). WPF's DPI virtualization is only trustworthy for
  single-monitor, single-DPI visuals.
