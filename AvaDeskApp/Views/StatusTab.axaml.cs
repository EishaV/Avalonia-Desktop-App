using Avalonia.Controls;

namespace AvaApp.Views {
  public partial class StatusTab : UserControl {
    public StatusTab() {
      InitializeComponent();
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e) {
      base.OnSizeChanged(e);

      if( e.NewSize.Width < e.NewSize.Height ) {
        StatusLow[Grid.ColumnProperty] = 0;
        StatusLow[Grid.RowProperty] = 1;
      } else {
        StatusLow[Grid.ColumnProperty] = 1;
        StatusLow[Grid.RowProperty] = 0;
      }
    }
  }
}
