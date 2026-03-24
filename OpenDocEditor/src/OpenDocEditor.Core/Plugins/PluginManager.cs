using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenDocEditor.Core.Plugins;

/// <summary>
/// Загружает и управляет плагинами из каталога plugins/.
/// Плагины — это .dll-файлы, реализующие IEditorPlugin.
/// </summary>
public sealed class PluginManager
{
    private readonly List<IEditorPlugin> _loaded = [];
    private readonly ILogger<PluginManager> _logger;

    public PluginManager(ILogger<PluginManager> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IEditorPlugin> Plugins => _loaded;

    /// <summary>
    /// Обнаруживает и загружает все плагины из каталога.
    /// Вызывать ДО построения DI-контейнера для регистрации зависимостей.
    /// </summary>
    public void DiscoverPlugins(string pluginsDirectory, IServiceCollection services)
    {
        if (!Directory.Exists(pluginsDirectory)) return;

        foreach (var dll in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IEditorPlugin).IsAssignableFrom(t));

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IEditorPlugin plugin)
                    {
                        _loaded.Add(plugin);
                        plugin.ConfigureServices(services);
                        _logger.LogInformation("Plugin loaded: {Name} v{Version}", plugin.Name, plugin.Version);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from: {Dll}", dll);
            }
        }
    }

    /// <summary>Инициализирует все загруженные плагины после построения контейнера.</summary>
    public void InitializeAll(IPluginContext context)
    {
        foreach (var plugin in _loaded)
        {
            try { plugin.Initialize(context); }
            catch (Exception ex) { _logger.LogError(ex, "Plugin init failed: {Name}", plugin.Name); }
        }
    }

    public void ShutdownAll()
    {
        foreach (var plugin in _loaded)
        {
            try { plugin.Shutdown(); }
            catch (Exception ex) { _logger.LogError(ex, "Plugin shutdown failed: {Name}", plugin.Name); }
        }
        _loaded.Clear();
    }

    public IEnumerable<T> GetPluginsOf<T>() where T : IEditorPlugin =>
        _loaded.OfType<T>();
}
