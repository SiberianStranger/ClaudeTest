using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDocEditor.Core.Services.Documents;
using OpenDocEditor.Core.Services.EDM;
using OpenDocEditor.Core.Services.Signature;
using System.Collections.ObjectModel;

namespace OpenDocEditor.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;
    private readonly IEdmService _edmService;
    private readonly ISignatureService _signatureService;

    public ObservableCollection<DocumentEditorViewModel> OpenDocuments { get; } = [];

    [ObservableProperty] private DocumentEditorViewModel? _activeDocument;
    [ObservableProperty] private string _statusText = "Готов";
    [ObservableProperty] private bool _isEdmPanelVisible;
    [ObservableProperty] private string _appTitle = "OpenDocEditor";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";

    // Список недавних файлов
    public ObservableCollection<RecentFile> RecentFiles { get; } = [];

    // Доступные шрифты (для Toolbar)
    public IReadOnlyList<string> AvailableFonts { get; } = GetSystemFonts();

    // Размеры шрифта
    public IReadOnlyList<int> FontSizes { get; } = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 36, 48, 72];

    public MainWindowViewModel(
        IDocumentService documentService,
        IEdmService edmService,
        ISignatureService signatureService)
    {
        _documentService = documentService;
        _edmService = edmService;
        _signatureService = signatureService;
    }

    // ── Файловые операции ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewDocument()
    {
        var doc = await _documentService.CreateNewAsync();
        var vm = new DocumentEditorViewModel(doc, _documentService);
        OpenDocuments.Add(vm);
        ActiveDocument = vm;
        UpdateTitle();
    }

    [RelayCommand]
    private async Task OpenDocument(IStorageFile? file = null)
    {
        string? path;
        if (file != null)
        {
            path = file.Path.LocalPath;
        }
        else
        {
            // Диалог открывается через View — здесь получаем путь через параметр
            return;
        }

        await OpenDocumentFromPath(path);
    }

    public async Task OpenDocumentFromPath(string path)
    {
        try
        {
            SetBusy("Открытие документа…");
            var doc = await _documentService.OpenAsync(path);
            var vm = new DocumentEditorViewModel(doc, _documentService);
            OpenDocuments.Add(vm);
            ActiveDocument = vm;
            AddRecentFile(path);
            UpdateTitle();
            StatusText = $"Открыт: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка открытия: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private async Task SaveDocument()
    {
        if (ActiveDocument == null) return;
        if (ActiveDocument.Document.FilePath == null)
        {
            await SaveDocumentAs();
            return;
        }

        try
        {
            SetBusy("Сохранение…");
            await _documentService.SaveAsync(ActiveDocument.Document);
            ActiveDocument.MarkSaved();
            StatusText = "Сохранено";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private async Task SaveDocumentAs(string? path = null)
    {
        if (ActiveDocument == null || path == null) return;

        try
        {
            SetBusy("Сохранение…");
            await _documentService.SaveAsync(ActiveDocument.Document, path);
            ActiveDocument.MarkSaved();
            AddRecentFile(path);
            UpdateTitle();
            StatusText = $"Сохранено: {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private async Task ExportPdf(string? path = null)
    {
        if (ActiveDocument == null || path == null) return;

        try
        {
            SetBusy("Экспорт в PDF…");
            await _documentService.ExportPdfAsync(ActiveDocument.Document, path);
            StatusText = $"PDF сохранён: {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка экспорта: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private void CloseDocument(DocumentEditorViewModel? vm = null)
    {
        var target = vm ?? ActiveDocument;
        if (target == null) return;

        OpenDocuments.Remove(target);
        ActiveDocument = OpenDocuments.LastOrDefault();
        UpdateTitle();
    }

    // ── ЭЦП / СЭД ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SignDocument()
    {
        if (ActiveDocument == null) return;
        try
        {
            SetBusy("Подписание ЭЦП…");
            var certs = await _signatureService.GetAvailableCertificatesAsync();
            if (!certs.Any())
            {
                StatusText = "Нет доступных сертификатов ЭЦП";
                return;
            }
            var opts = new Core.Services.Signature.SigningOptions
            {
                CertificateThumbprint = certs[0].Thumbprint
            };
            await _signatureService.SignDocumentAsync(ActiveDocument.Document, opts);
            ActiveDocument.MarkModified();
            StatusText = $"Документ подписан: {certs[0].SubjectName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка подписания: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private async Task RegisterInEdm()
    {
        if (ActiveDocument == null) return;
        try
        {
            SetBusy("Регистрация в СЭД…");
            var result = await _edmService.RegisterDocumentAsync(ActiveDocument.Document);
            if (result.Success)
            {
                ActiveDocument.MarkModified();
                StatusText = $"Зарегистрирован: {result.RegistrationNumber}";
            }
            else
            {
                StatusText = $"Ошибка регистрации: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private void ToggleEdmPanel() => IsEdmPanelVisible = !IsEdmPanelVisible;

    // ── Helpers ───────────────────────────────────────────────────────────────

    partial void OnActiveDocumentChanged(DocumentEditorViewModel? value)
    {
        UpdateTitle();
        StatusText = value?.Document.DisplayName ?? "Готов";
    }

    private void UpdateTitle()
    {
        AppTitle = ActiveDocument != null
            ? $"{ActiveDocument.Title} — OpenDocEditor"
            : "OpenDocEditor";
    }

    private void AddRecentFile(string path)
    {
        var existing = RecentFiles.FirstOrDefault(r => r.Path == path);
        if (existing != null) RecentFiles.Remove(existing);
        RecentFiles.Insert(0, new RecentFile(path, DateTime.Now));
        while (RecentFiles.Count > 10) RecentFiles.RemoveAt(RecentFiles.Count - 1);
    }

    private void SetBusy(string text) { IsBusy = true; BusyText = text; }
    private void ClearBusy() { IsBusy = false; BusyText = ""; }

    private static IReadOnlyList<string> GetSystemFonts()
    {
        return ["Arial", "Times New Roman", "Calibri", "Verdana", "Tahoma",
                "Georgia", "Courier New", "Comic Sans MS", "Impact",
                "Arial Narrow", "Book Antiqua", "Century Gothic",
                "Garamond", "Palatino Linotype", "Trebuchet MS"];
    }
}

public sealed record RecentFile(string Path, DateTime LastOpened)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string DisplayText => $"{FileName}  ({LastOpened:dd.MM.yyyy HH:mm})";
}
