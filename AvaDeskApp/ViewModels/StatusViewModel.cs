﻿using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using ReactiveUI;

using Positec;
using AvaApp.Texts;

namespace AvaApp.ViewModels {
  public class StatusViewModel : ReactiveObject {
    const string AvaAss = "avares://AvaDeskApp/Assets";

    static MainWindowViewModel Main => MainWindowViewModel.Instance;

    public string Error { get; set; } = string.Empty;
    public IBrush ErrorColor { get; set; } = Brushes.Red;
    public string State { get; set; } = string.Empty;
    public IBrush StateColor { get; set; } = Brushes.Orange;

    public string LastZone { get; private set; } = string.Empty;

    public float BatPerc { get; private set; }
    public float BatVolt { get; private set; }
    public float BatTemp { get; private set; }
    public int BatCycle { get; private set; }

    public double DmpPitch { get; private set; }
    public double DmpRoll { get; private set; }
    public double DmpYaw { get; private set; }

    private static string FormatTime(int t) {
      string f = t > 24 * 60 ? @"d\d\ h\h\ m\m" : @"h\h\ m\m";
      return TimeSpan.FromMinutes(t).ToString(f);
    }
    public float StatDist { get; private set; }
    public string StatWork { get; private set; } = string.Empty;
    public string StatBlade { get; private set; } = string.Empty;

    public string BladeCur { get; private set; } = string.Empty;
    public string BladeAt { get; private set; } = string.Empty;

    public DateTime Stamp { get; private set; }

    public string? Firmware { get; private set; }

    private ProductItem? _pi = null;

    private IImage? _WebPic;
    public IImage? WebPic {
      get => _WebPic;
      set => this.RaiseAndSetIfChanged(ref _WebPic, value);
    }

    public IImage? RsiPic { get; private set; }

    private void RefreshBase(DataBase d) {
      string? bra = _pi?.BladeResetAt;
      bool bmt = _pi?.Endpoint != null && _pi?.Topic != null;

      Error = GetError(d.LastError); //this.RaisePropertyChanged(nameof(Error));
      ErrorColor = d.LastError == ErrorCode.RAINING ? Brushes.Aqua : Brushes.Red;
      State = GetState(d.LastState, d.Battery.Charging, out ISolidColorBrush fc);
      StateColor = fc;
      if( d.Conn == "BLE" ) {
        var asset = AssetLoader.Open(new Uri($"{AvaAss}/bluetooth.png"));
        RsiPic = new Bitmap(asset);
      } else SetRssiPic(d.RecvSignal);
      if( d.Orient != null && d.Orient.Length == 3 ) {
        DmpPitch = d.Orient[0]; DmpRoll = d.Orient[1]; DmpYaw = d.Orient[2];
      }
      BatPerc = d.Battery.Perc; BatVolt = d.Battery.Volt; BatTemp = d.Battery.Temp; BatCycle = d.Battery.Cycle;
      StatBlade = FormatTime(d.Statistic.Blade);
      BladeCur =  FormatTime(d.Statistic.Blade - _pi?.BladeReset ?? 0);
      if( bra != null ) {
        DateTime dt = DateTime.Parse(bra);

        BladeAt = dt.ToShortDateString();
      } else BladeAt = string.Empty;
      CanBlade = true;
      StatDist = d.Statistic.Distance; StatWork = FormatTime(d.Statistic.WorkTime);
      CanPoll = true;
      CanStart = bmt && (d.LastState == StatusCode.HOME || d.LastState == StatusCode.PAUSE);
      CanHome = bmt && (d.LastState == StatusCode.GRASS_CUTTING || d.LastState == StatusCode.PAUSE);
      CanStop = bmt && !(d.LastState == StatusCode.HOME || d.LastState == StatusCode.IDLE || d.LastState == StatusCode.PAUSE);
    }
    public void Refresh(object? o) {
      if( o is MqttP0 m && !string.IsNullOrEmpty(m.Cfg.Date) && !string.IsNullOrEmpty(m.Cfg.Time) ) {
        ConfigP0 c = m.Cfg;
        DataP0 d = m.Dat;
        string dts = $"{c.Date} {c.Time}"; // parsable DateTime string

        Stamp = DateTime.ParseExact(dts, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        RefreshBase(d);
        if( c.MultiZones != null && c.MultiZones[0] > 0 && c.MultiZonePercs != null ) {
          int lz = d.LastZone;

          if( 0 <= lz && lz < c.MultiZonePercs.Length ) LastZone = $"SP {c.MultiZonePercs[lz] + 1} [{lz}]";
          else LastZone = string.Empty;
        }
        Firmware =  d.Firmware.ToString("N2", CultureInfo.InvariantCulture) + (d.Beta != null ? $"b{d.Beta}" : string.Empty);

        CanMenu = _pi?.Endpoint != null && _pi?.Topic != null && c.Schedule.Ots != null && c.Schedule.Party != null;
        IsParty = c.Schedule.Mode == 2;
        if( IsParty ) State += " Party ∞";
        if( c.Schedule.Party > 0 ) State += $" Party {c.Schedule.Party}min";
        CanEdge = d.LastState == StatusCode.HOME;
        VisSafe = _pi?.Capas?.Contains("safe_go_home") ?? false;
        CanSafe = d.LastState == StatusCode.GRASS_CUTTING;
      } else if( o is MqttP1 mn && !string.IsNullOrEmpty(mn.Dat.Stamp) ) {
        DataP1 d = mn.Dat;

        Stamp = DateTime.Parse(d.Stamp);
        RefreshBase(d);
        LastZone = string.Empty;
        Firmware = d.Firmware;
      } else {
        Error = "null data"; ErrorColor = Brushes.Red;
        State = "unkwon"; StateColor = Brushes.Orange;
        Stamp = DateTime.Now;
      }
      foreach( PropertyInfo pi in this.GetType().GetProperties() ) this.RaisePropertyChanged(pi.Name);
    }

    public void SetRssiPic(int rs) {
      int idx;

      if( Math.Abs(rs) < 50 ) idx = 0;
      else if( Math.Abs(rs) < 60 ) idx = 1;
      else if( Math.Abs(rs) < 70 ) idx = 2;
      else idx = 3;

      var asset = AssetLoader.Open(new Uri($"{AvaAss}/rssi_{idx}.png"));
      RsiPic = new Bitmap(asset);
    }

    private void DefaultPic() {
      char c = Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? 'W' : 'B';

      Uri uri = new($"{AvaAss}/Mower{c}.webp");

      if( AssetLoader.Exists(uri) ) {
        var asset = AssetLoader.Open(uri);

        WebPic = new Bitmap(asset);
        asset.Close();
      }
    }
    public void UpdateProduct(string api, ProductItem pi) {
      string img = string.Empty;
      int pid = pi.ProductId;

      _pi = pi;
      switch( api[0] ) {
        case 'W':
          switch( pid ) {
            case 6: case 11: img = "WG757_WG796.webp"; break;
            case 19: case 20: img = "WG797_WG798.webp"; break;
            case 21: case 24: case 33: case 37: img = "WR101_WR105.webp"; break;
            case 22: case 23: case 34: case 36: img = "WR102_WR104.webp"; break;
            case 35: img = "WR103.webp"; break;
            case 25: case 32: case 38: img = "WR100_WR106.webp"; break;
            case 26: case 39: img = "WR110.webp"; break;
            case 28: case 29: case 31: img = "WR111_WR112_WR113.webp"; break;
            case 40: img = "WR115.webp"; break;
            case 48: case 79: img = "WR130.webp"; break; // S300
            case 49: case 90: case 91: img = "WR141.webp"; break; // M500, WR168 S250 / WR169 S300 => C2E
            case 50: case 51: img = "WR142_WR143.webp"; break; // M700, M1000
            case 62: case 66: img = "WR147.webp"; break; // L1000
            case 67: img = "WR148.webp"; break; // L800
            case 52: case 53: img = "WR153_WR155.webp"; break; // L1500, L2000
            case 69: case 70: case 80: img = "WR165_WR167.webp"; break; // M500+, M700+
            case 73: case 74: case 84: case 85: case 101: case 103: img = "WR206_WR208.webp"; break; // Vision M
            case 75: case 76: case 86: case 87: case 102: img = "WR213_WR216.webp"; break; // Vision L
          }
          break;
        case 'K':
          switch( pid ) {
            case 1: case 6: img = "Kress Nano.webp"; break;
            case 2: case 3: case 7: img = "Kress Miss.webp"; break;
            case 4: case 5: case 8: case 9: case 10: img = "Kress Miss OAS.webp"; break;
            case 11: case 12: img = "Kress Mega.webp"; break;
            case 13: case 32: case 34: case 35: img = "Kress RTK Miss.webp"; break;
            case 14: case 21: case 25: case 33:  img = "Kress RTK Miss OAS.webp"; break;
            case 19: case 23: case 37:  img = "Kress RTK Mega.webp"; break;
          }
        break;
        case 'L':
          switch( pid ) {
            case 6: img = "LX790i.webp"; break;
            case 8: img = "LX812i.webp"; break;
            case 11: img = "LX796i.webp"; break;
            case 13: img = "LX810i.webp"; break;
            case 20: img = "LX835i.webp"; break;
            case 26: img = "LX838i.webp"; break;
          }
        break;
        case 'S':
          switch( pid ) {
            case 1: img = "SM800.webp"; break;
          }
          break;
      }

      if( !string.IsNullOrEmpty(img) ) {
        Uri uri = new ($"{AvaAss}/{img}");

        if( AssetLoader.Exists(uri) ) {
          var asset = AssetLoader.Open(uri);
          WebPic = new Bitmap(asset);
        } else WebPic = null;
      } else DefaultPic();
    }

    private static string GetError(ErrorCode ec) {
      return ec switch {
        ErrorCode.NONE => string.Empty,
        ErrorCode.BATTERY_CHARGE => Ressource.Get("mower_error_battery_charge"),
        ErrorCode.BATTERY_LOW => Ressource.Get("mower_error_battery_low"),
        ErrorCode.BATTERY_TEMP => Ressource.Get("mower_error_battery_temp"),
        ErrorCode.BATTERY_TRUNK => Ressource.Get("mower_error_battery_trunk"),
        ErrorCode.FIND_TIMEOUT => Ressource.Get("mower_error_home_find_timeout"),
        ErrorCode.LIFTED => Ressource.Get("mower_error_lifted"),
        ErrorCode.LOCK => Ressource.Get("mower_error_locked"),
        ErrorCode.MOTOR_BLADE_FAULT => Ressource.Get("mower_error_blade_motor_fault"),
        ErrorCode.MOTOR_WHEELS_FAULT => Ressource.Get("mower_error_wheel_motor_fault"),
        ErrorCode.OUTSIDE_WIRE => Ressource.Get("mower_error_outside_wire"),
        ErrorCode.RAINING => Ressource.Get("mower_rain_delay"),
        ErrorCode.REVERSE_WIRE => Ressource.Get("mower_error_reverse_wire"),
        ErrorCode.TRAPPED => Ressource.Get("mower_trapped"),
        ErrorCode.TRAPPED_TIMEOUT_FAULT => Ressource.Get("mower_error_trapped_timeout_fault"),
        ErrorCode.UPSIDE_DOWN => Ressource.Get("mower_error_upside_down"),
        ErrorCode.WIRE_MISSING => Ressource.Get("mower_error_wire_missing"),
        ErrorCode.WIRE_SYNC => Ressource.Get("mower_error_wire_sync"),

        ErrorCode.RTK_Charging_station_docking => Ressource.Get("mower_error_charging_station_docking"),
        ErrorCode.RTK_Excessive_slope => Ressource.Get("mower_error_excessive_slope"),
        ErrorCode.RTK_HBI => Ressource.Get("mower_error_hbi_error"),
        ErrorCode.RTK_Insufficient_sensor_data => Ressource.Get("mower_error_insufficient_sensor_data"),
        ErrorCode.RTK_Map => Ressource.Get("mower_error_map_error"),
        ErrorCode.RTK_Missing_charging_station => Ressource.Get("mower_error_missing_charging_station"),
        ErrorCode.RTK_OTA => Ressource.Get("mower_error_ota_error"),
        ErrorCode.RTK_Training_start_disallowed => Ressource.Get("mower_error_training_start_disallowed"),
        ErrorCode.RTK_Unreachable_chargingstation => Ressource.Get("mower_error_unreachable_charging_station"),
        ErrorCode.RTK_Unreachable_zone => Ressource.Get("mower_error_unreachable_zone"),
        ErrorCode.RV_Blade_height_adjustment_blocked => Ressource.Get("mower_error_blade_height_adjustment"),

        ErrorCode.VISION_CAMERA => Ressource.Get("mower_error_camera_error"),
        ErrorCode.VISION_Headlight_error => Ressource.Get("mower_error_headlight_error"),
        ErrorCode.VISION_mapping_exploration_failed => Ressource.Get("mower_error_mapping_exploration_failed"),
        ErrorCode.VISION_mapping_exploration_required => Ressource.Get("mower_error_mapping_exploration_required"),
        ErrorCode.VISION_RFID_reader_error => Ressource.Get("mower_error_rfid_reader_error"),
        _ => $"Unknown le {ec}"
      };
    }
    private static string GetState(StatusCode sc, ChargeCoge cc, out ISolidColorBrush c) {
      bool b = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

      c = b ? Brushes.LightGray : Brushes.DimGray;
      switch( sc ) {
        case StatusCode.GRASS_CUTTING: c = b ? Brushes.LawnGreen : Brushes.DarkGreen; return Ressource.Get("mower_mowing");
        case StatusCode.HOME:
          if( cc == ChargeCoge.CHARGING ) { c = b ? Brushes.Cyan : Brushes.Navy; return Ressource.Get("mower_charging"); } else return Ressource.Get("mower_home");
        case StatusCode.IDLE: c = Brushes.Red; return Ressource.Get("mower_idle");
        case StatusCode.PAUSE: c = b ? Brushes.Magenta : Brushes.Purple; return Ressource.Get("mower_pause");

        case StatusCode.START_SEQUENCE:
        case StatusCode.LEAVE_HOUSE: return Ressource.Get("mower_leaving_home");

        case StatusCode.FOLLOW_WIRE:
        case StatusCode.SEARCHING_WIRE:
        case StatusCode.SEARCHING_HOME:
        case StatusCode.RTK_GOING_HOME:
        case StatusCode.GOING_HOME: return Ressource.Get("mower_going_home");
        case StatusCode.RTK_MOVE_TO_ZONE:
        case StatusCode.AREA_SEARCH: return Ressource.Get("mower_area_search");
        case StatusCode.AREA_TRAINING: return Ressource.Get("mower_area_training");
        case StatusCode.BORDER_CUT: return Ressource.Get("mower_border_cut");

        case StatusCode.LIFT_RECOVERY:
        case StatusCode.TRAPPED_RECOVERY:
        case StatusCode.BLADE_BLOCKED_RECOVERY: c = b ? Brushes.Orange : Brushes.DarkOrange; return Ressource.Get("mower_trapped");
        default: c = Brushes.Red; return $"Unknown ls {sc}";
      }
    }

    #region Commands
    public bool CanBlade { get; set; }
    public static async Task CmdBlade() {
      await Main.ResetBlade();
    }
    public bool CanPoll{
      get => _IsCmdPollEnabled;
      set => this.RaiseAndSetIfChanged(ref _IsCmdPollEnabled, value);
    }
    private bool _IsCmdPollEnabled;
    public void CmdPoll() {
      CanPoll = false;
      Main.Publish($"\"cmd\":{(int)Command.PING}");
    }

    public bool CanStart { get; set; }
    public void CmdStart() {
      CanPoll = false;
      Main.Publish($"\"cmd\":{(int)Command.START}");
    }
    public bool CanStop { get; set; }
    public void CmdStop() {
      CanPoll = false;
      Main.Publish($"\"cmd\":{(int)Command.STOP}");
    }
    public bool CanHome { get; set; }
    public void CmdHome() {
      CanPoll = false;
      Main.Publish($"\"cmd\":{(int)Command.HOME}");
    }
    #endregion

    #region Menu Button
    private static void HideMenu() {
      Control? m = MainWindowViewModel.AppMainWindow;
      UserControl? c = m?.FindControl<UserControl>("ViewStatus");
      Button? b = c?.FindControl<Button>("BtnMenu");

      b?.Flyout?.Hide();
    }
    public bool CanMenu { get; set; }
    public bool IsParty { get; set; }
    public void CmdParty() {
      HideMenu();
      string cmd = $"\"id\":2,\"sc\":{{\"m\":{(IsParty ? 2 : 1)},\"distm\":0}}"; // id:2 damit Cfg refresht wird

      MainWindowViewModel.Instance.Publish(cmd);
    }
    public bool CanEdge { get; set; }
    public void CmdEdge() {
      HideMenu();
      CanPoll = false;
      Main.Publish("\"sc\":{\"ots\":{\"bc\":1,\"wtm\":0}}");
    }
    public bool VisSafe { get; set; }
    public bool CanSafe { get; set; }
    public void CmdSafe() {
      HideMenu();
      CanPoll = false;
      Main.Publish($"\"cmd\":{(int)Command.SAFE_HOMING}");
    }
    #endregion
    public StatusViewModel() {
      DefaultPic();
      if( Design.IsDesignMode ) {
        BatPerc = 100.0F; BatVolt = 20.5F; BatTemp = 36.0F;
        DmpPitch = DmpRoll = DmpYaw = 17.3;
      }
    }
  }
}
