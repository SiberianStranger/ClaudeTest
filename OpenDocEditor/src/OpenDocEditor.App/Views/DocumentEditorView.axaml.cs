using Avalonia.Controls;
using OpenDocEditor.App.Controls;
using OpenDocEditor.App.ViewModels;

namespace OpenDocEditor.App.Views;

public partial class DocumentEditorView : UserControl
{
    public DocumentEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (this.FindControl<DocumentCanvas>("DocCanvas") is { } canvas)
            canvas.DocumentModified += (_, _) =>
                (DataContext as DocumentEditorViewModel)?.MarkModified();
    }
}
