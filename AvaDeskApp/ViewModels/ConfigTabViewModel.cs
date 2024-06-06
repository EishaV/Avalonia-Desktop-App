using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ReactiveUI;

using Positec;
using CSScripting;

namespace AvaApp.ViewModels {
  public class ConfigTabViewModel : ViewModelBase {
    public class StartTime : ValidationAttribute {
      public override bool IsValid(object? value) {
        if( TimeSpan.TryParseExact((string?)value, "%h\\:%m", CultureInfo.InvariantCulture, out _) ) return true;
        return false;
      }
    }

    public class SchedulerEntry : ViewModelBase {
      public string Wday { get; private set; }
      public bool Edge { get; set; }

      [NotifyParentProperty(true), Required, StartTime(ErrorMessage = "=> hh:mm")]
      public string Beg { 
        get { return _Beg; }
        set { 
          if( _Beg != value ) {
            this.RaiseAndSetIfChanged(ref _Beg, value);
            CalcEnd();
            Instance.RaisePropertyChanged(nameof(CanSave));
          }
        }
      } string _Beg;

      [NotifyParentProperty(true), Required, Range(0, 1200, ErrorMessage = "=> {1} - {2}")] // 20h
      public string Min {
        get { return _Min; }
        set {
          if( _Min != value ) {
            this.RaiseAndSetIfChanged(ref _Min, value);
            CalcEnd();
            Instance.RaisePropertyChanged(nameof(CanSave));
          }
        }
      } string _Min;
      public string? End { get; private set; }

      public SchedulerEntry(string wday, bool edge, string? beg, string? min) {
        Wday = wday;
        Edge = edge;
        _Beg = beg ?? "00:00";
        _Min = min ?? "0";
        CalcEnd();
      }

      private bool Validate() {
        ValidationContext context = new (this, serviceProvider: null, items: null);
        List<ValidationResult> results = [];

        return Validator.TryValidateObject(this, context, results, true);
      }
      public bool IsValid => Validate();

      public void CalcEnd() {
        int perc = Instance != null ? Instance.ScPerc : 0;
        TimeSpan end;

        try {
          //TimeSpan beg = Beg; // TimeSpan.Parse(Beg.Replace("_", ""));
          int min = int.Parse(Min);

          end = TimeSpan.Parse(Beg) + TimeSpan.FromMinutes(min + min * perc / 100.0);
        } catch {
          end = TimeSpan.Zero;
        }
        End = end.ToString("hh\\:mm");
        this.RaisePropertyChanged(nameof(End));
      }
    }

    public class ZoneEntry : ViewModelBase {
      public int Start { get; private set; }
      public ObservableCollection<bool> Mz { get; set; }
      public int Sum { get; private set; }

      public ZoneEntry(int start, ObservableCollection<bool> mz) {
        Start = start;
        Mz = mz;
        Mz.CollectionChanged += MzChanged;
        ReSum();
      }

      private void MzChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        ReSum();
      }

      public void ReSum() {
        int p = 0;

        foreach( bool b in Mz ) { p += b ? 10 : 0; }
        Sum = p;
        this.RaisePropertyChanged(nameof(Sum));
      }
    }

    public ObservableCollection<SchedulerEntry> SchedulerD { get; set; }
    public bool DoubleSc { get; private set; }
    public int ScMode { get; set; }
    public int ScPerc { 
      get { return _ScPerc; }
      set {
        this.RaiseAndSetIfChanged(ref _ScPerc, value);
        SchedulerD.ForEach(d => d.CalcEnd());
      }
    } int _ScPerc;
    public void CmdScPerc(object para) {
      if( para is string s ) {
        ScPerc = int.Parse(s);
        //this.RaisePropertyChanged(nameof(ScPerc));
      }
    }
    public ObservableCollection<ZoneEntry> MultiZone { get; set; }

    public ObservableCollection<IBrush> ZoneBack { get; private set; }

    public int Rain { get; set; }
    public int Torque { get; set; }
    public bool HasTq { get; private set; }

    private static readonly ConfigTabViewModel _Instance;
    public static ConfigTabViewModel Instance => _Instance;

    static ConfigTabViewModel() {
      _Instance = new ConfigTabViewModel();
    }
    public ConfigTabViewModel() {
      SchedulerD = [
        new("Mo", true, "10:00", "180"),
        new("Di", false, "10:00", "180"),
        new("Mi", true, "10:00", "180"),
        new("Do", false, "10:00", "180"),
        new("Fr", true, "10:00", "180"),
        new("Sa", false, "10:00", "0"),
        new("So", false, "10:00", "0")
      ];
      DoubleSc = true;
      MultiZone = [
        new ZoneEntry(10, [ true, false, true, false, true, false, true, false, true, false ]),
        new ZoneEntry(100, [ false, true, false, true, false, true, false, true, false, true ]),
        new ZoneEntry(0, [ false, false, false, false, false, false, false, false, false, false ]),
        new ZoneEntry(0, [ false, false, false, false, false, false, false, false, false, false ])
      ];
      ZoneBack = [];
    }

    private int _id = -1;
    public async void Refresh(MqttP0? m, bool b) {
      if( m != null ) {
        ConfigP0 c = m.Cfg;

        if(b || (c.Id > 1 && c.Id != _id) ) {
          ScMode = c.Schedule.Mode; this.RaisePropertyChanged(nameof(ScMode));
          ScPerc = c.Schedule.Perc; this.RaisePropertyChanged(nameof(ScPerc)); // vor End wegen Calc
          DoubleSc = c.Schedule.DDays != null && c.Schedule.DDays.Count == 7;
          SchedulerD.Clear();
          if(c.Schedule.Days != null && c.Schedule.Days.Count == 7) {
            for(int i = 1; i < 8; i++) { // Mo - So
              List<object> obj = c.Schedule.Days[i % 7];
              string dow = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedDayName((DayOfWeek)(i % 7));

              SchedulerD.Add(new SchedulerEntry(dow, Convert.ToBoolean(obj[2]), Convert.ToString(obj[0]), Convert.ToString(obj[1])));
              if( c.Schedule.DDays != null && c.Schedule.DDays.Count == 7 ) {
                obj = c.Schedule.DDays[i % 7];

                SchedulerD.Add(new SchedulerEntry("", obj[2].ToString() == "1", obj[0].ToString(), obj[1].ToString()));
              }
            }
          }
          MultiZone.Clear();
          if(c.MultiZones != null && c.MultiZones.Length == 4 && c.MultiZonePercs != null && c.MultiZonePercs.Length == 10) {
            for(int i = 0; i < 4; i++) {
              ObservableCollection<bool> mzv = [];

              foreach(var x in c.MultiZonePercs) mzv.Add(x == i);
              MultiZone.Add(new ZoneEntry(c.MultiZones[i], mzv));
            }
            ZoneBack.Clear();
            for(int i = 0; i < 10; i++) ZoneBack.Add(i == m.Dat.LastZone ? Brushes.White : Brushes.Transparent);
          }
          Rain = c.RainDelay; this.RaisePropertyChanged(nameof(Rain));
          HasTq = c.Torque != null; this.RaisePropertyChanged(nameof(HasTq));
          Torque = c.Torque ?? 0; this.RaisePropertyChanged(nameof(Torque));
        } else {
          if(m.Cfg.Id == _id) {
            var msgbox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard("", "Update Plan ist erfolgt");

            if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
              await msgbox.ShowWindowDialogAsync(desktop.MainWindow);
            }
          }
        }
      }
    }
    public bool CanSave {
      get {
        MainWindowViewModel mw = MainWindowViewModel.Instance;
        bool b = true;

        SchedulerD.ToList().ForEach(se => b = b && se.IsValid);
        return b && mw.Online;
      }
    }

    [DataContract]
    public struct Cfg {
      [DataMember] public int id;
      [DataMember] public Schedule sc;
      [DataMember] public int[] mz;
      [DataMember] public int[] mzv;
      [DataMember] public int rd;
      [DataMember(EmitDefaultValue = false)] public int? tq;
    }

    public async void CmdSave() {
      Cfg cfg = new ();
      string json;
      DateTime dt = DateTime.Now;

      _id = dt.Hour * 60 * 30 + dt.Minute * 30 + dt.Second % 2;
      cfg.id = _id;
      cfg.sc.Days = [];
      for( int i = 6; i < 13; i++ ) {
        try {
          int idx = (i % 7) * (DoubleSc ? 2 : 1);
          TimeSpan beg = TimeSpan.Parse(SchedulerD[idx].Beg);
          int min = int.Parse(SchedulerD[idx].Min);
          List<object> lso = [ beg.ToString("hh\\:mm"), min, SchedulerD[idx].Edge ? 1 : 0 ];

          cfg.sc.Days.Add(lso);
        } catch {
          cfg.sc.Days.Add([ "00:00", 0, 0 ]);
        }
      }
      if( DoubleSc ) {
        cfg.sc.DDays = [];
        for( int i = 6; i < 13; i++ ) {
          try {
            int idx = (i % 7) * 2 + 1;
            TimeSpan beg = TimeSpan.Parse(SchedulerD[idx].Beg);
            int min = int.Parse(SchedulerD[idx].Min);
            List<object> lso = [ beg.ToString("hh\\:mm"), min, SchedulerD[idx].Edge ? 1 : 0 ];

            cfg.sc.DDays.Add(lso);
          } catch {
            cfg.sc.DDays.Add([ "00:00", 0, 0 ]);
          }
        }
      }
      cfg.sc.Mode = ScMode;
      cfg.sc.Perc = ScPerc;
      cfg.mz = new int[4];
      cfg.mzv = new int[10];
      for( int i = 0; i < 4; i++ ) {
        cfg.mz[i] = MultiZone[i].Start;
        for(int j = 0; j < 10; j++) if(MultiZone[i].Mz[j]) cfg.mzv[j] = i;
      }
      cfg.rd = Rain;
      if( HasTq ) cfg.tq = Torque;
      json = PositecApi.FormatJson(Json.Write(cfg));
      var msgpar = new MessageBoxStandardParams { ButtonDefinitions = ButtonEnum.OkAbort, Icon = Icon.Info, CanResize = true, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                                  ContentTitle = "Avalonia MessageBox", ContentHeader ="Check json", ContentMessage = json,
                                                  SystemDecorations = SystemDecorations.BorderOnly };
      var msgbox = MessageBoxManager.GetMessageBoxStandard(msgpar);
      if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
        ButtonResult br = await msgbox.ShowWindowDialogAsync(desktop.MainWindow);

        if( br == ButtonResult.Ok ) {
          MainWindowViewModel.Instance.Publish(json);
        }
      }
    }
  }
}