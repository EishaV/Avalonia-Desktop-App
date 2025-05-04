using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Positec;
using Plugin;
using System.IO;

public class PluginPlaySound : IPlugin {
  const bool verbose = true;

  static async Task Play() {
    ProcessStartInfo? pi = null;

    if( OperatingSystem.IsWindows() ) {
      string cmd = string.Empty;

      cmd += "$player = New-Object System.Media.SoundPlayer;";
      cmd += "$player.SoundLocation = '" + Path.Combine(DeskApp.DirPlugin, "Ding-Dong.wav") + "';";
      cmd += "$player.PlaySync()";
      pi = new("powershell.exe", $"-Command \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false };
    }
    if( pi != null ) {
      Process? p = Process.Start(pi);

      if( verbose ) DeskApp.Trace($"PluginPlaySound Start => {pi.FileName} {pi.Arguments}");
      if( p != null ) {
        await p.WaitForExitAsync();
        if( verbose ) DeskApp.Trace($"PluginPlaySound Powershell Exit => {p.ExitCode}");
        p.Close();
      }
    }
  }

  string IPlugin.Desc {
    get { return "Plugin to play sound when error or idle"; }
  }
  List<PluginParaBase> IPlugin.Paras => [];
  async void IPlugin.Doit(PluginData pd) {
    if( verbose ) DeskApp.Trace("PluginPlaySound DoIt");

    await Play();
  }

  async void IPlugin.Todo(PluginData pd) {
    if( verbose ) DeskApp.Trace($"PluginPlaySound ToDo with Mower => {pd.Index}: {pd.Name} {pd.Config.Time} {pd.Data.LastState}");
    if( pd.Data.LastError != 0 || pd.Data.LastState == StatusCode.IDLE ) await Play();
  }

  void IDisposable.Dispose() {
    DeskApp.Trace($"PluginPlaySound Dispose");
  }

  public PluginPlaySound() {
    DeskApp.Trace($"PluginPlaySound Constructor");
  }
}
