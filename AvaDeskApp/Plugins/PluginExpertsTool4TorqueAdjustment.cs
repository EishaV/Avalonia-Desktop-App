using System;
using System.IO;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Positec;
using Plugin;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia;

public class PluginExpertsTool4TorqueAdjustment : IPlugin {
  // Version identification
  /////////////////////////////////////////////////////		
  const string VERSION_NUMBER = "test";
  const string VERSION_DATE = "31.05.2023";
  const string PLUGIN_DESC = "Experts tool: Torque adjustment";
  const string PLUGIN_NAME = "ExpertsTool4TorqueAdjustment";
  /////////////////////////////////////////////////////		

  // Global variables
  /////////////////////////////////////////////////////	
  private List<PluginParaBase> MyParas;
  List<PluginParaBase> IPlugin.Paras => MyParas;
  string IPlugin.Desc { get { return DESC_PLUGIN; } }
  const int TORQUE_MIN = -51;
  const int TORQUE_MAX = 51;
  private ushort MyCfgId = 0;

  // Form Design
  /////////////////////////////////////////////////////		
  [DataContract]
  public class MySettings {
    [DataMember] public int NewTorque;

    public MySettings() {
      NewTorque = 0;
    }
  }

  public PluginParaText ParaCurrentTorque;
  public PluginParaReal ParaNewTorque;
  /////////////////////////////////////////////////////		  


  // Event: On Open
  /////////////////////////////////////////////////////		
  public PluginExpertsTool4TorqueAdjustment() {
    MySettings? ms = DeskApp.GetJson<MySettings>(JsonFile());

    if(ms == null) ms = new MySettings();
    ParaCurrentTorque = new PluginParaText(DISP_CURRENTTORQUE, "Wait for ToDo", DESC_CURRENTTORQUE, true);
    ParaNewTorque = new PluginParaReal(DISP_NEWTORQUE, ms.NewTorque, DESC_NEWTORQUE, TORQUE_MIN, TORQUE_MAX);
    MyParas = new List<PluginParaBase>() { ParaCurrentTorque, ParaNewTorque };
  }

  // Event: Neue MQTT Daten eingetroffen
  /////////////////////////////////////////////////////		
  async void IPlugin.Todo(PluginData pd) { // async wegen MessageBox
    ParaCurrentTorque.Text = $"{pd.Config.Torque}";
    if(MyCfgId > 0 && pd.Config.Id == MyCfgId) { // 0 ist Status-/Fehler-Meldung vom Mäher
      var msgbox = MessageBoxManager.GetMessageBoxStandardWindow("ExpTool - Torq", $"Wert bestätigt :-) {pd.Config.Torque}", ButtonEnum.Ok, Icon.Info, WindowStartupLocation.CenterOwner);

      if(Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
        await msgbox.Show(desktop.MainWindow);
      }
    }
  }

  // Event: Doit Button gedrückt
  /////////////////////////////////////////////////////	
  void IPlugin.Doit(PluginData pd) {
    if(TORQUE_MIN <= ParaNewTorque.Real && ParaNewTorque.Real <= TORQUE_MAX) {
      Random r = new();
      string Json;

      MyCfgId = (ushort)r.Next(1, ushort.MaxValue); // cfg.id ist UInt16, 0 net, weil Baum im Weg
      Json = $"{{\"id\":{MyCfgId},\"tq\":{ParaNewTorque.Real}}}";
      DeskApp.Send(Json, pd.Index);
      MyTrace(TRACE_SENT.Replace("@@JSON@@", Json).Replace("@@MOWER@@", pd.Name));
      // MessageBox.Show(MSG_SENT.Replace("@@JSON@@", Json).Replace("@@MOWER@@", pd.Name));
    } else { // darf eigentlich nimmer passieren
      // MessageBox.Show(MSG_ILLEGAL_NUMBER);
      MyTrace(MSG_ILLEGAL_NUMBER);
    }
  }

  // Event: On Close
  /////////////////////////////////////////////////////	
  void IDisposable.Dispose() {
    MySettings ms = new() { NewTorque = Convert.ToInt32(ParaNewTorque.Real) };

    DeskApp.PutJson<MySettings>(JsonFile(), ms);
  }

  // Writing a trace text 
  /////////////////////////////////////////////////////	
  private void MyTrace(string Text) {
    DeskApp.Trace(PLUGIN_NAME + ": " + Text);
  }

  // Data file name 
  /////////////////////////////////////////////////////	
  string DataFile(string Extension, string Supplement) {
    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PLUGIN_NAME + (Supplement == "" ? "" : Supplement) + Extension);
  }

  // File name for json File
  /////////////////////////////////////////////////////	
  string JsonFile() {
    return DataFile(".json", "");
  }

  // Texts
  /////////////////////////////////////////////////////
  const string DESC_PLUGIN = PLUGIN_DESC + " (v" + VERSION_NUMBER + " | " + VERSION_DATE + ")";
  static readonly string DESC_CURRENTTORQUE = $"Torque value as an integer percentage between {TORQUE_MIN} and {TORQUE_MAX}"
    + "\r\nExamples: -10 = standard torque minus 10% | 0 = standard torque | 10 = standard torque plus 10%";
  const string DESC_NEWTORQUE = "This plugin is a tool for experts."
    + " Only those who can accurately assess the consequences of a torque change should use the plugin."
    + " Improper use can cause damage to the lawn.";
  const string DISP_CURRENTTORQUE = "Current torque adjust";
  const string DISP_NEWTORQUE = "New torque adjust";
  static readonly string MSG_ILLEGAL_NUMBER = $"Only whole numbers between {TORQUE_MIN} and {TORQUE_MAX} are accepted.";
  const string MSG_SENT = "The following message has been sent to @@MOWER@@:\r\n@@JSON@@";
  const string TRACE_SENT = "Sent: @@JSON@@ to @@MOWER@@";
}
