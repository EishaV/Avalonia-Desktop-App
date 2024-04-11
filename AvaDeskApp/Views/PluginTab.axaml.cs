using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Plugin;

namespace AvaApp.Views;

public partial class PluginTab : UserControl
{
    public PluginTab() {
        InitializeComponent();
    }

  private void Para_OnGotFocus(object? sender, GotFocusEventArgs e) {
    if( sender is TextBox tb ) {
      if( tb.Parent is DataGridCell dgc ) {
        if( dgc.DataContext is PluginParaText ppt ) {
          DataGridPara.SelectedItem = ppt;
        }
      }
    }
    if( sender is NumericUpDown nud ) {
      if( nud.Parent is DataGridCell dgc ) {
        if( dgc.DataContext is PluginParaReal ppr ) {
          DataGridPara.SelectedItem = ppr;
        }
      }
    }
  }
}