using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using ReactiveUI;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

using Plugin;
using Positec;
using AvaApp.Views;
using Avalonia.Platform;
using Avalonia.Styling;

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
    [DataMember(Name = "midx")] public int? Midx;
    [DataMember(Name = "frame")] public CfgFrame Frame;
    [DataMember(Name = "plugins")] public List<string>? Plugins;

    public bool Equals(CfgJson cfg) {
      bool b;

      b = Uuid == cfg.Uuid && Api == cfg.Api && Mail == cfg.Mail && Midx == cfg.Midx;
      //b = b && Top == lsj.Top && X == lsj.X && Y == lsj.Y && W == lsj.W && H == lsj.H;
      if(b && Plugins != null && cfg.Plugins != null) {
        b = Plugins.Count == cfg.Plugins.Count;
        for(int i = 0; b && i < Plugins.Count; i++) b = b && Plugins[i] == cfg.Plugins[i];
      }
      return b;
    }
  }

  public class MainWindowViewModel : ViewModelBase {
    internal static string GetConfigFile() => Path.Combine(DeskApp.DirData, "AvaApp.config.json");
    internal static string GetTraceFile(int i) => Path.Combine(DeskApp.DirTrace, $"AvaApp.trace.{i}.txt");

    public CfgJson Config => _Config;
    private readonly CfgJson _Config = new();

    public bool Splash { get; private set; }
    public string Uuid { get; set; }
    public Version? Version { get; set; }

    internal static Window? AppMainWindow {
      get {
        if( Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ) return desktop.MainWindow;
        return null;
      }
    }

    public PositecApi Client => client;
    private readonly PositecApi client;

    public IImage? ImgMode {
      get {
        const string AvaAss = "avares://AvaDeskApp/Assets";
        Stream asset;
        
        if( Application.Current?.ActualThemeVariant == ThemeVariant.Dark ) asset = AssetLoader.Open(new Uri($"{AvaAss}/Sun.png"));
        else asset = AssetLoader.Open(new Uri($"{AvaAss}/Moon.png"));
        return new Bitmap(asset);
      }
    }
    public void CmdMode() {
      if( Application.Current is Application a ) {
        if( a.ActualThemeVariant == ThemeVariant.Dark ) a.RequestedThemeVariant = ThemeVariant.Light;
        else  a.RequestedThemeVariant = ThemeVariant.Dark;
        this.RaisePropertyChanged(nameof(ImgMode));
      }
    }

    public List<string>? MowNames { get; set; }
    public int MowIdx {
      get => _MowIdx;
      set {
        _MowIdx = value;
        if( 0 <= _MowIdx && _MowIdx < client.Mowers.Count && UsrApi != null ) {
          StatusVM?.SetProductImage(UsrApi[..2], MowIdx);
          if( client.Mowers[_MowIdx] is MowerP0 mo ) {
            if( mo.Mqtt != null && !string.IsNullOrEmpty(client.Mowers[MowIdx].Json) ) {
              StatusVM?.Refresh(mo.Mqtt);
              ConfigVM?.Refresh(mo.Mqtt, true);
            }
          }
          if( client.Mowers[_MowIdx] is MowerP1 mn ) {
            if( mn.Mqtt != null || !string.IsNullOrEmpty(client.Mowers[MowIdx].Json) ) {
              StatusVM?.Refresh(mn.Mqtt);
              //ConfigVM.Refresh(mo.Mqtt, true);
            }
          }
          this.RaisePropertyChanged(nameof(MowIdx));
          this.RaisePropertyChanged(nameof(CanTabCfg));
          MqttJson = client.Mowers[MowIdx].Json;
          Activities.Clear();
          //this.RaisePropertyChanged(nameof(Activities));
        }
      }
    }
    private int _MowIdx;

    public int TabIdx {
      get => _TabIdx;
      set {
        this.RaiseAndSetIfChanged(ref _TabIdx, value);
        this.RaisePropertyChanged(nameof(CanPoll));
        if( TabIdx == 2 && client.Mowers[_MowIdx] is MowerP0 mo ) ConfigVM?.Refresh(mo.Mqtt, true);
        if( TabIdx == 3 && Activities.Count == 0 ) Dispatcher.UIThread.InvokeAsync(() => ActCmdCall());
      }
    }
    private int _TabIdx;


    public StatusViewModel? StatusVM { get; }
    public ConfigTabViewModel ConfigVM { get; }
    public PluginTabViewModel? PluginVM { get; }

    public bool CanPoll => TabIdx == 0 || TabIdx == 2 || TabIdx == 3 || TabIdx == 4;
    public async void CmdPoll() {
      if( TabIdx == 3 ) await ActCmdCall();
      else {
        if( Client != null ) {
          if( Client.Connected ) Client.Publish("", MowIdx);
          else {
            MowerBase mb = client.Mowers[MowIdx];

            await Client.GetStatus(MowIdx);
            if( !string.IsNullOrEmpty(mb.Json) ) {
              MqttJson = mb.Json;
              if( mb is MowerP0 mo && mo.Mqtt != null ) StatusVM?.Refresh(mo.Mqtt);
              if( mb is MowerP1 mn && mn.Mqtt != null ) StatusVM?.Refresh(mn.Mqtt);
            }
          }
        }
      } 
    }

    public bool CanTabCfg => 0 <= MowIdx && MowIdx < client.Mowers.Count 
                          && client.Mowers[MowIdx] is MowerP0;

    #region Constructor's
    private static readonly MainWindowViewModel _Instance;
    public static MainWindowViewModel Instance => _Instance;

    static MainWindowViewModel() {
      _Instance = new MainWindowViewModel(); ;
    }

    public MainWindowViewModel() {
      CfgJson cfg = DeskApp.GetJson<CfgJson>(GetConfigFile()) ?? new();

      CmdLogin = ReactiveCommand.Create(Login);
      _Json = string.Empty;
      MqttIn = string.Empty;
      Activities = [];
      Uuid = cfg.Uuid ?? Guid.NewGuid().ToString();
      Splash = true;
      client = new PositecApi(Err, Uuid);
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

      StatusVM = new StatusViewModel();
      PluginVM = new PluginTabViewModel(client);

      if(cfg.Api != null) { UsrApi = UsrApis.FirstOrDefault(x => x.StartsWith(cfg.Api)); this.RaisePropertyChanged(nameof(UsrApi)); }
      UsrMail = cfg.Mail; this.RaisePropertyChanged(nameof(UsrMail));
      _Config = cfg;
    }
    #endregion

    #region Account
    public static List<string> UsrApis => [ "WX - Worx Landroid", "KR - Kress Mission", "LX - LandXcape", "SM - Ferrex Smartmower" ];
    public string? UsrApi { get; set; }
    public string? UsrMail { get; set; }
    public string? UsrPass { get; set; }

    public ReactiveCommand<Unit, Unit> CmdLogin { get; }
    public async void Login() {
      lasterror = string.Empty;
      if( UsrApi != null && UsrMail != null && UsrPass != null ) {
        Button? b = AppMainWindow?.FindControl<Button>("BtnAcc");
        string api = UsrApi[..2];
        string mail = UsrMail;
        string pass = UsrPass;

        b?.Flyout?.Hide();
        if( client.Connected ) {
          Trace.TraceInformation($"Main.Login Exiting old connection");
          client.Exit();
        }
        Trace.TraceInformation($"Main.Login {api} {mail}");
        if( await client.Login(api, mail, pass) && client.Mowers.Count > 0 ) {
          Trace.TraceInformation($"Main.Login Start Mqtt");
          if( await StartMqtt() ) {
            await client.GetStatus(0);
            MowIdx = 0;
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
      if( client != null && e.MowIdx == MowIdx ) {
        MqttJson = client.Mowers[MowIdx].Json;
        if( client.Mowers[_MowIdx] is MowerP0 mo) {
          StatusVM?.Refresh(mo.Mqtt);
          Dispatcher.UIThread.InvokeAsync(() => ConfigVM?.Refresh(mo.Mqtt, false));
        }
        if(client.Mowers[_MowIdx] is MowerP1 mn) {
          StatusVM?.Refresh(mn.Mqtt);
          //Dispatcher.UIThread.InvokeAsync(() => ConfigVM.Refresh(mo.Mqtt, false));
        }
      }
      PluginVM?.ToDo(e.MowIdx);
    }

    public async void MainWindow_Opened(object? sender, System.EventArgs e) {
      string dir = AppDomain.CurrentDomain.BaseDirectory;
      Stopwatch sw = Stopwatch.StartNew();

      Trace.TraceInformation($"Main.Open Beg => {sw.ElapsedMilliseconds}");
      client.RecvMqtt += Client_RecvMqtt;
      PluginVM?.Init(Config.Plugins);
      Trace.TraceInformation($"Main.Open Pgn => {sw.ElapsedMilliseconds}");

      if(File.Exists(PositecApi.TokenFile) && UsrApi != null) {
        string api = UsrApi[..2];

        if(await client.Access(api) && client.Mowers.Count > 0) {
          Trace.TraceInformation($"Main.Open Acc => {sw.ElapsedMilliseconds}");
          if(await StartMqtt()) {
            int mi = 0;

            if(Config.Midx != null && 0 <= Config.Midx && Config.Midx < client.Mowers.Count) mi = (int)Config.Midx;
            await client.GetStatus(mi);
            MowIdx = mi;
            Trace.TraceInformation($"Main.Open Con => {sw.ElapsedMilliseconds}");
          }
        }
      } else {
        Button? b = AppMainWindow?.FindControl<Button>("BtnAcc");

        b?.Flyout?.ShowAt(b);
      }
      Splash = false; this.RaisePropertyChanged(nameof (Splash));

      Trace.TraceInformation($"Main.Open End => {sw.ElapsedMilliseconds}");
    }

    public void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
      CfgJson cfg = new();

      if( sender is MainWindow mw && mw.WindowState == WindowState.Normal ) {
        cfg.Frame = new() { X = mw.Position.X, Y = mw.Position.Y, W = mw.Width, H = mw.Height };
      }
      cfg.Uuid = Uuid;
      if( !string.IsNullOrEmpty(UsrApi) ) cfg.Api = UsrApi[..2];
      cfg.Mail = UsrMail;
      cfg.Midx = MowIdx;
      PluginVM?.Fini(out cfg.Plugins);
      DeskApp.PutJson(GetConfigFile(), cfg);
    }

    public void MainWindow_Closed(object? sender, System.EventArgs e) {
      client.RecvMqtt -= Client_RecvMqtt;
    }
    #endregion

    #region Functions
    private async Task<bool> StartMqtt() {
      bool b = await client.Start();

      if( b ) {
        MowNames = [];
        foreach( MowerBase mb in client.Mowers ) {
          if( mb.Product.Name != null ) MowNames.Add(mb.Product.Name);
        }
        this.RaisePropertyChanged(nameof(MowNames));
        if( StatusVM != null ) StatusVM.CanPoll = client.Connected;
      }
      return b;
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

    public bool CanSend => client.Connected;
    public void MqttCmd() {
      if( !string.IsNullOrEmpty(MqttIn) ) {
        int p = MqttIn.IndexOf(" - ");
        string s = p > 0 ? MqttIn[..p] : MqttIn;

        client.Publish(s, _MowIdx);
      }
    }
    #endregion

    #region Tab Act
    public class ActEntryVM {
      public string Stamp { get; private set; }
      public string State { get; private set; }
      //public string Error { get; private set; }
      public string Charge { get; private set; }
      public IBrush Color { get; private set; }

      public ActEntryVM(ActivityEntry ae) {
        ActivityData d = ae.Payload.Dat;

        Stamp = DateTime.Parse(ae.Stamp).ToLocalTime().ToString(); // ae.Payload.Cfg.Date + " " + ae.Payload.Cfg.Time;
        if( d.LastError == ErrorCode.NONE ) {
          State = d.LastState.ToString();
          Color = d.LastState switch {
            StatusCode.GRASS_CUTTING or StatusCode.BORDER_CUT => Brushes.Lime,
            StatusCode.IDLE => Brushes.LightPink,
            _ => Brushes.White,
          };
        } else if( d.LastError == ErrorCode.RAINING ) {
          State = $"{d.LastState} {d.LastError}";
          Color = Brushes.Aqua;
        } else {
          State = $"{d.LastError}";
          Color = Brushes.LightCoral;
        }
        Charge = d.Battery.Charging == ChargeCoge.CHARGING ? "+" : "-";
        //li.SubItems.Add(a.Payload.Dat.Battery.Maintenance.ToString());
        //li.ToolTipText = a.Stamp;
      }
    }
    public ObservableCollection<ActEntryVM> Activities { get; private set; }

    public async Task ActCmdCall() {
      Activities.Clear();
      if( client != null && await client.CheckToken() && 0 <= MowIdx && MowIdx <= client.Mowers.Count ) {
        foreach( ActivityEntry ae in await client.GetActivities(MowIdx) ) Activities.Add(new ActEntryVM(ae));
      }
      this.RaisePropertyChanged(nameof(Activities));
    }

    public async void ActCmdJson() {
      if( client != null && 0 <= MowIdx && MowIdx <= client.Mowers.Count ) {
        string name = $"ActLog_{client.Mowers[MowIdx].Product.Name}.json";
        string path = Path.Combine(PositecApi.DirTrace, name);

        if( !File.Exists(path) ) await ActCmdCall();
        using Process editor = new();
        editor.StartInfo.FileName = path;
        editor.StartInfo.UseShellExecute = true;
        editor.Start();
      }
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

      await msgbox.ShowWindowDialogAsync(AppMainWindow);
    }
  }
}