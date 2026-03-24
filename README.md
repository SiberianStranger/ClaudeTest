# OpenDocEditor

**Редактор DOCX-документов для Windows** — лёгкая коммерческая альтернатива МойОфис и Р7-Офис, построенная на .NET 8 + Avalonia.

---

## Скачать

### Готовый исполняемый файл (Windows x64)

> **Установка не нужна.** Один файл, запускается сразу.

| Файл | Версия | Платформа | Размер |
|------|--------|-----------|--------|
| [**OpenDocEditor.exe**](https://github.com/siberianstranger/claudetest/raw/main/OpenDocEditor/release/OpenDocEditor.exe) | 1.2.0 | Windows 10/11 x64 | ~47 МБ |

**Как скачать:**
1. Нажмите на ссылку выше — откроется прямая загрузка
2. Или перейдите в папку [`OpenDocEditor/release/`](https://github.com/siberianstranger/claudetest/tree/main/OpenDocEditor/release) → нажмите на `OpenDocEditor.exe` → кнопка **Download raw file** (иконка загрузки справа)

> **Примечание:** файл крупный (~47 МБ), т.к. включает .NET runtime — никаких дополнительных установок не требуется.

---

## Возможности

- **Открытие и редактирование** `.docx` файлов (Microsoft Word совместимый формат)
- **Экспорт в PDF** с встраиванием шрифтов
- **Форматирование текста:** шрифт, размер, жирный, курсив, подчёркивание, цвет, выделение
- **Форматирование абзацев:** выравнивание, отступы, межстрочный интервал, списки
- **Таблицы:** создание, редактирование, форматирование ячеек
- **Колонтитулы** и параметры страницы
- **Undo/Redo** (история изменений)
- **Плагинная архитектура** для расширений
- **Интеграционные заглушки** для КриптоПро, СМЭВ, СЭД (Directum, 1С-ДО)

---

## Системные требования

| Компонент | Требование |
|-----------|------------|
| ОС | Windows 10 (1903+) или Windows 11, x64 |
| .NET Runtime | **Не требуется** — включён в exe |
| RAM | 200 МБ+ |
| Место на диске | 100 МБ+ |
| Видеокарта | Любая с поддержкой DirectX 11 / OpenGL 2.0 |

---

## Технологический стек

| Слой | Технология | Лицензия |
|------|-----------|----------|
| Runtime | .NET 8 | MIT |
| UI Framework | Avalonia 11 | MIT |
| DOCX I/O | DocumentFormat.OpenXml 3.x | MIT |
| PDF экспорт | PdfSharpCore | MIT |
| MVVM | CommunityToolkit.Mvvm | MIT |
| DI | Microsoft.Extensions.DI | MIT |
| Логирование | Serilog | Apache 2.0 |

Все зависимости **MIT / Apache 2.0** — допускают коммерческое использование без ограничений. Нет GPL/AGPL.

---

## Структура репозитория

```
OpenDocEditor/
├── release/
│   └── OpenDocEditor.exe          ← скомпилированный exe для Windows
├── docs/
│   └── ARCHITECTURE.md            ← подробная архитектура проекта
├── build/
│   ├── build.ps1                  ← сборка на Windows (PowerShell)
│   └── build.sh                   ← сборка на Linux / CI
└── src/
    ├── OpenDocEditor.Core/        ← бизнес-логика (без UI)
    └── OpenDocEditor.App/         ← Avalonia UI приложение
```

---

## Архитектура

Подробное описание — в [`OpenDocEditor/docs/ARCHITECTURE.md`](OpenDocEditor/docs/ARCHITECTURE.md).

Краткая схема слоёв:

```
┌─────────────────────────────────────────┐
│           OpenDocEditor.App             │
│   Avalonia UI · MVVM · Views/Controls   │
└────────────────┬────────────────────────┘
                 │ зависит от
┌────────────────▼────────────────────────┐
│           OpenDocEditor.Core            │
│  Domain Models · Services · Plugins     │
│                                         │
│  DocModel ──► DocxReader/Writer         │
│             ──► PdfExport               │
│             ──► ISignatureService       │
│             ──► IEdmService             │
└─────────────────────────────────────────┘
```

### Интеграционные точки

| Система | Интерфейс | Статус |
|---------|-----------|--------|
| КриптоПро / ViPNet ЭЦП | `ISignatureService` | Заглушка |
| СМЭВ | `ISmevClient` | Placeholder |
| Directum RX / 1С-ДО | `IEdmService` | Заглушка |
| ЕСИА (Госуслуги) | `IAuthProvider` | Placeholder |
| Госключ | `IGoskeyService` | Placeholder |
| 1С:Предприятие | `IOneCConnector` | Placeholder |
| WebDAV / SharePoint | `ICloudStorageService` | Placeholder |

---

## Сборка из исходников

### Windows (PowerShell)

```powershell
# Требуется .NET 8 SDK: https://dotnet.microsoft.com/download
git clone https://github.com/siberianstranger/claudetest.git
cd claudetest/OpenDocEditor
.\build\build.ps1
# Результат: release\OpenDocEditor.exe
```

### Linux / macOS (для кросс-компиляции под Windows)

```bash
git clone https://github.com/siberianstranger/claudetest.git
cd claudetest/OpenDocEditor
bash build/build.sh
# Результат: release/OpenDocEditor.exe
```

### Ручная сборка

```bash
dotnet publish src/OpenDocEditor.App \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o release
```

---

## Changelog

### v1.2.0 (2026-03-24)
**Визуальная доработка и стабилизация редактирования**

- **UI**: современная тема — кнопки с закруглёнными углами, hover-эффекты, более светлый фон документа
- **Toolbar**: синхронизирован список размеров шрифта (8–72 pt) с выбором из ComboBox
- **Пробелы**: исправлен рендеринг — используется *sentinel trick* (`Width("text i") − Width("i")`) для корректного измерения advance width при trailing space в Avalonia
- **Каретка**: реализовано click-to-position — клик мышью позиционирует каретку в точное место абзаца через бинарный поиск по символьным смещениям
- **Производительность**: набор текста больше не вызывает полную перестройку страниц; `FastRefresh` (только `InvalidateVisual`) используется для вставки/удаления символов, `FullRebuild` только при изменении структуры абзацев
- **Окно**: добавлен `ExtendClientAreaToDecorationsHint="False"` для гарантированного отображения нативного заголовка Windows (кнопки свернуть / развернуть / закрыть)

### v1.1.0 (2026-03-24)
**Редактирование текста и базовые исправления**

- Реализовано редактирование документа: вставка символов (`OnTextInput`), удаление (`Backspace`/`Delete`), разрыв абзаца (`Enter`), навигация стрелками, `Home`/`End`
- Мигающая каретка (530 мс) с отрисовкой в `PageControl.RenderParagraph`; `LineSeg` хранит `CharStart` для точного X-позиционирования
- `SystemDecorations="Full"` и `CanResize="True"` добавлены в `MainWindow.axaml`
- Событие `DocumentModified` из `DocumentCanvas` подключено к `ViewModel.MarkModified()`
- Попытка fix пробелов (trailing-space токены) — заменена более надёжным sentinel методом в v1.2.0

### v1.0.0 (2026-03-24)
**Первый релиз**

- Чтение и сохранение `.docx` (OpenXML SDK)
- Постраничный рендеринг документа (кастомный layout engine)
- Экспорт в PDF (PdfSharpCore)
- Панель СЭД / метаданные ЭДО
- Интеграционные заглушки: КриптоПро, СМЭВ, Directum, ЕСИА, Госключ, 1С, WebDAV
- Плагинная архитектура

---

## Лицензия

Исходный код распространяется на условиях коммерческой лицензии. Все сторонние зависимости имеют лицензии MIT или Apache 2.0, допускающие коммерческое использование.
