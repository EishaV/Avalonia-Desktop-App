using System.Collections.Generic;
using System.Timers;
using System;
using System.IO;
using System.Runtime.Serialization;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

using Positec;
using Plugin;

public class PluginTest : IPlugin {
  const string SetJson = "TestPluginSettings.json";
  const string DescTxt = "Ein Text welcher der Ausgabe voran gestellt wird.\r\nEine weitere Zeile\r\nnoch ne Zeile ...";
  const string DescOut = "Hier erfolgt bei eingeschaltetem Timer die Ausgabe des voran gstellten Textes und der aktuellen Zeit.";

  readonly List<string> ListCase = new() { "Timer", "MsgBox 1", "MsgBox 2" };

  [DataContract]
  public class TestSettings { // Options for Open and Close
    [DataMember] public string Text;
    [DataMember] public int Case;
    [DataMember] public double Real;
    [DataMember] public string Nono;

    public TestSettings() {
      Text = "Time: ";
      Case = 0;
      Real = 2000;
      Nono = "geht die UI nix an";
    }
  }

  private Timer _timer;
  private TestSettings _sets;

  public PluginParaBool ParaBool;
  public PluginParaText ParaText;
  public PluginParaCase ParaEnum;
  public PluginParaReal ParaReal;
  public PluginParaText ParaOut;

  private List<PluginParaBase> TestParas;
  string IPlugin.Desc {
    get { return "Test Plugin with a little bit longer description"; }
  }
  List<PluginParaBase> IPlugin.Paras => TestParas;

  async void IPlugin.Doit(PluginData pd) {
    DeskApp.Trace($"PluginTest DoIt with Mower {pd.Index}: {pd.Name} => Action {ParaEnum.Index}");

    if( ParaEnum.Index == 0 ) {
      PushNow();
      if( ParaBool.Check ) {
        _timer.Interval = ParaReal.Real * 1000;
        _timer.Start();
      } else {
        _timer.Stop();
      }
    } else if( ParaEnum.Index == 1 ) {
      var msgpar = new MessageBoxStandardParams {
        ButtonDefinitions = ButtonEnum.YesNo, ContentTitle = "TestPlugin", ContentMessage = "MsgBox for hsteinme", WindowStartupLocation = WindowStartupLocation.CenterOwner
      };
      var msgbox = MessageBoxManager.GetMessageBoxStandard(msgpar);
      if(Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
        ButtonResult br = await msgbox.ShowWindowDialogAsync(desktop.MainWindow);

        ParaOut.Text = $"MsgBox 1 => {br}";
      }
    } else if(ParaEnum.Index == 2) {
      var msgbox = MessageBoxManager.GetMessageBoxStandard("Test-Titel", "MsgBox für Helmut", ButtonEnum.OkCancel, Icon.Info, WindowStartupLocation.CenterOwner);

      ButtonResult br = await msgbox.ShowAsync();
      ParaOut.Text = $"MsgBox 2 => {br}";
    }
  }

  private void PushNow() {
    ParaOut.Text = $"{ParaText.Text} {DateTime.Now.ToString("HH:mm:ss")}";
    DeskApp.Trace($"PluginTest Set ParaOut.Text => {ParaOut.Text}");
  }
  private void _timer_Elapsed(object? sender, ElapsedEventArgs e) {
    PushNow();
  }

  void IPlugin.Todo(PluginData pd) {
    DeskApp.Trace($"PluginTest ToDo with Mower => {pd.Index}: {pd.Name} {pd.Config.Time}");
  }

  void IDisposable.Dispose() {
    _sets.Text = ParaText.Text;
    _sets.Case = ParaEnum.Index;
    _sets.Real = ParaReal.Real;
    DeskApp.PutJson(Path.Combine(DeskApp.DirData, SetJson), _sets);
    DeskApp.Trace($"PluginTest Dispose Settings => {_sets.Text} {_sets.Case} {_sets.Real}");
  }

  public PluginTest() {
    TestSettings? ts = DeskApp.GetJson<TestSettings>(Path.Combine(DeskApp.DirData, SetJson));

    DeskApp.Trace($"PluginTest Constructor Settings => {ts?.Text} {ts?.Case} {ts?.Real}");
    _sets ??= new TestSettings();
    ParaBool = new PluginParaBool("Test Parameter Bool", false, "Schaltet den Timer an/aus.");
    ParaText = new PluginParaText("Test Parameter Text", _sets.Text, DescTxt);
    ParaEnum = new PluginParaCase("Test Parameter Enum", _sets.Case, ListCase,  "Aktion bei DoIt");
    ParaReal = new PluginParaReal("Test Parameter Real", _sets.Real, "Timeout in Sekunden");
    ParaOut = new PluginParaText("Test Parameter ReadOnly", "Warten auf DoIt oder Timer", DescOut, true);
    TestParas = new List<PluginParaBase>() { ParaBool, ParaEnum, ParaText, ParaReal, ParaOut };

    _timer = new Timer();
    _timer.Elapsed += _timer_Elapsed;
  }
}