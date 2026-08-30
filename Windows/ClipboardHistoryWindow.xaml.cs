using System.Windows;
using System.Windows.Input;
using MyAiAssistant.Services;

namespace MyAiAssistant.Windows;

public partial class ClipboardHistoryWindow : Window
{
    private readonly ClipboardHistoryService _service;

    public ClipboardHistoryWindow(ClipboardHistoryService service)
    {
        InitializeComponent();
        _service = service;
        RefreshList();
    }

    private void RefreshList()
    {
        HistoryList.ItemsSource = _service.Entries.ToList();
    }

    private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is ClipboardEntry entry)
        {
            _service.CopyToClipboard(entry.Text);
            MessageBox.Show("已复制到剪贴板", "Fairy AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定清空剪贴板历史？", "Fairy AI",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _service.Clear();
            RefreshList();
        }
    }
}
