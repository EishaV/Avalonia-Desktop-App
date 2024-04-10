using System;
using System.Diagnostics;
using System.Reflection;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaApp.ViewModels;
using AvaApp.Views;
using MsBox.Avalonia;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using CSScriptLib;
using Plugin;
using MsBox.Avalonia.Enums;
using Avalonia.Controls;

namespace AvaApp {
  public partial class App : Application {
    public override void Initialize() {
      AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted() {
      if( Design.IsDesignMode ) return;

      string s, title = "PosiMowApp";
      string dir = AppContext.BaseDirectory;
      Assembly assembly = Assembly.GetExecutingAssembly();

      s = Path.Combine(dir, "Trace");
      if( !Directory.Exists(s) ) Directory.CreateDirectory(s);
      s = Path.Combine(dir, "Plugins");
      if( !Directory.Exists(s) ) Directory.CreateDirectory(s);

      Console.WriteLine($"BaseDir {dir}");
      Console.WriteLine($"Assembly {assembly.Location} {assembly.FullName}");

      int pid = Environment.ProcessId; Console.WriteLine($"ProcessId {pid}");
      Process[] ps = Process.GetProcesses(); Console.WriteLine($"Processes {ps.Length}");
      StringBuilder sb = new();

      sb.AppendLine($"{pid} - {assembly.FullName} - {dir}");
      foreach( Process process in ps ) {
        if( (process.ProcessName == "dotnet" || process.ProcessName == "AvaApp") ) {
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
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

        desktop.MainWindow = new MainWindow {
          DataContext = mwvm, Title = $"{title} {fvi.ProductVersion}",
          WindowStartupLocation = WindowStartupLocation.Manual,
          Position = new PixelPoint(fr.X-10, fr.Y), Width = fr.W, Height = fr.H
        };
        Trace.TraceInformation($"{desktop.MainWindow.Title}");

        if( mwvm != null ) {
          mwvm.Version = new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart);
          desktop.MainWindow.Opened += mwvm.MainWindow_Opened;
          desktop.MainWindow.Closed += mwvm.MainWindow_Closed;
          desktop.MainWindow.Closing += mwvm.MainWindow_Closing;
          //desktop.MainWindow.PositionChanged += mwvm.MainWindow_Moved;
        }
      }

      base.OnFrameworkInitializationCompleted();
    }
  }
}


