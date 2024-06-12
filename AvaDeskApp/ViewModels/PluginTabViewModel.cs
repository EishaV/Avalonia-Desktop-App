using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using Avalonia.Controls.Templates;
using Avalonia.Controls;
using Avalonia.Metadata;
using Avalonia.Threading;
using CSScriptLib;
using ReactiveUI;

using Plugin;
using Positec;

namespace AvaApp.ViewModels {
  public class ParaTypeSelector : IDataTemplate {
    // This Dictionary should store our shapes. We mark this as [Content], so we can directly add elements to it later.
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = [];

    // Build the DataTemplate here
    public Control? Build(object? param) {
      if( param is PluginParaBase ppb ) { // If the key is null, we throw an ArgumentNullException
        string key = ppb.ParaType.ToString();

        return AvailableTemplates[key].Build(param); // finally we look up the provided key and let the System build the DataTemplate for us
      } else throw new ArgumentNullException(nameof(param));
    }

    // Check if we can accept the provided data
    public bool Match(object? data) {
      if( data is PluginParaBase ppb ) return AvailableTemplates.ContainsKey(ppb.ParaType.ToString());

      // Our Keys in the dictionary are strings, so we call .ToString() to get the key to look up
      var key = data?.ToString();

      return data is ParaType                        // the provided data needs to be our enum type
              && !string.IsNullOrEmpty(key)           // and the key must not be null or empty
              && AvailableTemplates.ContainsKey(key); // and the key must be found in our Dictionary
    }
  }

  public class PluginTabViewModel : ViewModelBase {
    public class PluginEntry : ViewModelBase {
      public string Name { get; private set; }
      public string Desc { get; private set; }
      public bool Check {
        get { return _Check; }
        set { this.RaiseAndSetIfChanged(ref _Check, value); }
      } bool _Check;
      internal IPlugin Script { get; set; }

      public PluginEntry(string name, bool check, IPlugin script) {
        Name = name;
        Desc = script.Desc;
        Check = check;
        Script = script;
      }
    }

    public List<PluginEntry> PluginList { get; private set; }
    public PluginEntry? SelPlugin {
      get { return _SelPlugin; }
      set { 
        this.RaiseAndSetIfChanged(ref _SelPlugin, value);
        if( SelPlugin != null ) {
          ParaList = SelPlugin.Script.Paras;
          this.RaisePropertyChanged(nameof(PluginDesc));
          this.RaisePropertyChanged(nameof(ParaList));
          this.RaisePropertyChanged(nameof(CanDoIt));
          ParaDesc = string.Empty;
          this.RaisePropertyChanged(nameof(ParaDesc));
        }
      }
    } PluginEntry? _SelPlugin = null;
    public string PluginDesc => SelPlugin?.Desc ?? string.Empty;

    public List<PluginParaBase> ParaList { get; private set; }

    //public int PluginIdx {
    //  get { return _PluginIdx; }
    //  set { PluginSelChg(_PluginIdx = value); }
    //}
    //int _PluginIdx;

    //private void PluginSelChg(int idx) {
    //  if( 0 <= idx && idx < PluginList.Count ) PluginData = PluginList[idx].Script.Paras;
    //  else PluginData = new();
    //  this.RaisePropertyChanged(nameof(PluginData));
    //  this.RaisePropertyChanged(nameof(CanDoIt));
    //  ParaDesc = string.Empty;
    //  this.RaisePropertyChanged(nameof(ParaDesc));
    //}

    public PluginParaBase? SelPara {
      get { return _SelPara; }
      set {
        _SelPara = value;
        if( value != null ) {
          ParaDesc = value.Description;
          this.RaisePropertyChanged(nameof(ParaDesc));
        }
      }
    }
    PluginParaBase? _SelPara;

    public string ParaDesc { get; private set; }

    public bool CanDoIt {
      get {
        MainWindowViewModel mw = MainWindowViewModel.Instance;

        //return 0 <= PluginIdx && PluginIdx < PluginList.Count && _client != null && _client.Connected &&
        //       idx != -1 && _client.Mowers[idx] != null && _client.Mowers[idx] is MowerP0;
        return SelPlugin != null && mw.Online; // && mw.SelMow              idx != -1 && _client.Mowers[idx] != null && _client.Mowers[idx] is MowerP0;
      }
    }
    public void CmdDoIt() {
      if( SelPlugin != null ) {
        MowerBase? mb = MainWindowViewModel.Instance.Mower;

        if( mb is MowerP0 mo ) {
          if( mo.Mqtt != null ) {
            PluginData pd = new() {
              Name = MainWindowViewModel.Instance.Name,
              Config = mo.Mqtt.Cfg,
              Data = mo.Mqtt.Dat
            };
            SelPlugin.Script.Doit(pd);
          }
        }
      }
    }

    public void CmdToDo(Version v, string k, MqttP0 m) {
      PluginData pd = new() { Version = v, Name = k, Config = m.Cfg, Data = m.Dat };

      foreach( PluginEntry pe in PluginList ) if( pe.Check ) Dispatcher.UIThread.InvokeAsync(() => pe.Script.Todo(pd));
    }

    public PluginTabViewModel() {
      PluginList = [
        new PluginEntry("Plugin 01", true, new DesignPlugin("The description of the plugin 1 at design time ...")),
        new PluginEntry("Plugin 02", true, new DesignPlugin("The description of the plugin 2 at design time ..."))
      ];
      foreach( var pe in PluginList ) pe.PropertyChanged += ItemChanged;
      ParaList = [];
      ParaDesc = string.Empty;
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e) {
      if( sender is PluginEntry pe ) {
        SelPlugin = pe;
      }
    }
    internal class DesignPlugin(string desc) : IPlugin {
      readonly string _desc = desc;
      string IPlugin.Desc => _desc;
      List<PluginParaBase> IPlugin.Paras => [
        new PluginParaBool("Parameter Bool", false, "Long description of parameter boolean at design time ..."),
        new PluginParaText("Parameter Text", "Text", "..."),
        new PluginParaCase("Parameter Enum", 0, ["Case 1", "Case 2"], "..."),
        new PluginParaReal("Parameter Real", 123.456, "...")
      ];
      void IPlugin.Doit(PluginData pd) { }
      void IPlugin.Todo(PluginData pd) { }
      void IDisposable.Dispose() { }
    }

    private void LogPlugin(string log) { Trace.TraceInformation(log); }
    internal void Init(List<string>? cfg) {
      string dir = DeskApp.DirPlugin;
      Stopwatch sw = Stopwatch.StartNew();

      PluginList.Clear();
      Trace.TraceInformation($"Plugin.Init Beg => {sw.ElapsedMilliseconds}");
      DeskApp.TraceDelegate = LogPlugin;
      DeskApp.SendDelegate = MainWindowViewModel.Publish;

      Trace.TraceInformation($"Plugin.Init Hdl => {sw.ElapsedMilliseconds}");
      SelPlugin = null;
      foreach( string script in Directory.GetFiles(dir, "Plugin*.cs") ) {
        try {
          string name = Path.GetFileNameWithoutExtension(script)[6..];

          if( CSScript.Evaluator.LoadFile(script) is IPlugin plugin ) PluginList.Add(new PluginEntry(name, cfg != null && cfg.Contains(name), plugin));
          else Trace.TraceWarning($"Plugin {name} not loaded");
          Trace.TraceInformation($"Plugin.Init {name} => {sw.ElapsedMilliseconds}");
        } catch( Exception ex ) {
          //MessageBox.Show(ex.ToString(), "Load Plugin " + script, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
          Trace.TraceError($"Load plugin {script} {ex}");
        }
      }
      this.RaisePropertyChanged(nameof(PluginList));
      Trace.TraceInformation($"Plugin.Init End => {sw.ElapsedMilliseconds}");
    }
    internal void Fini(out List<string> cfg) {
      cfg = [];

      foreach( PluginEntry pe in PluginList ) {
        if( pe.Check ) cfg.Add(pe.Name);
        pe.Script.Dispose();
      }
      PluginList.Clear();
    }
  }
}
