using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Positec {
  #region Enums
  public enum ErrorCode : int {
    UNK = -1,
    NONE = 0,
    TRAPPED = 1,
    LIFTED = 2,
    WIRE_MISSING = 3,
    OUTSIDE_WIRE = 4,
    RAINING = 5,
    CLOSE_DOOR_TO_CUT_GRASS = 6,
    CLOSE_DOOR_GO_HOME = 7,
    MOTOR_BLADE_FAULT = 8,
    MOTOR_WHEELS_FAULT = 9,
    TRAPPED_TIMEOUT_FAULT = 10,
    UPSIDE_DOWN = 11,
    BATTERY_LOW = 12,
    REVERSE_WIRE = 13,
    BATTERY_CHARGE = 14,
    FIND_TIMEOUT = 15,
    LOCK = 16,
    BATTERY_TEMP = 17,
    DUMMY_MODEL = 18,
    BATTERY_TRUNK = 19,
    WIRE_SYNC = 20,
    NUM = 21,
    RTK_Charging_station_docking = 100,
    RTK_HBI = 101,
    RTK_OTA = 102,
    RTK_Map = 103,
    RTK_Excessive_slope = 104,
    RTK_Unreachable_zone = 105,
    RTK_Unreachable_chargingstation = 106,
    RTK_Insufficient_sensor_data = 108,
    RTK_Training_start_disallowed = 109,
    VISION_CAMERA = 110,
    VISION_mapping_exploration_required = 111,
    VISION_mapping_exploration_failed = 112,
    VISION_RFID_reader_error = 113,
    VISION_Headlight_error = 114,
    RTK_Missing_charging_station = 115,
    RV_Blade_height_adjustment_blocked = 116
  }
  public enum StatusCode : int {
    UNK = -1,
    IDLE = 0,
    HOME = 1,
    START_SEQUENCE = 2,
    LEAVE_HOUSE = 3,
    FOLLOW_WIRE = 4,
    SEARCHING_HOME = 5,
    SEARCHING_WIRE = 6,
    GRASS_CUTTING = 7,
    LIFT_RECOVERY = 8,
    TRAPPED_RECOVERY = 9,
    BLADE_BLOCKED_RECOVERY = 10,
    DEBUG = 11,
    REMOTE_CONTROL = 12,
    OFF_LIMITS_ESCAPE = 13,
    GOING_HOME = 30,
    AREA_TRAINING = 31,
    BORDER_CUT = 32,
    AREA_SEARCH = 33,
    PAUSE = 34,
    RTK_MOVE_TO_ZONE = 103, // Moving to zone - The mower is reaching a zone without cutting
    RTK_GOING_HOME = 104,   // Going home - The mower is returning to the charging station
    VISION_border_crossing = 110,
    VISION_exploring_lawn = 111
  }
  public enum ChargeCoge : int {
    CHARGED = 0,
    CHARGING = 1,
    ERROR_CHARGING = 2
  }
  public enum Command : int {
    PING = 0,
    START = 1,
    STOP = 2,
    REQ = 3,
    ZONE_SEARCH_REQ = 4,
    LOCK = 5,
    UNLOCK = 6,
    RESET_LOG = 7,
    PAUSE_OVER_WIRE,
    SAFE_HOMING
  }
  #endregion

  [DataContract]
  public struct ApiEntry {
    [DataMember(Name = "Login")]  public string? Login;
    [DataMember(Name = "WebApi")] public string? WebApi;
    [DataMember(Name = "CliId")] public string? ClientId;
  }

  #region AWS IoT
  /*
  cfg":{
    "id":3,"lg":"de",
    "tm":"15:34:31","dt":"14/05/2023",
    "sc":{"m":1,"distm":0,
       "ots":{"bc":0,"wtm":0},"p":50,
       "d":[["14:30",150,0],...,["14:30",150,0]],
       "dd":[["00:00",0,0],...,["00:00",0,0]]},
    "cmd":0,
    "mz":[0,0,0,0],"mzv":[0,0,0,0,0,0,0,0,0,0],
    "mzk":1,
    "rd":60,
    "sn":"...",
    "al":{"lvl":0,"t":60},
    "tq":0,
    "modules":{"US":{"enabled":1}}},
  "dat":{
    "mac":"...",
    "fw":3.29,"fwb":88,
    "bt":{"t":19.2,"v":17.85,"p":25,"nr":199,"c":1,"m":0},
    "dmp":[0.7,1.1,315.3],
    "st":{"b":22234,"d":428668,"wt":23059,"bl":95},
    "ls":1,"le":5,"lz":0,
    "rsi":0,"lk":0,
    "act":1,"tr":0,
    "conn":"wifi",
    "rain":{"s":1,"cnt":60},"time":{"r":131,"l":131},
    "modules":{"US":{"stat":"ok"}}}
  */
  [DataContract]
  public struct OneTimeScheduler {
    [DataMember(Name = "bc")] public int BorderCut; // cmommand for border cut
    [DataMember(Name = "wtm")] public int WorkTime; // working time in minutes
  }

  [DataContract]
  public struct Schedule {
    [DataMember(Name = "m")] public int Mode;
    [DataMember(Name = "distm", EmitDefaultValue = false)] public int? Party;
    [DataMember(Name = "ots", EmitDefaultValue = false)] public OneTimeScheduler? Ots;
    [DataMember(Name = "p")] public int Perc; // override from -100% to +100%, 0% is normal
    [DataMember(Name = "d")] public List<List<object>> Days;
    [DataMember(Name = "dd", EmitDefaultValue = false)] public List<List<object>> DDays;
  }

  [DataContract]
  public class ModuleConfig {
    [DataMember(Name = "enabled")] public int Enabled; // config of module
  }

  [DataContract]
  public class ModuleConfigDF { // config of module OLM
    [DataMember(Name = "cut")] public int Cutting;
    [DataMember(Name = "fh")] public int FastHome;
  }

  [DataContract]
  public class ModuleConfigs {
    [DataMember(Name = "US")] public ModuleConfig? US; // config of module ACS
    [DataMember(Name = "4G")] public ModuleConfig? G4; // config of module FML
    [DataMember(Name = "DF")] public ModuleConfigDF? DF; // config of module OML
  }

  [DataContract]
  public class AutoLock {
    [DataMember(Name = "lvl")] public int Level;
    [DataMember(Name = "t")] public int Time;
  }

  [DataContract]
  public struct ConfigP0 {
    [DataMember(Name = "id", EmitDefaultValue = false)] public ushort? Id;
    [DataMember(Name = "lg")] public string? Language; // always it :-(
    [DataMember(Name = "tm")] public string? Time;
    [DataMember(Name = "dt")] public string? Date;
    [DataMember(Name = "sc")] public Schedule Schedule;
    [DataMember(Name = "cmd")] public Command Cmd;
    [DataMember(Name = "mz")] public int[] MultiZones; // [0-3] start point in meters
    [DataMember(Name = "mzv")] public int[] MultiZonePercs; // [0-9] ring list of start indizes
    [DataMember(Name = "mzk", EmitDefaultValue = false)] public int? MultiZoneKeeper; // 0/1
    [DataMember(Name = "rd")] public int RainDelay;
    [DataMember(Name = "sn")] public string? SerialNo;
    [DataMember(Name = "al", EmitDefaultValue = false)] public AutoLock AutoLock;
    [DataMember(Name = "tq", EmitDefaultValue = false)] public int? Torque;
    [DataMember(Name = "modules", EmitDefaultValue = false)] public ModuleConfigs ModulesC;
  }

  [DataContract]
  public struct ConfigP1 {
    [DataMember(Name = "id", EmitDefaultValue = false)] public ushort? Id;
    [DataMember(Name = "lg")] public string? Language;
    [DataMember(Name = "cmd")] public Command Cmd;
    [DataMember(Name = "rd")] public int RainDelay;
    [DataMember(Name = "al", EmitDefaultValue = false)] public AutoLock AutoLock;
    [DataMember(Name = "tq", EmitDefaultValue = false)] public int? Torque;
  }

  [DataContract]
  public struct Battery {
    [DataMember(Name = "t")] public float Temp;
    [DataMember(Name = "v")] public float Volt;
    [DataMember(Name = "p")] public float Perc;
    [DataMember(Name = "nr")] public int Cycle;
    [DataMember(Name = "c")] public ChargeCoge Charging;
    [DataMember(Name = "m")] public int Maintenance;
  }

  [DataContract]
  public struct Statistic {
    [DataMember(Name = "b")] public int Blade; // total runtime with blade on in minutes
    [DataMember(Name = "d")] public int Distance; // total distance in meters
    [DataMember(Name = "wt")] public int WorkTime; // total worktim in minutes
    [DataMember(Name = "bl")] public int BorderLen; // length of border wire
  }

  [DataContract]
  public struct Rain {
    [DataMember(Name = "s")] public int State; // state of sensor
    [DataMember(Name = "cnt")] public int Counter; // delay counter
  }

  [DataContract]
  public struct ModuleState {
    [DataMember(Name = "stat")] public string? State; // state of module
  }

  [DataContract]
  public class ModuleStates {
    [DataMember(Name = "US")] public ModuleState? US; // state of module ACS
    [DataMember(Name = "DF")] public ModuleState? DF; // state of module OLM
    [DataMember(Name = "RL")] public ModuleState? RL; // state of module RLM
    [DataMember(Name = "4G")] public ModuleState? G4; // state of module FML
  }

  [DataContract]
  public class DataBase {
    [DataMember(Name = "bt")] public Battery Battery;
    [DataMember(Name = "dmp")] public float[] Orient = [0, 0, 0]; // 0-pitch, 1-roll, 2-yaw
    [DataMember(Name = "st")] public Statistic Statistic;
    [DataMember(Name = "ls")] public StatusCode LastState;
    [DataMember(Name = "le")] public ErrorCode LastError;
    [DataMember(Name = "rsi")] public int RecvSignal;
    [DataMember(Name = "act", EmitDefaultValue = false)] public int? Act;
    [DataMember(Name = "tr", EmitDefaultValue = false)] public int? Tr;
    [DataMember(Name = "conn", EmitDefaultValue = false)] public string? Conn;
    [DataMember(Name = "rain", EmitDefaultValue = false)] public Rain Rain;
  }

  [DataContract]
  public class DataP0 : DataBase {
    [DataMember(Name = "mac")] public string? MacAdr;
    [DataMember(Name = "fw")] public double Firmware;
    [DataMember(Name = "fwb", EmitDefaultValue = false)] public int? Beta;
    [DataMember(Name = "lz")] public int LastZone;
    [DataMember(Name = "lk")] public int Lock;
    [DataMember(Name = "modules", EmitDefaultValue = false)] public ModuleStates? ModulesD;
  }

  [DataContract]
  public class DataP1 : DataBase {
    [DataMember(Name = "tm")] public string? Stamp = string.Empty;
    [DataMember(Name = "fw")] public string? Firmware = string.Empty;
  }

  [DataContract]
  public class MqttP0 {
    [DataMember(Name = "cfg")]
    public ConfigP0 Cfg;
    [DataMember(Name = "dat")]
    public DataP0 Dat = new();
  }

  [DataContract]
  public class MqttP1 {
    [DataMember(Name = "cfg")]
    public ConfigP1 Cfg;
    [DataMember(Name = "dat")]
    public DataP1 Dat = new();
  }
  #endregion

  #region Web API
  #region OAuth
  [DataContract]
  public class OError {
    [DataMember(Name = "error")] public string? Error; // "invalid_grant"
    [DataMember(Name = "error_description")] public string? Desc; // "The user credentials were incorrect."
    [DataMember(Name = "message")] public string? Message; // "The user credentials were incorrect."
  }

  [DataContract]
  public class OAuth {
    [DataMember(Name = "token_type")]
    public string? Type;
    [DataMember(Name = "expires_in")]
    public int Expires;
    [DataMember(Name = "access_token")]
    public string? Access;
    [DataMember(Name = "refresh_token")]
    public string? Refresh;
  }
  #endregion

  #region User me
  /* User me
   * "id":...,"user_type":"customer","push_notifications":true,
   * "mqtt_endpoint":"a1optpg91s0ydf-ats.iot.eu-west-1.amazonaws.com",
   * "created_at":"2017-03-27 18:23:00","updated_at":"2021-08-23 15:31:30"
  */
  [DataContract]
  public class UserMe {
    [DataMember(Name = "id")] public int Id;
    [DataMember(Name = "mqtt_endpoint")] public string? Endpoint;
  }
  #endregion

  #region Product item + Status
  /*  Product items: [{
   *  "id":...,"uuid":"...","product_id":..,"user_id":..,
   *  "serial_number":"...","mac_address":"...","name":"...","locked":false,
   *  "firmware_version":3.xy,"firmware_auto_upgrade":false,"push_notifications":true,"sim":null,
   *  "push_notifications_level":"warning","test":false,"iot_registered":true,"mqtt_registered":true,
   *  "pin_code":null,"registered_at":"2017-03-27 00:00:00",
   *  "online":true,"app_settings":null,"accessories":null,
   *  "features":{"chassis":"s_2017","display_type":"led","input_type":"keyboard_led","lock":true,
   *              "mqtt":true,"multi_zone":true,"multi_zone_percentage":true,"multi_zone_zones":4,
   *              "rain_delay":true,"unrestricted_mowing_time":true,"wifi_pairing":"smartlink"},
   *  "pending_radio_link_validation":null,
   *  "mqtt_endpoint":"iot.eu-west-1.worxlandroid.com",
   *  "mqtt_topics":{"command_in":"...","command_out":"..."},
   *  "warranty_registered":true,"purchased_at":"...","warranty_expires_at":"...",
   *  "setup_location":{"latitude":...,"longitude":...},
   *  "city":{"id":...,"country_id":276,"name":"...","latitude":...,"longitude":...,"created_at":"...","updated_at":"..."},
   *  "time_zone":"Europe\/Berlin","lawn_size":...,"lawn_perimeter":null,
   *  "auto_schedule_settings":{"boost":0,"exclusion_scheduler":{"days":[{"slots":[],"exclude_day":false},...,{"slots":[],"exclude_day":false}],"exclude_nights":true},
   *                            "grass_type":null,"irrigation":null,"nutrition":null,"soil_type":null},
   *  "auto_schedule":false,
   *  "distance_covered":2813588,"mower_work_time":180560,
   *  "blade_work_time":165998,"blade_work_time_reset":null,"blade_work_time_reset_at":null,
   *  "battery_charge_cycles":13912,"battery_charge_cycles_reset":null,"battery_charge_cycles_reset_at":null,
   *  "messages_in":1484,"messages_out":164756,"raw_messages_in":4616,"raw_messages_out":164756,
   *  "created_at":"...","updated_at":"..."},
   *  "last_status":{"timestamp":"2022-10-04 18:02:21","payload": ...
   *  ]
   *  
   *  "capabilities":["auto_lock","bluetooth_control","bluetooth_pairing","lock","mqtt","multi_zone","multi_zone_percentage",
   *                  "one_time_scheduler","pairing_smartconfig","pause_over_wire","rain_delay","rain_delay_start","safe_go_home",
   *                  "scheduler_two_slots","unrestricted_mowing_time"],
   *  "capabilities_available":["zone_keeper"],
   *  "features":{"auto_lock":3.25,"bluetooth_control":3.2,"bluetooth_pairing":true,"chassis":"m_4wheels_2019","display_type":"lcd",
   *              "input_type":"keyboard_arrow_keys","lock":true,"mqtt":true,"multi_zone":true,"multi_zone_percentage":true,"multi_zone_zones":4,
   *              "one_time_scheduler":3.15,"pause_over_wire":3.26,"rain_delay":true,"rain_delay_start":3.08,"safe_go_home":3.25,
   *              "scheduler_two_slots":3.15,"unrestricted_mowing_time":true,"wifi_pairing":"smartconfig"},
   *  "accessories":{"ultrasonic":true},
   *  "mqtt_endpoint":"iot.eu-west-1.kress-robotik.com","mqtt_topics":{"command_in":"KRM100\/F4CFA29EA028\/commandIn","command_out":"KRM100\/F4CFA29EA028\/commandOut"},
   *  "warranty_registered":true,"purchased_at":"2021-09-09 00:00:00","warranty_expires_at":"2024-09-09 00:00:00",
   *  "setup_location":{"latitude":50.5131303,"longitude":12.4183929},
   *  "city":{"id":2954602,"country_id":276,"name":"Auerbach","latitude":50.51667,"longitude":12.4,"created_at":"2018-05-09 19:53:52","updated_at":"2018-05-09 19:53:52"},
   *  "time_zone":"Europe\/Berlin","lawn_size":null,
   *  "lawn_perimeter":95,
   *  "auto_schedule_settings":{"boost":0,"exclusion_scheduler":{"days":[{"slots":[],"exclude_day":false}, ,"exclude_nights":true},"grass_type":null,"irrigation":null,"nutrition":null,"soil_type":null},
   *  "auto_schedule":false,
   *  "improvement":true,"diagnostic":true,
   *  "distance_covered":499693,"mower_work_time":27007,"blade_work_time":26045,
   *  "blade_work_time_reset":26045,"blade_work_time_reset_at":"2023-06-06 11:15:42",
   *  "battery_charge_cycles":229,"battery_charge_cycles_reset":0,"battery_charge_cycles_reset_at":null,
   *  "created_at":"2021-05-30 08:11:13","updated_at":"2023-05-16 16:06:51"}]

  */
  [DataContract]
  public struct MqttTopic {
    [DataMember(Name = "command_in")]  public string CmdIn { get; set; }
    [DataMember(Name = "command_out")] public string CmdOut { get; set; }
  }
  [DataContract]
  public class LastStatusOld {
    [DataMember(Name = "timestamp")]
    public string? TimeStamp;
    [DataMember(Name = "payload")]
    public MqttP0? PayLoad;
  }
  [DataContract]
  public class LastStatusNew {
    [DataMember(Name = "timestamp")]
    public string? TimeStamp;
    [DataMember(Name = "payload")]
    public MqttP1? PayLoad;
  }

  [DataContract]
  public class ProductItem {
    [DataMember(Name = "id")] public int Id { get; set; }
    [DataMember(Name = "uuid")] public string? Uuid { get; set; }
    [DataMember(Name = "user_id")] public int UserId { get; set; }
    [DataMember(Name = "product_id")] public int ProductId { get; set; }
    [DataMember(Name = "serial_number")] public string? SerialNo { get; set; }
    [DataMember(Name = "mac_address")] public string? MacAdr { get; set; }
    [DataMember(Name = "name")] public string? Name { get; set; }
    [DataMember(Name = "locked")] public bool Locked { get; set; }
    [DataMember(Name = "firmware_auto_upgrade")] public bool AutoUpgd { get; set; }
    [DataMember(Name = "mqtt_xendpoint")] public string? Endpoint { get; set; }
    [DataMember(Name = "mqtt_Xtopics")] public MqttTopic? Topic { get; set; }
    [DataMember(Name = "online")] public bool Online { get; set; }
    [DataMember(Name = "protocol")] public int Protocol { get; set; }
    [DataMember(Name = "capabilities")] public List<string>? Capas { get; set; }
    [DataMember(Name = "blade_work_time_reset")] public int? BladeReset { get; set; }
    [DataMember(Name =  "blade_work_time_reset_at")] public string? BladeResetAt { get; set; }
  }

  [DataContract]
  public class StatusOld {
    [DataMember(Name = "serial_number")] public string? SerialNo;
    [DataMember(Name = "name")] public string? Name;
    [DataMember(Name = "online")] public bool Online;
    [DataMember(Name = "protocol")] public int Protocol;
    [DataMember(Name = "blade_work_time_reset")] public int? BladeReset;
    [DataMember(Name = "last_status")] public LastStatusOld? Last;
  }
  [DataContract]
  public class StatusNew {
    [DataMember(Name = "serial_number")] public string? SerialNo;
    [DataMember(Name = "name")] public string? Name;
    [DataMember(Name = "online")] public bool Online;
    [DataMember(Name = "protocol")] public int Protocol;
    [DataMember(Name = "blade_work_time_reset")] public int? BladeReset;
    [DataMember(Name = "last_status")] public LastStatusNew? Last;
  }
  #endregion

  #region Activity log
  /*
  {
    "_id":"5d65fcd8241fa136e0551d1f",
    "timestamp":"2019-08-28 04:02:31",
    "product_item_id":12061,
    "payload":{
      "cfg":{"dt":"28/08/2019","tm":"06:02:23","mzv":[0,0,0,0,0,0,0,0,0,0],"mz":[0,0,0,0]},
      "dat":{"le":0,"ls":0,"fw":3.51,"lz":0,"lk":0,"bt":{"c":0,"m":1}}
    }
  }
  */
  [DataContract]
  public struct ActivityConfig {
    [DataMember(Name = "dt")] public string? Date;
    [DataMember(Name = "tm")] public string? Time;
    //[DataMember(Name = "mz")] public int[] MultiZones; // [0-3] start point in meters
    //[DataMember(Name = "mzv")] public int[] MultiZonePercs; // [0-9] ring list of start indizes
  }
  [DataContract]
  public struct ActivityBattery {
    [DataMember(Name = "c")] public ChargeCoge Charging;
    [DataMember(Name = "m")] public int Maintenance;
  }
  [DataContract]
  public struct ActivityData {
    [DataMember(Name = "le")] public ErrorCode LastError;
    [DataMember(Name = "ls")] public StatusCode LastState;
    //[DataMember(Name = "fw")] public double Firmware;
    [DataMember(Name = "lz")] public int LastZone;
    [DataMember(Name = "lk")] public int Lock;
    [DataMember(Name = "bt")] public ActivityBattery Battery;
  }
  [DataContract]
  public struct ActivityPayload {
    [DataMember(Name = "cfg")] public ActivityConfig Cfg;
    [DataMember(Name = "dat")] public ActivityData Dat;
  }
  [DataContract]
  public struct ActivityEntry {
    [DataMember(Name = "_id")] public string ActId;
    [DataMember(Name = "timestamp")] public string Stamp;
    [DataMember(Name = "product_item_id")] public string? MowId;
    [DataMember(Name = "payload")] public ActivityPayload Payload;
  }
  #endregion
  #endregion
}