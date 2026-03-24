using Microsoft.Extensions.DependencyInjection;
using OpenDocEditor.Core.Models.Document;

namespace OpenDocEditor.Core.Plugins;

/// <summary>
/// Базовый интерфейс плагина редактора.
/// Плагины загружаются из каталога plugins/ рефлексией при запуске.
/// </summary>
public interface IEditorPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }
    string Author { get; }

    /// <summary>Регистрация зависимостей плагина в DI-контейнере.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Инициализация после построения контейнера.</summary>
    void Initialize(IPluginContext context);

    /// <summary>Очистка ресурсов при выгрузке плагина.</summary>
    void Shutdown();
}

/// <summary>
/// Контекст, предоставляемый плагину при инициализации.
/// </summary>
public interface IPluginContext
{
    IServiceProvider Services { get; }
    string PluginsDirectory { get; }
    string DataDirectory { get; }

    /// <summary>Подписка на события жизненного цикла документа.</summary>
    void OnDocumentOpened(Action<DocModel> handler);
    void OnDocumentSaved(Action<DocModel> handler);
    void OnDocumentClosed(Action<DocModel> handler);
}

/// <summary>
/// Плагин, добавляющий кнопки в toolbar.
/// </summary>
public interface IToolbarPlugin : IEditorPlugin
{
    IReadOnlyList<ToolbarItem> GetToolbarItems();
}

/// <summary>
/// Плагин, добавляющий пункты меню.
/// </summary>
public interface IMenuPlugin : IEditorPlugin
{
    IReadOnlyList<MenuItem> GetMenuItems();
}

/// <summary>
/// Плагин-обработчик документа (pre/post-processing при открытии/сохранении).
/// </summary>
public interface IDocumentProcessorPlugin : IEditorPlugin
{
    Task OnBeforeOpenAsync(string filePath, CancellationToken ct = default);
    Task OnAfterOpenAsync(DocModel doc, CancellationToken ct = default);
    Task OnBeforeSaveAsync(DocModel doc, CancellationToken ct = default);
    Task OnAfterSaveAsync(DocModel doc, string savedPath, CancellationToken ct = default);
}

/// <summary>
/// Плагин экспорта в новый формат.
/// </summary>
public interface IExportPlugin : IEditorPlugin
{
    string FormatName { get; }
    string FileExtension { get; }
    string FileFilter { get; }
    Task ExportAsync(DocModel doc, string filePath, CancellationToken ct = default);
}

public sealed class ToolbarItem
{
    public string Id { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public Func<Task>? Command { get; set; }
    public string? Group { get; set; }
}

public sealed class MenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? ParentMenu { get; set; }
    public Func<Task>? Command { get; set; }
    public string? InputGestureText { get; set; }
}
