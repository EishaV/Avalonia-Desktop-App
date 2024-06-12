using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Plugin;

namespace AvaApp.Views;

public partial class PluginTab : UserControl {
  public PluginTab() {
    InitializeComponent();
  }

  private void Para_OnGotFocus(object? sender, GotFocusEventArgs e) {
    // passt so für TextBox und NumeriUpDown
    if( sender is StyledElement s && s.Parent is ContentControl c && c.DataContext is PluginParaBase p ) {
      ListPara.SelectedItem = p;
    }
  }
}