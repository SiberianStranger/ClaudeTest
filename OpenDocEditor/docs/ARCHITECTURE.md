# OpenDocEditor — Архитектурный документ

## Цель проекта

Коммерчески применимый редактор DOCX-документов для Windows:
- Легче и быстрее МойОфис / Р7-Офис за счёт нативного .NET/Avalonia стека
- Полноценное открытие, редактирование и сохранение DOCX
- Экспорт в PDF
- Открытая архитектура для интеграций с госсистемами (СМЭВ, 1С, ЕСИА), ЭЦП (КриптоПро, ViPNet), СЭД (Directum, 1С-ДО)
- Коммерческая лицензия: все зависимости MIT / LGPL (допускают коммерческое использование)

---

## Технологический стек

| Слой | Технология | Лицензия | Обоснование |
|------|-----------|----------|-------------|
| Runtime | .NET 8 | MIT | Бесплатный, self-contained сборки |
| UI Framework | Avalonia 11 | MIT | Кроссплатформенный, GPU-ускоренный, легковесный |
| DOCX I/O | DocumentFormat.OpenXml 3.x | MIT | Официальная библиотека MS OpenXML |
| PDF экспорт | PdfSharpCore | MIT | Чистый .NET PDF без нативных зависимостей |
| MVVM | CommunityToolkit.Mvvm | MIT | Source-generator MVVM, нет рефлексии |
| DI Container | Microsoft.Extensions.DI | MIT | Стандартный .NET DI |
| Логирование | Serilog | Apache 2.0 | Структурированные логи, sink-расширяемость |
| Тесты | xUnit | Apache 2.0 | Стандарт .NET тестирования |

---

## Структура решения

```
OpenDocEditor/
├── src/
│   ├── OpenDocEditor.Core/           # Бизнес-логика, чистая, без UI
│   │   ├── Models/
│   │   │   ├── Document/             # Доменная модель документа
│   │   │   │   ├── DocModel.cs       # Корневая модель
│   │   │   │   ├── DocSection.cs     # Секция (колонтитулы, поля)
│   │   │   │   ├── DocParagraph.cs   # Абзац
│   │   │   │   ├── DocRun.cs         # Текстовый фрагмент (runs)
│   │   │   │   ├── DocTable.cs       # Таблица
│   │   │   │   ├── DocImage.cs       # Встроенное изображение
│   │   │   │   ├── RunFormat.cs      # Форматирование символов
│   │   │   │   ├── ParaFormat.cs     # Форматирование абзаца
│   │   │   │   └── PageLayout.cs     # Параметры страницы
│   │   │   └── EDM/                  # Электронный документооборот
│   │   │       ├── EdmMetadata.cs    # Метаданные СЭД
│   │   │       ├── WorkflowState.cs  # Состояние маршрута согласования
│   │   │       ├── SignatureInfo.cs  # Информация об ЭЦП
│   │   │       └── AuditEntry.cs     # Запись аудита
│   │   ├── Services/
│   │   │   ├── Documents/
│   │   │   │   ├── IDocumentService.cs
│   │   │   │   ├── DocxReaderService.cs   # DOCX → DocModel
│   │   │   │   ├── DocxWriterService.cs   # DocModel → DOCX
│   │   │   │   └── PdfExportService.cs    # DocModel → PDF
│   │   │   ├── EDM/
│   │   │   │   ├── IEdmService.cs
│   │   │   │   └── EdmService.cs          # СЭД-операции (стаб + DI-точка)
│   │   │   └── Signature/
│   │   │       ├── ISignatureService.cs   # Интерфейс ЭЦП
│   │   │       └── StubSignatureService.cs # Заглушка → подключить КриптоПро
│   │   └── Plugins/
│   │       ├── IEditorPlugin.cs      # Базовый интерфейс плагина
│   │       ├── IToolbarPlugin.cs     # Плагин добавляет кнопки toolbar
│   │       ├── IMenuPlugin.cs        # Плагин добавляет пункты меню
│   │       └── PluginManager.cs      # Загрузка/управление плагинами
│   │
│   └── OpenDocEditor.App/            # Avalonia UI
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs
│       │   ├── MainWindowViewModel.cs     # Главное окно: меню, статус
│       │   ├── DocumentEditorViewModel.cs # Логика редактора
│       │   ├── ToolbarViewModel.cs        # Форматирование toolbar
│       │   └── EdmPanelViewModel.cs       # Панель СЭД
│       ├── Views/
│       │   ├── MainWindow.axaml           # Shell
│       │   ├── DocumentEditorView.axaml   # Область редактирования
│       │   ├── ToolbarView.axaml          # Панель инструментов
│       │   └── EdmPanelView.axaml         # Панель маршрута/ЭЦП
│       ├── Controls/
│       │   ├── DocumentCanvas.cs          # Кастомный canvas страниц
│       │   ├── PageControl.cs             # Одна страница
│       │   └── ParagraphControl.cs        # Абзац с форматированием
│       └── Converters/
│           ├── FontWeightConverter.cs
│           ├── FontStyleConverter.cs
│           └── AlignmentConverter.cs
├── tests/
│   └── OpenDocEditor.Core.Tests/
├── docs/
│   └── ARCHITECTURE.md
└── build/
    ├── build.ps1                     # Windows PowerShell build
    └── build.sh                      # Linux/CI build + cross-compile
```

---

## Архитектурные слои

### 1. Domain Layer (Core/Models)

**DocModel** — неизменяемое дерево документа, независимое от формата файла:

```
DocModel
  └── Sections[]
        ├── PageLayout (margins, size, orientation)
        ├── Header / Footer
        └── Blocks[] (IDocBlock)
              ├── DocParagraph
              │     ├── ParaFormat (align, indent, spacing, numbering)
              │     └── Runs[]
              │           ├── DocRun { Text, RunFormat }
              │           └── DocImage { Data, Width, Height }
              └── DocTable
                    └── Rows[] → Cells[] → Blocks[]
```

**RunFormat** содержит: FontName, FontSize, Bold, Italic, Underline, Strikethrough, Color, Highlight, VerticalAlign (super/subscript), Lang.

**ParaFormat** содержит: Alignment, IndentLeft, IndentRight, IndentFirstLine, SpaceBefore, SpaceAfter, LineSpacing, StyleId, NumberingId, NumberingLevel.

**EdmMetadata** содержит: DocumentId, RegistrationNumber, RegistrationDate, Author, Organization, SecurityLevel, WorkflowState, Signatures[], AuditLog[].

---

### 2. Service Layer

**IDocumentService** — фасад для работы с документом:
```csharp
Task<DocModel> OpenAsync(string path);
Task SaveAsync(DocModel doc, string path);
Task ExportPdfAsync(DocModel doc, string path);
```

**IDocxReaderService** — парсинг OpenXML → DocModel:
- Читает styles.xml → StyleRegistry
- Читает document.xml → дерево блоков
- Разрешает стилевое наследование
- Конвертирует таблицы, изображения, поля

**IDocxWriterService** — DocModel → OpenXML:
- Полная сериализация обратно в DOCX
- Сохранение неизменённых частей пакета (relationships, custom XML)

**IPdfExportService** — DocModel → PDF через PdfSharpCore:
- Постраничный рендеринг через измеряемый layout engine
- Встраивание шрифтов (TrueType subsetting)

**ISignatureService** — абстракция ЭЦП:
```csharp
Task<SignatureInfo> SignDocumentAsync(DocModel doc, SigningOptions options);
Task<VerificationResult> VerifySignatureAsync(DocModel doc);
```
Реализации: `StubSignatureService` (dev), `CryptoproSignatureService` (production).

**IEdmService** — абстракция СЭД:
```csharp
Task RegisterDocumentAsync(DocModel doc);
Task<WorkflowState> GetWorkflowStateAsync(string documentId);
Task SendForApprovalAsync(DocModel doc, string[] approvers);
```

---

### 3. Plugin Architecture

Плагины загружаются через DI + reflection из каталога `plugins/`:

```csharp
public interface IEditorPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    void Initialize(IServiceCollection services, IPluginContext context);
}
```

Типы плагинов:
- `IToolbarPlugin` — добавляет кнопки в toolbar
- `IMenuPlugin` — добавляет пункты меню
- `IDocumentProcessorPlugin` — обработка при открытии/сохранении
- `IExportPlugin` — новые форматы экспорта (ODF, HTML, RTF)
- `IIntegrationPlugin` — интеграции (1С, СМЭВ, Goskey)

---

### 4. UI Layer (Avalonia MVVM)

**MainWindowViewModel** управляет:
- Открытые документы (TabControl)
- Статус-бар (режим, позиция курсора, язык)
- Последние файлы (Recent Files)
- Команды: New, Open, Save, SaveAs, Print, Export, Settings

**DocumentEditorViewModel** управляет:
- DocModel (текущий документ)
- Состояние выделения (SelectionState)
- Текущий RunFormat (для toolbar)
- Команды форматирования (Bold, Italic, FontSize, Color, ...)
- Undo/Redo через Command pattern

**ToolbarViewModel** отображает:
- Шрифт, размер, стиль
- Выравнивание
- Списки (маркированный/нумерованный)
- Отступы
- Цвет текста/фона

---

### 5. Интеграционные точки (для расширения)

| Направление | Интерфейс | Статус |
|-------------|-----------|--------|
| КриптоПро / ViPNet ЭЦП | `ISignatureService` | Стаб |
| СМЭВ (межведомственный обмен) | `ISmevClient` | Placeholder |
| Directum RX / 1С-ДО | `IEdmService` | Стаб |
| ЕСИА (Госуслуги авторизация) | `IAuthProvider` | Placeholder |
| Госключ (мобильная ЭЦП) | `IGoskeyService` | Placeholder |
| 1С:Предприятие | `IOneCConnector` | Placeholder |
| WebDAV / SharePoint | `ICloudStorageService` | Placeholder |

---

## Принципы качества

- **SOLID**: каждый класс — одна ответственность, все зависимости через интерфейсы
- **Clean Architecture**: Core не зависит от UI или конкретных форматов
- **Тестируемость**: все сервисы мокируемы, логика в ViewModel покрыта тестами
- **Расширяемость**: новый формат / интеграция = новая реализация интерфейса + регистрация в DI
- **Производительность**: lazy loading изображений, виртуализация страниц, async I/O

---

## Лицензионная чистота

Все компоненты допускают коммерческое использование:
- `DocumentFormat.OpenXml` — MIT
- `Avalonia` — MIT
- `PdfSharpCore` — MIT
- `CommunityToolkit.Mvvm` — MIT
- `Microsoft.Extensions.*` — MIT
- `Serilog` — Apache 2.0

Никаких GPL/AGPL зависимостей. Приложение может распространяться как closed-source коммерческий продукт.
