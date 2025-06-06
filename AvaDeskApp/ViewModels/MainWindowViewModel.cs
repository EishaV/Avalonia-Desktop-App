﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using ReactiveUI;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

using Plugin;
using Positec;
using AvaApp.Views;
using static AvaApp.ViewModels.ConfigTabViewModel;

namespace AvaApp.ViewModels {
  [DataContract]
  public struct CfgFrame {
    [DataMember(Name = "x")] public int X = 0;
    [DataMember(Name = "y")] public int Y = 0;
    [DataMember(Name = "w")] public double W = 300;
    [DataMember(Name = "h")] public double H = 500;

    public CfgFrame() {
      X = Y = 0;
      W = 300; H = 500;
    }
  }

  [DataContract]
  public class CfgJson {
    [DataMember(Name = "uuid")] public string? Uuid;
    [DataMember(Name = "api")] public string? Api;
    [DataMember(Name = "mail")] public string? Mail;
    [DataMember(Name = "name")] public string? Name;
    [DataMember(Name = "frame")] public CfgFrame Frame;
    [DataMember(Name = "plugins")] public List<string>? Plugins;
    [DataMember(Name = "mode", EmitDefaultValue =false)] public string? Mode;

    public bool Equals(CfgJson cfg) {
      bool b;

      b = Uuid == cfg.Uuid && Api == cfg.Api && Mail == cfg.Mail && Name == cfg.Name;
      //b = b && Top == lsj.Top && X == lsj.X && Y == lsj.Y && W == lsj.W && H == lsj.H;
      if(b && Plugins != null && cfg.Plugins != null) {
        b = Plugins.Count == cfg.Plugins.Count;
        for(int i = 0; b && i < Plugins.Count; i++) b = b && Plugins[i] == cfg.Plugins[i];
      }
      return b;
    }
  }

  public class MainWindowViewModel : ViewModelBase {
    internal static string GetConfigFile() => Path.Combine(PositecApi.DirData, "AvaDeskApp.config.json");
    internal static string GetTraceFile(int i) => Path.Combine(PositecApi.DirTrace, $"AvaDeskApp.trace.{i}.txt");

    public CfgJson Config => _Config;
    private readonly CfgJson _Config = new();

    public bool Splash { get; private set; }
    public string Uuid { get; set; }
    public static Version Version { get; set; }

    internal static Window? AppMainWindow {
      get {
        if( Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ) return desktop.MainWindow;
        return null;
      }
    }

    private readonly Dictionary<string, PositecApi> DicCli = [];

    public ObservableCollection<string> MowNames { get; set; }

    public string Name {
      get => _Name;
      set {
        _Name = value;
        this.RaisePropertyChanged(nameof(Name));
        UpdateMqtt();
        this.RaisePropertyChanged(nameof(CanTabCfg));
        Activities.Clear();
      }
    }
    private string _Name;

    public PositecApi? Client {
      get {
        if( !string.IsNullOrEmpty(Name) ) {
          foreach( PositecApi pa in DicCli.Values ) {
            if( pa != null ) {
              if( pa.Mowers.ContainsKey(Name) ) return pa;
            } else Debug.WriteLine("Client null client");
          }
        } else Debug.WriteLine("Client null name");
        return null;
      }
    }

    FileSystemWatcher? _fsw = null;
    MowerBase? _mb = null;

    public MowerBase? Mower {
      get {
        if( _mb != null ) return _mb;

        if( !string.IsNullOrEmpty(Name) ) {
          foreach( PositecApi pa in DicCli.Values ) {
            if( pa != null ) {
              if( pa.Connected ) {
                if( pa.Mowers.TryGetValue(Name, out MowerBase? val) ) return val;
              } else Debug.WriteLine("Mower not connect");
            } else Debug.WriteLine("Mower null client");
          }
        } else Debug.WriteLine("Mower null name");
        return null;
      }
    }

    public int TabIdx {
      get => _TabIdx;
      set {
        this.RaiseAndSetIfChanged(ref _TabIdx, value);
        this.RaisePropertyChanged(nameof(CanPoll));
        if( TabIdx == 1 && Mower is MowerP0 mo ) ConfigVM?.Refresh(mo.Mqtt, true);
        if( TabIdx == 2 ) this.RaisePropertyChanged(nameof(CanSend));
        if( TabIdx == 3 && Activities.Count == 0 ) Dispatcher.UIThread.InvokeAsync(() => ActCmdCall());
      }
    }
    private int _TabIdx;

    public StatusViewModel? StatusVM { get; }
    public ConfigTabViewModel ConfigVM { get; }
    public PluginTabViewModel? PluginVM { get; }

    public bool Online => Mower != null && Mower.Product.Online;

    public bool CanPoll => TabIdx == 0 || TabIdx == 2 || TabIdx == 3 || TabIdx == 4;
    public async void CmdPoll() {
      if( TabIdx == 3 ) await ActCmdCall();
      else {
        PositecApi? pa = Client;

        if( pa != null ) {
          if( pa.Connected ) pa.Publish("", Name);
          else {
            MowerBase mb = pa.Mowers[Name];

            await pa.GetStatus(Name);
            if( !string.IsNullOrEmpty(mb.Json) ) {
              MqttJson = mb.Json;
              if( mb is MowerP0 mo && mo.Mqtt != null ) StatusVM?.Refresh(mo.Mqtt);
              if( mb is MowerP1 mn && mn.Mqtt != null ) StatusVM?.Refresh(mn.Mqtt);
            }
          }
        }
      }
    }
    public void CmdMode() {
      if( Application.Current is Application a ) {
        if( a.ActualThemeVariant == ThemeVariant.Dark ) a.RequestedThemeVariant = ThemeVariant.Light;
        else a.RequestedThemeVariant = ThemeVariant.Dark;
        UpdateMqtt();
      }
    }

    public bool CanTabCfg => Mower is MowerP0;
    public bool CanTabAct => _mb == null;

    public async Task ResetBlade() { if( Client is PositecApi pa ) await pa.ResetBlade(Name); }

    public void Publish(string json) {
      if( Client is PositecApi pa ) pa.Publish(json, Name);
    }

    public void Publish(string json, string name) {
      foreach( PositecApi pa in DicCli.Values ) {
        foreach( MowerBase mb in pa.Mowers.Values ) {
          if( mb.Product.Name == name ) pa.Publish(json, Name);
        }
      }
    }

    public static void Publish(string json, int idx) {
      MainWindowViewModel mwvm = MainWindowViewModel.Instance;

      if( 0 <= idx && idx < mwvm.MowNames.Count ) mwvm.Publish(json, mwvm.MowNames[idx]);
    }

    #region Constructor's
    public static MainWindowViewModel Instance => _Instance;
    private static readonly MainWindowViewModel _Instance;

    static MainWindowViewModel() {
      Assembly assembly = Assembly.GetExecutingAssembly();
      FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

      Version = new(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart);
      _Instance = new MainWindowViewModel();
    }

    public MainWindowViewModel() {
      CfgJson cfg = DeskApp.GetJson<CfgJson>(GetConfigFile()) ?? new();

      CmdLogin = ReactiveCommand.Create(Login);
      MowNames = [];
      _Name = string.Empty;
      _Json = string.Empty;
      MqttIn = string.Empty;
      Activities = [];
      Uuid = cfg.Uuid ?? Guid.NewGuid().ToString();
      Splash = true;
      ConfigVM = ConfigTabViewModel.Instance;

      if( Design.IsDesignMode ) return;

      //_lckFile = new FileStream(Path.Combine(AppContext.BaseDirectory, "AvaApp.lck"), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);

      for( int i = 5; i > 0; i-- ) {
        string last = GetTraceFile(i);
        string next = GetTraceFile(i - 1);

        if( File.Exists(last) ) File.Delete(last);
        if( File.Exists(next) ) File.Move(next, last);
      }
      var stl = new StampTraceListener(GetTraceFile(0)) { TraceOutputOptions = TraceOptions.DateTime };
      Trace.Listeners.Add(stl);

      if( Application.Current is Application a ) a.RequestedThemeVariant = cfg.Mode == "L" ? ThemeVariant.Light : ThemeVariant.Dark;

      StatusVM = new StatusViewModel();
      PluginVM = new PluginTabViewModel();

      UsrMail = cfg.Mail; this.RaisePropertyChanged(nameof(UsrMail));
      _Config = cfg;
    }
    #endregion

    #region Account
    public static List<string> UsrApis => [.. PositecApi.ApiDic.Keys];
    public string? UsrApi { get; set; }
    public string? UsrMail { get; set; }
    public string? UsrPass { get; set; }

    public ReactiveCommand<Unit, Unit> CmdLogin { get; }

    private async Task<bool> BegApi(string api, PositecApi pa) {
      Trace.TraceInformation($"Main.BegApi {api}");
      DicCli.Add(api, pa);
      foreach( string key in pa.Mowers.Keys ) {
        MowNames.Add(key);
        await pa.GetStatus(key);
      }
      pa.RecvMqtt += Client_RecvMqtt;
      return await pa.Start();
    }
    private void EndAPi(string api) {
      if( DicCli.TryGetValue(api, out PositecApi? pa) && pa != null ) {
        Trace.TraceInformation($"Main.EndApi {api}");
        pa.RecvMqtt -= Client_RecvMqtt;
        pa.Exit();
        DicCli.Remove(api);
      }
    }

    public async void Login() {
      lasterror = string.Empty;
      if( UsrApi != null && UsrMail != null && UsrPass != null ) {
        Button? b = AppMainWindow?.FindControl<Button>("BtnAcc");
        string api2 = UsrApi[..2];
        string mail = UsrMail;
        string pass = UsrPass;
        PositecApi pa = new(Err, api2);

        b?.Flyout?.Hide();
        Trace.TraceInformation($"Main.Login {api2} {mail}");

        EndAPi(api2);
        if( await pa.Login(api2, mail, pass) && pa.Mowers.Count > 0 ) {
          Trace.TraceInformation($"Main.Login Start {api2} Mqtt");
          if( await BegApi(api2, pa) ) {
            Trace.TraceInformation($"Main.Login success");
            Name = pa.Mowers.Keys.First();
            if( StatusVM != null ) StatusVM.CanPoll = pa.Connected;
          } else await ErrorMsg("Fehler - Verbinden");
        } else await ErrorMsg("Fehler - Anmelden");
      }
    }

    public static void CmdTrace() {
      string trc = GetTraceFile(0);

      for( int i = 0; i < Trace.Listeners.Count; i++ ) {
        if( Trace.Listeners[i] is StampTraceListener ) {
          Trace.Listeners[i].Close();
          Trace.Listeners.RemoveAt(i);
          break;
        }
      }
      using( Process editor = new() ) {
        editor.StartInfo.FileName = trc;
        editor.StartInfo.UseShellExecute = true;
        editor.Start();
      }
      Trace.Listeners.Add(new StampTraceListener(trc));
    }
    #endregion

    #region Events
    private void Client_RecvMqtt(object? sender, RecvEventArgs e) {
      if( e.Key == Name && Mower is MowerBase mb ) {
        MqttJson = mb.Json;
        if( mb is MowerP0 mo ) {
          Dispatcher.UIThread.InvokeAsync(() => StatusVM?.Refresh(mo.Mqtt));
          Dispatcher.UIThread.InvokeAsync(() => ConfigVM?.Refresh(mo.Mqtt, false));
        }
        if( mb is MowerP1 mn ) {
          Dispatcher.UIThread.InvokeAsync(() => StatusVM?.Refresh(mn.Mqtt));
          //Dispatcher.UIThread.InvokeAsync(() => ConfigVM.Refresh(mo.Mqtt, false));
        }
      }
      if( DicCli[e.Api] is PositecApi pa && pa?.Mowers[e.Key] is MowerP0 mp ) PluginVM?.CmdToDo(Version, e.Key, mp.Mqtt);
    }

    public async void MainWindow_Opened(object? sender, System.EventArgs e) {
      Stopwatch sw = Stopwatch.StartNew();
      string dir = PositecApi.DirData, npi = "ProductItem.json";
      bool acc = false;

      Trace.TraceInformation($"Main.Open Beg => {sw.ElapsedMilliseconds}");
      //if( Application.Current is Application a ) a.RequestedThemeVariant = Config.Mode == "L" ? ThemeVariant.Light : ThemeVariant.Dark;
      PluginVM?.Init(Config.Plugins);
      Trace.TraceInformation($"Main.Open Pgn => {sw.ElapsedMilliseconds}");

      if( Config.Api != null) {
        UsrApi = UsrApis.FirstOrDefault(x => x.StartsWith(Config.Api));
        this.RaisePropertyChanged(nameof(UsrApi));
      }

      if( File.Exists(Path.Combine(dir, npi)) ) {
        string name = "CmdOut.json";
        ProductItem? pi = DeskApp.GetJson<ProductItem>(Path.Combine(dir, npi));

        if( pi != null ) {
          Trace.TraceInformation($"Main.Open Api IO => {sw.ElapsedMilliseconds}");
          if( pi.Protocol == 0 ) _mb = new MowerP0() { Product = pi };
          else if( pi.Protocol == 1 ) _mb = new MowerP1() { Product = pi };
          this.RaisePropertyChanged(nameof(CanTabAct));
          CheckCmdOut(Path.Combine(dir, name));
          UpdateMqtt();
          MowNames.Add(pi.Name ?? "Dummy");
          this.RaisePropertyChanged(nameof(MowNames));
          Name = pi.Name ?? "Dummy";
          _fsw = new(dir, name) { NotifyFilter = NotifyFilters.LastWrite };
          _fsw.Changed += Watcher_Changed;
          _fsw.Created += Watcher_Created;
          _fsw.EnableRaisingEvents = true;
        }
      } else {
        foreach( string tf in Directory.GetFiles(PositecApi.DirData, "Token.??.json") ) {
          string api = tf.Substring(tf.Length - 7, 2);
          PositecApi pa = new(Err, Uuid);

          if( await pa.Access(api) && pa.Mowers.Count > 0 ) {
            if( acc = await BegApi(api, pa) ) {
              Trace.TraceInformation($"Main.Open Api {api} => {sw.ElapsedMilliseconds}");
              if( StatusVM != null ) StatusVM.CanPoll = pa.Connected;
            } else await ErrorMsg("Fehler - Verbinden");
          } else await ErrorMsg("Fehler - Anmelden");
        }
        if( acc ) {
          this.RaisePropertyChanged(nameof(MowNames));
          if( !string.IsNullOrEmpty(Config.Name) && MowNames.Contains(Config.Name) ) Name = Config.Name;
        } else {
          Button? b = AppMainWindow?.FindControl<Button>("BtnAcc");

          b?.Flyout?.ShowAt(b);
        }
      }
      Splash = false; this.RaisePropertyChanged(nameof(Splash));

      Trace.TraceInformation($"Main.Open End => {sw.ElapsedMilliseconds}");
    }

    public void MainWindow_Activated(object? sender, System.EventArgs e) {
      if( TabIdx == 0 && Mower != null && DateTime.Now - Mower.LastRecv > TimeSpan.FromMinutes(1) ) Publish("");
    }
    private void CheckCmdOut(string path) {
      if( File.Exists(path) ) {
        string js = File.ReadAllText(path);

        if( _mb is MowerBase mb ) mb.Json = PositecApi.FormatJson(js);
        if( _mb is MowerP0 mo && DeskApp.GetJson<MqttP0>(path) is MqttP0 m0 ) mo.Mqtt = m0;
        if( _mb is MowerP1 mn && DeskApp.GetJson<MqttP1>(path) is MqttP1 m1 ) mn.Mqtt = m1;
      }
    }
    
    private void Watcher_Created(object sender, FileSystemEventArgs e) {
      throw new NotImplementedException();
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e) {
      Trace.TraceInformation($"WatcherChanged {e.Name}");
      CheckCmdOut(e.FullPath);
      Dispatcher.UIThread.InvokeAsync(UpdateMqtt);
    }

    public void MainWindow_Closing(object? sender, CancelEventArgs e) {
      CfgJson cfg = new();

      if( sender is MainWindow mw && mw.WindowState == WindowState.Normal ) {
        cfg.Frame = new() { X = mw.Position.X, Y = mw.Position.Y, W = mw.Width, H = mw.Height };
      }
      cfg.Uuid = Uuid;
      cfg.Api = UsrApi;
      cfg.Mail = UsrMail;
      cfg.Name = Name;
      cfg.Mode = Application.Current?.ActualThemeVariant == ThemeVariant.Light ? "L" : "D";
      PluginVM?.Fini(out cfg.Plugins);
      DeskApp.PutJson(GetConfigFile(), cfg);
    }

    public void MainWindow_Closed(object? sender, EventArgs e) {
      while( DicCli.Count > 0 ) EndAPi(DicCli.Keys.First());
    }
    #endregion

    #region Functions

    private void UpdateMqtt() {
      if( Mower is MowerBase mb ) {
        if( Client is PositecApi pa ) StatusVM?.UpdateProduct(pa.Api, mb.Product);
        else if( _mb != null && UsrApi != null ) StatusVM?.UpdateProduct(UsrApi, mb.Product);
        if( mb is MowerP0 mo ) {
          if( mo.Mqtt != null && !string.IsNullOrEmpty(mb.Json) ) {
            StatusVM?.Refresh(mo.Mqtt);
            ConfigVM?.Refresh(mo.Mqtt, true);
          }
        }
        if( mb is MowerP1 mn ) {
          if( mn.Mqtt != null || !string.IsNullOrEmpty(mb.Json) ) {
            StatusVM?.Refresh(mn.Mqtt);
            //ConfigVM.Refresh(mo.Mqtt, true);
          }
        }
        MqttJson = mb.Json;
        //this.RaisePropertyChanged(nameof(Activities));
      }
    }
    #endregion

    #region Tab Mqtt
    public string MqttJson {
      get => _Json;
      set => this.RaiseAndSetIfChanged(ref _Json, value);
    }
    private string _Json;

    public static List<string> MqttIns => [
      "\"sc\":{\"m\":1} - Scheduler => manual",
      "\"mzk\":0 - Zone keeper => off", "\"mzk\":1 - Zone keeper => on",
      "\"tq\":0 - Torque normal => zero" ];
    public string MqttIn { get; set; }

    public bool CanSend => Online;
    public void MqttCmd() {
      if( !string.IsNullOrEmpty(MqttIn) ) {
        int p = MqttIn.IndexOf(" - ");
        string s = p > 0 ? MqttIn[..p] : MqttIn;

        Publish(s);
      }
    }
    #endregion

    #region Tab Act
    public class ActEntryVM : INotifyPropertyChanged {
      public event PropertyChangedEventHandler? PropertyChanged;

      internal void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

      enum ActColors { None, Gras, Idle, Rain, Error }

      public string Stamp { get; private set; }
      public string State { get; private set; }
      //public string Error { get; private set; }
      public string Charge { get; private set; }

      private readonly ActColors _ac = ActColors.None;  
      public IBrush? Color {
        get {
          return _ac switch {
            ActColors.Gras => new SolidColorBrush() { Opacity = 0.2, Color = Colors.Green },
            ActColors.Idle => new SolidColorBrush() { Opacity = 0.2, Color = Colors.Magenta },
            ActColors.Rain => new SolidColorBrush() { Opacity = 0.2, Color = Colors.Blue },
            ActColors.Error => new SolidColorBrush() { Opacity = 0.2, Color = Colors.Red },
            _ => Brushes.Transparent
          }; 
        }
      }

      public ActEntryVM(ActivityEntry ae) {
        ActivityData d = ae.Payload.Dat;

        Stamp = DateTime.Parse(ae.Stamp).ToLocalTime().ToString(); // ae.Payload.Cfg.Date + " " + ae.Payload.Cfg.Time;
        Charge = d.Battery.Charging == ChargeCoge.CHARGING ? "+" : "-";
        if( d.LastError == ErrorCode.NONE ) {
          State = $"{d.LastState}";
          if( d.LastState == StatusCode.GRASS_CUTTING || d.LastState == StatusCode.BORDER_CUT ) _ac = ActColors.Gras;
          else if( d.LastState == StatusCode.IDLE ) _ac = ActColors.Idle;
          else _ac = ActColors.None;
        } else {
          State = $"{d.LastError}";
          if( d.LastError == ErrorCode.RAINING ) _ac = ActColors.Rain;
          else _ac = ActColors.Error;
        }
        //li.SubItems.Add(a.Payload.Dat.Battery.Maintenance.ToString());
        //li.ToolTipText = a.Stamp;
      }
    }
    public ObservableCollection<ActEntryVM> Activities { get; private set; }

    public async Task ActCmdCall() {
      Activities.Clear();
      if( Client is PositecApi pa && await pa.CheckToken() ) {
        var lae = await pa.GetActivities(Name);

        for( int i = 0; i < lae.Count; i++ ) {
          var ae = lae[i];

          if(ae.Payload.Dat.LastState == StatusCode.IDLE 
             && i > 0 && lae[i - 1].Payload.Dat.LastState == StatusCode.HOME) {
            DateTime dt0 = DateTime.Parse(ae.Stamp);
            DateTime dt1 = DateTime.Parse(lae[i - 1].Stamp);
            TimeSpan ts = dt1 - dt0;

            if( ts.TotalSeconds < 5 ) continue;
          }
          if( ae.Payload.Dat.LastState == StatusCode.HOME && ae.Payload.Dat.Battery.Charging == ChargeCoge.CHARGED
             && i < lae.Count - 1 && lae[i + 1].Payload.Dat.LastState == StatusCode.IDLE ) {
            DateTime dt0 = DateTime.Parse(ae.Stamp);
            DateTime dt1 = DateTime.Parse(lae[i + 1].Stamp);
            TimeSpan ts = dt0 - dt1;

            if( ts.TotalSeconds < 5 ) continue;
          }
          Activities.Add(new ActEntryVM(ae));
        }
      }
      this.RaisePropertyChanged(nameof(Activities));
    }

    public async void ActCmdJson() {
      string name = $"ActLog_{Name}.json";
      string path = Path.Combine(PositecApi.DirTrace, name);

      if( !File.Exists(path) ) await ActCmdCall();
      using Process editor = new();
      editor.StartInfo.FileName = path;
      editor.StartInfo.UseShellExecute = true;
      editor.Start();
    }
    #endregion

    private string lasterror = string.Empty;
    private void Err(String s) {
      Debug.WriteLine($"Err => {s}");
      lasterror = s;
    }

    public async Task ErrorMsg(string s) {
      var msgpar = new MessageBoxStandardParams {
        ButtonDefinitions = ButtonEnum.Ok, Icon = Icon.Error, CanResize = true, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ContentTitle = s, ContentHeader = "Error", ContentMessage = lasterror
      };
      var msgbox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(msgpar);

      if(AppMainWindow != null) await msgbox.ShowWindowDialogAsync(AppMainWindow);
      else await msgbox.ShowWindowAsync();
    }
  }
}


/*
  <Setter Property="Foreground" Value="{Binding ActColor, Converter={x:Static vm:MainWindowViewModel.ActColorConverter}}" />
  public static FuncValueConverter<string?, IBrush> ActColorConverter { get; } = new FuncValueConverter<string?, IBrush>(ac => {
    bool b = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    return ac switch {
      "Gras" => b ? Brushes.Magenta : Brushes.Green,
      "Idle" => b ? Brushes.LightPink : Brushes.Yellow,
      "Rain" => b ? Brushes.Aqua : Brushes.Blue,
      "Error" => b ? Brushes.LightCoral : Brushes.Red,
      _ => b ? Brushes.White : Brushes.Black,
    };
  }); // new FuncValueConverter<string?, IBrush>(num => $"Your number is: '{num}'");


  <DataGrid.Resources>
    <vm:ActLogConverter x:Key="ALC"/>
  </DataGrid.Resources>
  <Setter Property="Foreground" Value="{Binding ActColor, Converter={StaticResource ALC}}" />
  public class ActLogConverter : IValueConverter {
    public static readonly ActLogConverter Instance = new ();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
      bool b = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

      if( value is string s && targetType.IsAssignableTo(typeof(IBrush)) ) {
        return s switch {
          "Gras" => b ? Brushes.Lime : Brushes.Green,
          "Idle" => b ? Brushes.LightPink : Brushes.DarkMagenta,
          "Rain" => b ? Brushes.Aqua : Brushes.Blue,
          "Error" => b ? Brushes.LightCoral : Brushes.Red,
          _ => b ? Brushes.White : Brushes.Black,
        };
      }
      // converter used for the wrong type
      return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
      throw new NotSupportedException();
    }
  }

*/