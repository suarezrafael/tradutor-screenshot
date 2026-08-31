# Screen Translator

Aplicativo desktop para Windows 10/11 (x64) que traduz textos de qualquer área da tela
selecionada com o mouse. O usuário recorta uma região (como na Ferramenta de Captura do
Windows), o app reconhece o texto com OCR, traduz e mostra a tradução **acima** do texto
original, preservando o original abaixo.

## Stack e decisões técnicas

| Camada | Tecnologia | Por quê |
|---|---|---|
| UI | WPF (.NET 8, `net8.0-windows`) | Framework Windows nativo, suporte a janelas transparentes/topmost necessárias para o overlay de seleção. |
| OCR | [Tesseract OCR](https://github.com/charlesw/tesseract) | Gratuito, roda 100% offline/local — nenhuma imagem é enviada para nuvem para reconhecimento de texto. |
| Tradução | [GTranslate](https://github.com/d4n3436/GTranslate) (`AggregateTranslator`) | Sem custo, sem API key. Tenta em sequência 5 motores gratuitos (Google Web, Google RPC, Microsoft/Bing, Yandex) — se um estiver com rate-limit, cai para o próximo automaticamente, em vez de falhar a captura inteira. Abordagem inspirada no projeto [OverTranslate](https://github.com/asd880921/OverTranslate), que resolve exatamente esse mesmo problema. A arquitetura isola isso atrás de `ITranslationService`, então trocar para Azure Translator, DeepL, OpenAI, etc. depois é uma mudança de uma única classe. |
| Captura de tela | GDI (`Graphics.CopyFromScreen`) via Win32/WinForms | Simples, funciona com múltiplos monitores e DPIs diferentes quando o app é DPI-aware (ver `app.manifest`, Per-Monitor V2). |
| DI / Hosting | `Microsoft.Extensions.Hosting` | Composição de serviços e ciclo de vida (singletons descartáveis como o engine do Tesseract). |

Essas escolhas foram confirmadas com o usuário no início do projeto (Tesseract para OCR local
e gratuito; endpoint gratuito do Google para tradução).

## Arquitetura

Solução dividida em 5 projetos (`ScreenTranslator.sln`):

```
src/ScreenTranslator.Domain          Modelos puros (OcrWord, OcrBlock, TranslationBlock,
                                      BoundingBox, Language, AppSettings, OperationResult<T>).
                                      Sem dependências de framework/Windows.

src/ScreenTranslator.Application     Casos de uso e lógica pura, testável sem Windows:
                                      - Abstractions/  (IScreenCaptureService, IOcrService,
                                        ILanguageDetectionService, ITranslationService,
                                        ITranslationCache, ITranslationOverlayService, ...)
                                      - PhraseGroupingService   (agrupa palavras OCR em frases)
                                      - OverlayLayoutCalculator (posiciona tradução acima do
                                        original, com fallback e anti-colisão)
                                      - MemoryTranslationCache  (cache com expiração)
                                      - CaptureTranslationOrchestrator (orquestra o pipeline)

src/ScreenTranslator.Infrastructure  Implementações concretas: Win32ScreenCaptureService
                                      (GDI multi-monitor), TesseractOcrService,
                                      HeuristicLanguageDetectionService,
                                      GoogleFreeTranslationService, JsonAppSettingsStore.

src/ScreenTranslator.App             WPF: ToolbarWindow, SelectionOverlayWindow, ResultWindow,
                                      SettingsWindow, TrayIconService, GlobalHotkeyManager,
                                      composição de DI em App.xaml.cs.

tests/ScreenTranslator.Tests         xUnit — cobre a camada Application (agrupamento,
                                      bounding box, posicionamento/colisão, cache, seleção de
                                      idioma, normalização de texto, orquestração e erros).
```

Fluxo: **seleção → captura → OCR → agrupamento em frases → detecção de idioma (se "auto") →
tradução (com cache) → cálculo de posição do overlay → exibição**.

### Multi-monitor / DPI

O app é marcado **Per-Monitor V2 DPI aware** (`src/ScreenTranslator.App/app.manifest`). A
janela de seleção (`SelectionOverlayWindow`) cobre toda a área virtual da tela (todos os
monitores) e mede o arrasto do mouse usando `System.Windows.Forms.Cursor.Position`, que
retorna pixels físicos reais para um processo DPI-aware — isso evita os problemas clássicos de
um único `Window` do WPF span-ando monitores com escalas de DPI diferentes (100%/125%/150%/200%).
O desenho do retângulo de seleção na tela é apenas visual; a captura final usa sempre as
coordenadas físicas.

## Como rodar

Pré-requisitos: **Windows 10/11 x64**, [.NET 8 SDK](https://dotnet.microsoft.com/download).

1. Baixe os dados de treinamento do Tesseract (inglês, espanhol, chinês simplificado —
   não ficam no git por serem binários de alguns MB):

   ```powershell
   pwsh scripts/download-tessdata.ps1
   ```

2. Rode o app:

   ```powershell
   dotnet run --project src/ScreenTranslator.App/ScreenTranslator.App.csproj
   ```

3. Use o botão **Capturar** na barra de ferramentas, o atalho **Ctrl+Shift+T**, ou o ícone
   na bandeja do Windows para iniciar uma captura. **ESC** cancela a seleção.

## Build e testes

```powershell
dotnet build                                                   # build da solução inteira
dotnet test tests/ScreenTranslator.Tests/ScreenTranslator.Tests.csproj   # testes automatizados
dotnet test --filter "FullyQualifiedName~PhraseGroupingServiceTests"    # um teste específico
```

## Configurações

Acessíveis pela janela **Configurações**: idioma de origem/destino padrão, atalhos de
teclado, iniciar com o Windows, manter na bandeja, copiar tradução automaticamente, tamanho
da fonte e transparência do overlay. Persistidas em
`%AppData%\ScreenTranslator\settings.json`. Logs estruturados em
`%AppData%\ScreenTranslator\logs\app-yyyyMMdd.log`.

## Privacidade

- Nenhuma captura de tela é salva automaticamente em disco (só se o usuário clicar em
  "Salvar imagem").
- OCR roda 100% localmente (Tesseract) — a imagem capturada nunca sai da máquina para esse
  passo.
- Apenas o **texto já reconhecido** (não a imagem) é enviado ao serviço de tradução.

## MVP: o que já está implementado

- [x] Captura de região com mouse (estilo Ferramenta de Captura), com ESC para cancelar.
- [x] Suporte a múltiplos monitores e DPIs diferentes.
- [x] OCR local (Tesseract) com agrupamento de palavras em frases.
- [x] Detecção automática de idioma (heurística) ou seleção manual (inglês, espanhol, chinês
      simplificado).
- [x] Tradução para português do Brasil (padrão) via serviço gratuito, com cache.
- [x] Tradução exibida acima do texto original, com fallback/anti-colisão de posição.
- [x] Janela de resultado com copiar texto, copiar imagem, salvar imagem e nova captura.
- [x] Barra de ferramentas, ícone na bandeja e atalhos globais configuráveis.
- [x] Tratamento de erros (sem texto encontrado, falha de OCR, falha de conexão, limite de
      API) com mensagens amigáveis.

## Limitações conhecidas / próximos passos

- Os motores de tradução gratuitos usados (via GTranslate) não são APIs oficiais/suportadas;
  mesmo com fallback entre 5 motores, é possível que todos falhem ao mesmo tempo. Para uso
  mais robusto, implemente `ITranslationService` com Azure Translator (tier gratuito de 2M
  caracteres/mês) ou DeepL.
- A detecção automática de idioma é heurística (stopwords + detecção de CJK), não um modelo
  de ML — suficiente para inglês/espanhol/chinês, mas não generaliza para outros idiomas sem
  ajuste.
- Testes de interface (seleção de tela, overlay, janelas) não têm cobertura automatizada —
  apenas a lógica pura da camada Application é testada. Validação de UI foi feita
  manualmente durante o desenvolvimento.
