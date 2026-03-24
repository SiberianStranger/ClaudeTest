using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Services.Documents;
using System.Collections.ObjectModel;

namespace OpenDocEditor.App.ViewModels;

/// <summary>
/// ViewModel одного открытого документа — вкладка в редакторе.
/// </summary>
public sealed partial class DocumentEditorViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;

    [ObservableProperty] private DocModel _document;
    [ObservableProperty] private string _title = "Новый документ";
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private string _statusText = "";

    // Toolbar state — текущий формат в позиции курсора
    [ObservableProperty] private string _currentFontName = "Times New Roman";
    [ObservableProperty] private float _currentFontSize = 12f;
    [ObservableProperty] private bool _isBold;
    [ObservableProperty] private bool _isItalic;
    [ObservableProperty] private bool _isUnderline;
    [ObservableProperty] private bool _isStrikethrough;
    [ObservableProperty] private ParagraphAlignment _currentAlignment = ParagraphAlignment.Left;

    // Редактируемый контент как плоский список абзацев для binding
    public ObservableCollection<DocParagraph> Paragraphs { get; } = [];

    // Позиция курсора для статус-бара
    [ObservableProperty] private int _cursorLine = 1;
    [ObservableProperty] private int _cursorColumn = 1;
    [ObservableProperty] private int _wordCount;

    // Стек Undo/Redo
    private readonly Stack<DocSnapshot> _undoStack = new();
    private readonly Stack<DocSnapshot> _redoStack = new();

    public DocumentEditorViewModel(DocModel document, IDocumentService documentService)
    {
        _document = document;
        _documentService = documentService;
        RefreshParagraphs();
        UpdateTitle();
    }

    // ── Команды форматирования ───────────────────────────────────────────────

    [RelayCommand]
    private void ToggleBold()
    {
        IsBold = !IsBold;
        ApplyFormatToSelection(f => f.Bold = IsBold);
    }

    [RelayCommand]
    private void ToggleItalic()
    {
        IsItalic = !IsItalic;
        ApplyFormatToSelection(f => f.Italic = IsItalic);
    }

    [RelayCommand]
    private void ToggleUnderline()
    {
        IsUnderline = !IsUnderline;
        ApplyFormatToSelection(f => f.Underline = IsUnderline);
    }

    [RelayCommand]
    private void ToggleStrikethrough()
    {
        IsStrikethrough = !IsStrikethrough;
        ApplyFormatToSelection(f => f.Strikethrough = IsStrikethrough);
    }

    [RelayCommand]
    private void SetAlignment(ParagraphAlignment alignment)
    {
        CurrentAlignment = alignment;
        ApplyAlignmentToSelection(alignment);
        MarkModified();
    }

    [RelayCommand]
    private void SetFontSize(float size)
    {
        CurrentFontSize = size;
        ApplyFormatToSelection(f => f.FontSize = size);
    }

    [RelayCommand]
    private void SetFontName(string name)
    {
        CurrentFontName = name;
        ApplyFormatToSelection(f => f.FontName = name);
    }

    [RelayCommand]
    private void IncreaseFontSize() => SetFontSize(Math.Min(CurrentFontSize + 2, 96));

    [RelayCommand]
    private void DecreaseFontSize() => SetFontSize(Math.Max(CurrentFontSize - 2, 6));

    // ── Undo/Redo ────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!_undoStack.TryPop(out var snapshot)) return;
        _redoStack.Push(TakeSnapshot());
        RestoreSnapshot(snapshot);
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!_redoStack.TryPop(out var snapshot)) return;
        _undoStack.Push(TakeSnapshot());
        RestoreSnapshot(snapshot);
    }

    private bool CanUndo() => _undoStack.Count > 0;
    private bool CanRedo() => _redoStack.Count > 0;

    // ── Поиск и замена ───────────────────────────────────────────────────────

    [RelayCommand]
    private void FindReplace(FindReplaceParams p)
    {
        if (string.IsNullOrEmpty(p.Find)) return;
        PushUndoSnapshot();

        int count = 0;
        foreach (var para in _document.AllParagraphs)
        {
            foreach (var run in para.Inlines.OfType<DocRun>())
            {
                if (run.Text.Contains(p.Find, StringComparison.OrdinalIgnoreCase))
                {
                    run.Text = run.Text.Replace(p.Find, p.Replace ?? "", StringComparison.OrdinalIgnoreCase);
                    count++;
                }
            }
        }

        RefreshParagraphs();
        StatusText = $"Замен выполнено: {count}";
        if (count > 0) MarkModified();
    }

    // ── Вставка ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void InsertPageBreak()
    {
        PushUndoSnapshot();
        var para = new DocParagraph();
        para.Inlines.Add(new DocBreak { BreakType = BreakType.Page });
        _document.Blocks.Add(para);
        RefreshParagraphs();
        MarkModified();
    }

    // ── Статистика ───────────────────────────────────────────────────────────

    public void RecalculateStats()
    {
        WordCount = _document.AllParagraphs
            .Select(p => p.PlainText)
            .Sum(t => t.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length);
    }

    // ── Notification helpers ─────────────────────────────────────────────────

    public void MarkModified()
    {
        Document.IsModified = true;
        IsModified = true;
        UpdateTitle();
        RecalculateStats();
    }

    public void MarkSaved()
    {
        Document.IsModified = false;
        IsModified = false;
        UpdateTitle();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void RefreshParagraphs()
    {
        Paragraphs.Clear();
        foreach (var block in _document.Blocks.OfType<DocParagraph>())
            Paragraphs.Add(block);
    }

    private void UpdateTitle()
    {
        Title = Document.DisplayName + (IsModified ? " *" : "");
    }

    private void ApplyFormatToSelection(Action<RunFormat> apply)
    {
        // TODO: применять к выделенным runs; пока применяем к следующему вводу
        // через ToolbarViewModel.CurrentFormat
        MarkModified();
    }

    private void ApplyAlignmentToSelection(ParagraphAlignment alignment)
    {
        // TODO: применять к выделенным абзацам
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(TakeSnapshot());
        _redoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private DocSnapshot TakeSnapshot() => new(_document);

    private void RestoreSnapshot(DocSnapshot snapshot)
    {
        // Упрощённое восстановление — полная сериализация через OpenXML
        RefreshParagraphs();
        MarkModified();
    }

    /// <summary>Снимок состояния документа для Undo.</summary>
    private sealed record DocSnapshot(DocModel Document);
}

public sealed record FindReplaceParams(string Find, string? Replace, bool MatchCase = false, bool WholeWord = false);
