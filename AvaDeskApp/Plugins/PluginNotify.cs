using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Positec;
using Plugin;

public class PluginNotify : IPlugin {
  const bool verbose = true;

  static async Task Notify(string text) {
    ProcessStartInfo? pi = null;

    if( verbose ) DeskApp.Trace($"PluginToast Notify Init => {text}");
    if( OperatingSystem.IsWindows() ) {
      //pi = new("powershell.exe", $"-File \"{AppDomain.CurrentDomain.BaseDirectory}Plugins\\Toast.ps1\" \"{text}\"");
      string cmd = string.Empty;

      cmd += "[Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime];";
      cmd += "$TT=[Windows.UI.Notifications.ToastTemplateType]::ToastText01;";
      cmd += "$TC=[Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($TT);";
      cmd += "$TC.SelectSingleNode('//text[@id=\"1\"]').InnerText='" + text + "';";
      cmd += "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('AvaDeskApp').Show($TC)";
      pi = new("powershell.exe", $"-Command \"{cmd}\"");
      pi.CreateNoWindow = true;
      pi.UseShellExecute = false;
    }
    if( OperatingSystem.IsLinux() ) {
      pi = new("notify-send", $"-a AvaDeskApp \"{text}\"");
      pi.CreateNoWindow = true;
    }

    if( pi != null ) {
      Process? p = Process.Start(pi);

      if( verbose ) DeskApp.Trace($"PluginToast Notify Start => {pi.FileName} {pi.Arguments}");
      if( p != null ) {
        await p.WaitForExitAsync();
        if( verbose ) DeskApp.Trace($"PluginToast Powershell Exit => {p.ExitCode}");
        p.Close();
      }
    }
  }

  string IPlugin.Desc {
    get { return "Plugin to send a message via powershell / notify-send"; }
  }
  List<PluginParaBase> IPlugin.Paras => [];
  async void IPlugin.Doit(PluginData pd) {
    if( verbose ) DeskApp.Trace("PluginToast DoIt");

    await Notify($"{pd.Name} - Only DoIt ;-)");
  }

  async void IPlugin.Todo(PluginData pd) {
    if( verbose ) DeskApp.Trace($"PluginToast ToDo with Mower => {pd.Index}: {pd.Name} {pd.Config.Time} {pd.Data.LastState}");
    if( pd.Data.LastState == StatusCode.IDLE ) {
      await Notify($"Mower {pd.Name} is in IDLE state at {pd.Config.Date} {pd.Config.Time}");
    } else if( verbose ) {
      await Notify($"Mower {pd.Name} send state {pd.Data.LastState} at {pd.Config.Date} {pd.Config.Time}");
    }
  }

  void IDisposable.Dispose() {
    DeskApp.Trace($"PluginToast Dispose");
  }

  public PluginNotify() {
    DeskApp.Trace($"PluginToast Constructor");
  }
}
