using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

using AvaApp.ViewModels;
using AvaApp.Views;

namespace AvaApp {
  public partial class App : Application {
    public override void Initialize() {
      AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted() {
      if( Design.IsDesignMode ) return;

      string s, title;
      string dir = AppContext.BaseDirectory;
      Assembly assembly = Assembly.GetExecutingAssembly();

      s = Path.Combine(dir, "Trace");
      if( !Directory.Exists(s) ) Directory.CreateDirectory(s);
      s = Path.Combine(dir, "Plugins");
      if( !Directory.Exists(s) ) Directory.CreateDirectory(s);

      Console.WriteLine($"BaseDir {dir}");
      Console.WriteLine($"Assembly {assembly.Location} {assembly.FullName}");
      title = assembly.GetName().Name ?? "AvaDeskApp";

      int pid = Environment.ProcessId; Console.WriteLine($"ProcessId {pid}");
      Process[] ps = Process.GetProcesses(); Console.WriteLine($"Processes {ps.Length}");
      StringBuilder sb = new();

      sb.AppendLine($"{pid} - {assembly.FullName} - {dir}");
      foreach( Process process in ps ) {
        if( (process.ProcessName == "dotnet" || process.ProcessName == title) ) {
          sb.AppendLine($"{process.Id} - {process.ProcessName}"); Console.WriteLine($"Process {process.ProcessName}");
          foreach( ProcessModule pm in process.Modules ) {
            Console.WriteLine($"  Module {pm.FileName}");
            if( !string.IsNullOrEmpty(pm.FileName) && pm.FileName.StartsWith(dir, true, CultureInfo.InvariantCulture) ) {
              sb.AppendLine($"  {pm.FileName}");
              if( process.Id != pid ) {
                string msg = $"Only one instance from the folder{Environment.NewLine}  {dir}{Environment.NewLine}is allowed!";
                var msgbox = MessageBoxManager.GetMessageBoxStandard(title, msg, ButtonEnum.Ok, Icon.Error);

                await msgbox.ShowAsync();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "TwoInst.txt"), sb.ToString());
                return;
              }
            }
          }
        }
      }

      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
        MainWindowViewModel mwvm = MainWindowViewModel.Instance;
        CfgFrame fr = mwvm.Config.Frame;

        //? if( fr.X < 0 || fr.Y < 0 ) fr = new CfgFrame();
        desktop.MainWindow = new MainWindow {
          DataContext = mwvm, Title = $"{title} {MainWindowViewModel.Version}",
          WindowStartupLocation = WindowStartupLocation.Manual,
          Position = new PixelPoint(fr.X-10, fr.Y), Width = fr.W, Height = fr.H
        };
        Trace.TraceInformation($"{desktop.MainWindow.Title}");

        if( mwvm != null ) {
          desktop.MainWindow.Opened += mwvm.MainWindow_Opened;
          desktop.MainWindow.Activated += mwvm.MainWindow_Activated;
          desktop.MainWindow.Closed += mwvm.MainWindow_Closed;
          desktop.MainWindow.Closing += mwvm.MainWindow_Closing;
          //desktop.MainWindow.PositionChanged += mwvm.MainWindow_Moved;
        }
      }

      base.OnFrameworkInitializationCompleted();
    }
  }
}


