using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;

using MsBox.Avalonia.Enums;

using Positec;

namespace Plugin {
  #region Interface
  public struct PluginData {
    public Version Version;
    public string Name;
    public int Index;
    public ConfigP0 Config;
    public DataP0 Data;
  }

  public delegate void DelegateVoid();
  public delegate void DelegateTrace(string msg);
  public delegate void DelegateSend(string msg, int idx);
  public delegate ButtonResult DelegateMsg(string msg);

  #region Plugin Parameter
  public enum ParaType { Text, Real, Case, Bool }

  public class PluginParaBase(ParaType type, string caption, string description) : INotifyPropertyChanged {
    public ParaType ParaType { get; private set; } = type;
    public string Caption { get; private set; } = caption;
    public string Description { get; private set; } = description;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }

  public class PluginParaText : PluginParaBase {
    public string Text {
      get { return _Text; }
      set { _Text = value; NotifyPropertyChanged(nameof(Text)); }
    } string _Text;
    public bool ReadOnly { get; set; }

    public PluginParaText(string caption, string text, string desc, bool ro = false) : base(ParaType.Text, caption, desc) {
      _Text = text;
      ReadOnly = ro;
      NotifyPropertyChanged(nameof(ReadOnly));
    }
  }

  public class PluginParaCase : PluginParaBase {
    public List<string> Items { get; private set; }
    public int Index { 
      get { return _Index; }
      set { _Index = value; NotifyPropertyChanged(nameof(Index)); }
    } int _Index;

    public PluginParaCase(string caption, int index, List<string> items, string desc) : base(ParaType.Case, caption, desc) {
      Items = items; NotifyPropertyChanged(nameof(Items));
      Index = index; NotifyPropertyChanged(nameof(Index));
    }
  }

  public class PluginParaReal : PluginParaBase {
    public double Real {
      get { return _Real; }
      set { _Real = value; NotifyPropertyChanged(nameof(Real)); }
    } double _Real;

    public double Min { get; set; }
    public double Max { get; set; }

    public PluginParaReal(string caption, double real, string desc, double min = Double.MinValue, double max = Double.MaxValue) : base(ParaType.Real, caption, desc) {
      Real = real;
      Min = min;
      Max = max;
      //NotifyPropertyChanged(nameof(Real));
    }
  }

  public class PluginParaBool : PluginParaBase {
    public bool Check {
      get { return _Check; }
      set { _Check = value; NotifyPropertyChanged(nameof(Check)); }
    } bool _Check;

    public PluginParaBool(string caption, bool check, string desc) : base(ParaType.Bool, caption, desc) {
      Check = check;
      NotifyPropertyChanged(nameof(Check));
    }
  }
  #endregion

  public interface IPlugin : IDisposable {
    public List<PluginParaBase> Paras { get; }
    string Desc { get; }
    void Doit(PluginData pd);
    void Todo(PluginData pd);
  }
  #endregion

  #region Static DeskApp
  public static class DeskApp {
    public static string DirData => PositecApi.DirData;
    public static string DirPlugin => Path.Combine(AppContext.BaseDirectory, "Plugins");

    public static DelegateSend? SendDelegate { get; set; }
    public static DelegateTrace? TraceDelegate { get; set; }


    public static void Send(string mqtt, int idx) { SendDelegate?.Invoke(mqtt, idx); }
    public static void Trace(string text) { TraceDelegate?.Invoke(text); }

    public static T? GetJson<T>(string name) where T : class, new() {
      if( File.Exists(name) ) {
        try {
          using FileStream fs = new(name, FileMode.Open);
          DataContractJsonSerializer dcjs = new(typeof(T));
          return dcjs.ReadObject(fs) as T;
        } catch(Exception ex) {
          TraceDelegate?.Invoke($"GetJson({name}) => {ex}");
        }
      }
      return null;
    }

    public static void PutJson<T>(string name, T json) where T : class {
      DataContractJsonSerializer dcjs = new(typeof(T));
      FileStream fs = new(name, FileMode.Create);

      dcjs.WriteObject(fs, json);
      fs.Close();
    }
  }
  #endregion
}
