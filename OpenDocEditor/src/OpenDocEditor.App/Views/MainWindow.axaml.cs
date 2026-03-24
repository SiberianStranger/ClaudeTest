using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OpenDocEditor.App.ViewModels;

namespace OpenDocEditor.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as MainWindowViewModel;
        WireUpMenuHandlers();
    }

    private void WireUpMenuHandlers()
    {
        if (this.FindControl<MenuItem>("OpenMenuItem") is { } openItem)
            openItem.Click += async (_, _) => await OpenFileDialog();

        if (this.FindControl<Button>("OpenBtn") is { } openBtn)
            openBtn.Click += async (_, _) => await OpenFileDialog();

        if (this.FindControl<MenuItem>("SaveAsMenuItem") is { } saveAsItem)
            saveAsItem.Click += async (_, _) => await SaveAsDialog();

        if (this.FindControl<MenuItem>("ExportPdfMenuItem") is { } pdfItem)
            pdfItem.Click += async (_, _) => await ExportPdfDialog();

        if (this.FindControl<MenuItem>("ExitMenuItem") is { } exitItem)
            exitItem.Click += (_, _) => Close();

        if (this.FindControl<MenuItem>("AboutMenuItem") is { } aboutItem)
            aboutItem.Click += ShowAbout;

        if (this.FindControl<MenuItem>("FindReplaceMenuItem") is { } frItem)
            frItem.Click += ShowFindReplace;
    }

    private async Task OpenFileDialog()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть документ",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Word Documents") { Patterns = ["*.docx"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            }
        });

        if (files.Count > 0 && _vm != null)
            await _vm.OpenDocumentFromPath(files[0].Path.LocalPath);
    }

    private async Task SaveAsDialog()
    {
        if (_vm?.ActiveDocument == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить как",
            DefaultExtension = "docx",
            SuggestedFileName = _vm.ActiveDocument.Document.DisplayName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Word Document") { Patterns = ["*.docx"] },
            }
        });

        if (file != null)
            await _vm.SaveDocumentAsCommand.ExecuteAsync(file.Path.LocalPath);
    }

    private async Task ExportPdfDialog()
    {
        if (_vm?.ActiveDocument == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт в PDF",
            DefaultExtension = "pdf",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_vm.ActiveDocument.Document.DisplayName) + ".pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF Document") { Patterns = ["*.pdf"] },
            }
        });

        if (file != null)
            await _vm.ExportPdfCommand.ExecuteAsync(file.Path.LocalPath);
    }

    private void ShowAbout(object? sender, RoutedEventArgs e)
    {
        var dlg = new Window
        {
            Title = "О программе",
            Width = 400, Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "OpenDocEditor", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "Версия 1.0.0", FontSize = 14 },
                    new TextBlock { Text = "Лёгкий коммерческий редактор DOCX-документов", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new TextBlock { Text = "" },
                    new TextBlock { Text = "Стек: .NET 8 + Avalonia 11 + OpenXML SDK", FontSize = 11, Foreground = Avalonia.Media.Brushes.Gray },
                    new TextBlock { Text = "Лицензии: MIT / Apache 2.0 (коммерческое использование допустимо)", FontSize = 11, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.Gray },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                 Width = 80, Margin = new Thickness(0,8,0,0) },
                }
            }
        };

        if (dlg.Content is StackPanel sp)
            (sp.Children[^1] as Button)!.Click += (_, _) => dlg.Close();

        dlg.ShowDialog(this);
    }

    private void ShowFindReplace(object? sender, RoutedEventArgs e)
    {
        // TODO: открыть диалог поиска/замены
    }

    // Keyboard shortcuts
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm == null) return;

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N: _vm.NewDocumentCommand.Execute(null); e.Handled = true; break;
                case Key.O: _ = OpenFileDialog(); e.Handled = true; break;
                case Key.S when e.KeyModifiers == KeyModifiers.Control:
                    _vm.SaveDocumentCommand.Execute(null); e.Handled = true; break;
            }
        }
    }
}
