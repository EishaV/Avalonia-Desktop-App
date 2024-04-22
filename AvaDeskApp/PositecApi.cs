using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

using ApiDic = System.Collections.Generic.Dictionary<string, Positec.ApiEntry>;
using Plugin;
using System.Reflection.Metadata;

namespace Positec {
  public delegate void ErrDelegte(string msg);

  internal class Logger(bool isEnabled) : IMqttNetLogger {
    public bool IsEnabled { get; } = isEnabled;
    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception) {
      string s;

      s = source + " ";
      if( parameters != null ) s += string.Format(message, parameters);
      else s += message;
      if( exception != null ) s = s + "\r\n" + exception.ToString();
      switch( logLevel ) {
        case MqttNetLogLevel.Info: Trace.TraceInformation(s); break;
        case MqttNetLogLevel.Warning: Trace.TraceWarning(s); break;
        case MqttNetLogLevel.Error: Trace.TraceError(s); break;
        case MqttNetLogLevel.Verbose: /*Debug.WriteLine(s);*/ break;
      }
    }
  }

  static class Json {
    public static string Write(object obj) {
      string str = string.Empty;

      if( obj != null ) {
        DataContractJsonSerializerSettings dcjss = new() { UseSimpleDictionaryFormat = true };
        DataContractJsonSerializer dcjs = new(obj.GetType(), dcjss);
        using MemoryStream ms = new();

        dcjs.WriteObject(ms, obj);
        str = Encoding.UTF8.GetString(ms.ToArray());
      }
      return str;
    }

    public static T? Read<T>(string str) where T : class, new() {
      try {
        DataContractJsonSerializerSettings dcjss = new() { UseSimpleDictionaryFormat = true };
        DataContractJsonSerializer dcjs = new(typeof(T), dcjss);
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(str));

        return dcjs.ReadObject(ms) as T;
      } catch( Exception ex ) {
        Trace.TraceError($"Json.ReadObject => {ex}");
      }
      return null;
    }
    public static T? Read<T>(byte[] ba) where T : class, new() {
      try {
        DataContractJsonSerializerSettings dcjss = new() { UseSimpleDictionaryFormat = true };
        DataContractJsonSerializer dcjs = new(typeof(T), dcjss);
        using MemoryStream ms = new(ba);

        return dcjs.ReadObject(ms) as T;
      } catch( Exception ex ) {
        Trace.TraceError($"Json.ReadObject => {ex}");
      }
      return null;
    }
    //public static T PutJson<T>(string str) where T : class {
    //  DataContractJsonSerializer dcjs = new(typeof(T));
    //  FileStream fs = new(name, FileMode.Create);

    //  dcjs.WriteObject(fs, json);
    //  fs.Close();
    //}
  }

  public class MowerBase {
    public ProductItem Product { get; set; } = new ProductItem();
    public string Json { get; set; } = string.Empty;
  }

  public class MowerP0 : MowerBase {
    public MqttP0 Mqtt { get; set; } = new MqttP0();
  }

  public class MowerP1 : MowerBase {
    public MqttP1 Mqtt { get; set; } = new MqttP1();
  }

  public class RecvEventArgs : EventArgs {
    public int MowIdx { get; set; }
  }
  public class PositecApi {
    private readonly HttpClient httpClient = new();
    private readonly ErrDelegte Err;

    public event EventHandler<RecvEventArgs>? RecvMqtt;

    private string UrlLgn { get; set; } = string.Empty;
    private string UrlApi { get; set; } = string.Empty;
    private string CliId { get; set; } = string.Empty;

    private OAuth _oAuth;
    private DateTime _tokDT;
    private readonly IMqttClient _mqtt;
    private MqttClientOptionsBuilder _mqttCOB;
    private string _api;
    private int _reTry = 0;

    public static string DirData => Path.Combine(AppContext.BaseDirectory, "Data");
    public static string DirTrace => Path.Combine(AppContext.BaseDirectory, "Trace");

    public static string TokenFile => Path.Combine(DirData, $"Token.json");
    public string Uuid { get; set; }

    public List<MowerBase> Mowers = [];

    public PositecApi(ErrDelegte err, string uid) {
      var fac = new MqttFactory(); //.UseWebSocket4Net();
      string path = AppContext.BaseDirectory;
      string name = Dns.GetHostName();

      Uuid = $"{name.GetHashCode():X8}-{path.GetHashCode():X8}-{uid}"; // 9 + 9 + 36 => 54
      Trace.TraceInformation($"App ID: {name} {path} => {Uuid}");
      Err = err;
      _api = string.Empty;
      _mqttCOB = new MqttClientOptionsBuilder();
      _mqtt = fac.CreateMqttClient(new Logger(true));
      _oAuth = new();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Nicht verwendete private Member entfernen", Justification = "<Ausstehend>")]
    private async Task<bool> GetUser() {
      try {
        HttpResponseMessage response = await httpClient.GetAsync(UrlApi + "users/me");
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();

        Trace.TraceInformation($"Users me: {str}");
        File.WriteAllText(Path.Combine(DirTrace, $"UserMe {_api}.json"), str);
        return true;
      } catch( Exception ex ) {
        Err(ex.Message);
        Trace.TraceError($"GetUser => {ex}");
        return false;
      }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Nicht verwendete private Member entfernen", Justification = "<Ausstehend>")]
    private async Task<bool> GetProductList() { // kann auch ohne Login im Browser angefragt werden
      try {
        HttpResponseMessage response = await httpClient.GetAsync(UrlApi + "producst");
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();

        Trace.TraceInformation($"Product list: {str}");
        File.WriteAllText(Path.Combine(DirTrace, $"Products {_api}.json"), str);
      } catch( Exception ex ) {
        Err(ex.Message);
        Trace.TraceError($"GetProductList => {ex}");
        return false;
      }
      return true;
    }

    private async Task<bool> GetProducts() {
      try {
        HttpResponseMessage response = await httpClient.GetAsync(UrlApi + "product-items");
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();

        Trace.TraceInformation($"Product items: {str}");
        if( Json.Read<List<ProductItem>>(str) is List<ProductItem> pis ) {
          Mowers = [];
          foreach( var pi in pis ) {
            if( pi.Protocol == 1 ) Mowers.Add(new MowerP1 { Product = pi });
            else Mowers.Add(new MowerP0 { Product = pi });
          }
        }
      } catch( Exception ex ) {
        Err(ex.Message);
        Trace.TraceError($"GetProducts => {ex}");
        return false;
      }
      return true;
    }

    public async Task GetStatus(int idx) {
      try {
        if( await CheckToken() ) {
          string url = $"{UrlApi}product-items/{Mowers[idx].Product.SerialNo}?status=1";
          HttpResponseMessage response = await httpClient.GetAsync(url);
          response.EnsureSuccessStatusCode();
          var str = await response.Content.ReadAsStringAsync();

          //Trace.TraceInformation($"Last status: {str}");
          if( Mowers[idx] is MowerP0 mo ) {
            if( Json.Read<StatusOld>(str) is StatusOld pi && pi.Last != null && pi.Last.PayLoad != null ) {
              int ip = str.IndexOf("\"payload\":");

              str = str[(ip + 10)..];
              mo.Json = FormatJson(str[..^3]); // Substring(0, str.Length - 3)
              Trace.TraceInformation($"Last status: {str}");
              mo.Mqtt = pi.Last.PayLoad;
              //return true;
            }
          }
          if( Mowers[idx] is MowerP1 mn ) {
            if( Json.Read<StatusNew>(str) is StatusNew pi && pi.Last != null && pi.Last.PayLoad != null ) {
              int ip = str.IndexOf("\"payload\":");

              str = str[(ip + 10)..];
              mn.Json = FormatJson(str[..^3]);
              Trace.TraceInformation($"Last status: {str}");
              mn.Mqtt = pi.Last.PayLoad;
              //return true;
            }
          }
        }
      } catch( Exception ex ) {
        Err(ex.Message);
        Trace.TraceError($"GetLastState({idx}) => {ex}");
      }
      //return false;
    }

    public async Task ResetBlade(int idx) {
      try {
        string? snr = Mowers[idx].Product.SerialNo;

        if( snr != null ) {
          string url = $"{UrlApi}product-items/{snr}/counters/blade/reset";
          HttpResponseMessage res = await httpClient.PostAsync(url, null);
          var content = await res.Content.ReadAsStringAsync();

          //str = "{\"push_notifications\":" + "true" + "}";
          Trace.TraceInformation($"Reset Blade => {content}");
          if( Json.Read<ProductItem>(content) is ProductItem pi ) Mowers[idx].Product = pi;
        }
      } catch( Exception ex ) {
        Err(ex.Message);
        Trace.TraceError($"ResetBlade => {ex}");
      }
    }
    //public void AutoUpgrde(string serial, bool b) {
    //  string url = ConfigurationManager.AppSettings["ApiBaseUrl"] +  "product-items/" + serial, str;

    //  _client.Headers[HttpRequestHeader.ContentType] = "application/json";
    //  //str = client.UploadString(str, "PUT", "{\"name\":\"Egon\"}");
    //  str = "{\"firmware_auto_upgrade\":" + (b ? "true" : "false") + "}";
    //  str = _client.UploadString(url, "PUT", str);
    //  Log(string.Format("Auto upgd: {0}", str), 1);
    //  Debug.WriteLine("Auto upgd: {0}", str);
    //}
    private bool InitApi(string api) {
      ApiDic? da = new() {
        { "WX", new ApiEntry() { Login = "https://id.worx.com/", WebApi = "https://api.worxlandroid.com/api/v2/", ClientId = "013132A8-DB34-4101-B993-3C8348EA0EBC" } },
        { "KR", new ApiEntry() { Login = "https://id.kress.com/", WebApi = "https://api.kress-robotik.com/api/v2/", ClientId = "62FA25FB-3509-4778-A835-D5C50F4E5D88" } },
        { "LX", new ApiEntry() { Login = "https://id.landxcape-services.com/", WebApi = "https://api.landxcape-services.com/api/v2/", ClientId = "4F1B89F0-230F-410A-8436-D9610103A2A4" } },
        { "SM", new ApiEntry() { Login = "https://id.watermelon.smartmower.cloud/", WebApi = "https://api.watermelon.smartmower.cloud/api/v2/", ClientId = "AFD0DA53-FC0C-49FA-9A8F-1C2342F95E3E" } }
      };
      string path = Path.Combine(AppContext.BaseDirectory, "PositecApi.json");
      string str;

      if( File.Exists(path) ) {
        str = File.ReadAllText(path);
        da = Json.Read<ApiDic>(str);
      }

      _api = api;
      if( da != null && da[api] is ApiEntry ae && ae.Login != null && ae.WebApi != null && ae.ClientId != null ) {
        UrlLgn = ae.Login;
        UrlApi = ae.WebApi;
        CliId = ae.ClientId;
      }

      if( _api == null || UrlLgn == null || UrlApi == null || CliId == null ) {
        str = $"API-Prefix: {_api}, Login-Uri: {UrlLgn}, API-Uri: {UrlApi}, Client-Id: {CliId}";
        Err($"Intern data error => {str}");
        Trace.TraceError($"WebApi null data {str}");
        return false;
      }
      Trace.TraceInformation($"Init Positec API: {_api}");
      return true;
    }

    public async Task<bool> Login(string api, string mail, string pass) {
      //byte[] buf;

      if( InitApi(api) ) {
        #region Anmeldung
        try {
          var data = new Dictionary<string, string> {
            { "scope", "*" },
            { "client_id", CliId },
            { "grant_type", "password" },
            { "username", mail },
            { "password", pass }
          };
          HttpResponseMessage res = await httpClient.PostAsync(UrlLgn + "oauth/token", new FormUrlEncodedContent(data));
          var content = await res.Content.ReadAsStringAsync();

          if( res.IsSuccessStatusCode ) {
            Trace.TraceInformation($"Oauth token: {content}");
            if( Json.Read<OAuth>(content) is OAuth oa ) {
              _oAuth = oa;
              File.WriteAllText(TokenFile, content);
              httpClient.DefaultRequestHeaders.Clear();
              httpClient.DefaultRequestHeaders.Add("Authorization", $"{_oAuth.Type} {_oAuth.Access}");
              _tokDT = DateTime.Now;
              await GetProducts(); // füllt Mowers
            }
          } else {
            Trace.TraceError($"Oauth error: {content}");
            if( Json.Read<OError>(content) is OError oe ) Err($"{oe.Error} - {oe.Message}");
            else Err($"Unknown - {content}");
          }
        } catch( Exception ex ) {
          Err(ex.Message);
          Trace.TraceError($"Oauth token {ex}");
          return false;
        }
        #endregion
      }
      return Mowers?.Count > 0;
    }

    public async Task<bool> Access(string api) {
      if( InitApi(api) && File.Exists(TokenFile) ) {
        try {
          string str = File.ReadAllText(TokenFile);

          if( Json.Read<OAuth>(str) is OAuth oa ) _oAuth = oa;
        } catch( Exception ex ) {
          Trace.TraceError($"Access({api}) => {ex}");
          return false;
        }
        _tokDT = File.GetLastWriteTime(TokenFile);
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"{_oAuth.Type} {_oAuth.Access}"); // wird ggf gleich wieder überschrieben
        if( !await CheckToken() ) return false;
        await GetProducts(); // füllt Mowers
        //GetProductList();
      }
      return Mowers?.Count > 0;
    }

    private async Task<bool> RefreshToken() {
      if( !string.IsNullOrEmpty(_oAuth.Refresh) ) {
        var data = new Dictionary<string, string> {
            { "scope", "*" },
            { "client_id", CliId },
            { "grant_type", "refresh_token" },
            { "refresh_token", _oAuth.Refresh }
          };

        try {
          HttpResponseMessage res = await httpClient.PostAsync(UrlLgn + "oauth/token", new FormUrlEncodedContent(data));
          var content = await res.Content.ReadAsStringAsync();

          Trace.TraceInformation($"Refresh token: {content}");
          if( Json.Read<OAuth>(content) is OAuth oa && oa.Type == "Bearer" ) {
            _oAuth = oa;
            File.WriteAllText(TokenFile, content);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"{_oAuth.Type} {_oAuth.Access}");
            _tokDT = DateTime.Now;
            return true;
          } else {
            Trace.TraceError($"Oauth error: {content}");
            if( Json.Read<OError>(content) is OError oe ) Err($"{oe.Error} - {oe.Message}");
            else Err($"Unknown - {content}");
          }
        } catch( Exception ex ) {
          Err(ex.Message);
          Trace.TraceError($"Refresh token {ex}");
        }
      }
      return false;
    }

    public async Task<bool> CheckToken() {
      if( DateTime.Now > _tokDT + TimeSpan.FromSeconds(_oAuth.Expires) ) return await RefreshToken();
      else return true;
    }

    public async Task<List<ActivityEntry>> GetActivities(int idx) {
      var mp = Mowers[idx].Product;

      if( await CheckToken() ) {
        string url = $"{UrlApi}product-items/{mp.SerialNo}/activity-log";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();

        Trace.TraceInformation(str);
        File.WriteAllText(Path.Combine(DirTrace, $"ActLog_{mp.Name}.json"), str);
        if( Json.Read<List<ActivityEntry>>(str) is List<ActivityEntry> lae ) {
          foreach( ActivityEntry ae in lae ) {
            ActivityPayload ap = ae.Payload;

            Trace.TraceInformation($"{ae.Stamp}: {ap.Dat.LastError} - {ap.Dat.LastState} - {ap.Dat.Battery.Charging} - {ap.Dat.Battery.Maintenance}");
          }
          return lae;
        }
      }
      return [];
    }

    private string[] TokenToParts() {
      string[] tps = new string[3];

      if( !string.IsNullOrEmpty(_oAuth.Access) ) {
        string tok = _oAuth.Access;

        tok = tok.Replace('_', '/');
        tok = tok.Replace('-', '+');
        tps = tok.Split('.');
        for( int i = 0; i < tps.Length; i++ ) tps[i] = System.Web.HttpUtility.UrlEncode(tps[i]);
      }
      return tps;
    }

    public async Task<bool> Start() {
      string? broker = Mowers[0].Product.Endpoint;
      int userid = Mowers[0].Product.UserId; // User.Id;

      if( broker != null ) {
        try {
          var obp_tls = new MqttClientTlsOptions() {
            UseTls = true,
            SslProtocol = System.Security.Authentication.SslProtocols.Tls12,
            AllowUntrustedCertificates = true,
            IgnoreCertificateChainErrors = true,
            IgnoreCertificateRevocationErrors = true,
            ApplicationProtocols = [new System.Net.Security.SslApplicationProtocol("mqtt")]
          };
          obp_tls.CertificateValidationHandler += delegate { return true; };

          _mqttCOB = _mqttCOB.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311);
          _mqttCOB = _mqttCOB.WithKeepAlivePeriod(TimeSpan.FromMinutes(5));
          //_mqttCOB = _mqttCOB.WithNoKeepAlive();
          _mqttCOB = _mqttCOB.WithTcpServer(broker, 443);
          _mqttCOB = _mqttCOB.WithClientId($"{_api}/USER/{userid}/AvaDeskApp/{Uuid}");
          _mqttCOB = _mqttCOB.WithTlsOptions(obp_tls);
          _mqttCOB = _mqttCOB.WithCleanSession(true);
        } catch( Exception ex ) {
          Trace.TraceError($"Mqtt build {ex}");
          return false;
        }

        try {
          _mqtt.ApplicationMessageReceivedAsync += Mqtt_ApplicationMessageReceivedAsync;
          _mqtt.DisconnectedAsync += Mqtt_DisconnectedAsync;
          await ConnAndSub();
          Trace.TraceInformation($"Connect '{broker} ({_mqtt.IsConnected})'");
          //Publish("{}");
        } catch( Exception ex ) {
          Trace.TraceError($"Mqtt conn {ex}");
          return false;
        }
      } else Trace.TraceWarning($"Mqtt mower {Mowers[0].Product.Name} has no endpoint!");
      return true;
    }

    async Task ConnAndSub(bool re = false) {
      string[] tps;

      await CheckToken();
      tps = TokenToParts();
      _mqttCOB = _mqttCOB.WithCredentials($"da?jwt={tps[0]}.{tps[1]}&x-amz-customauthorizer-signature={tps[2]}");
      _mqttCOB = _mqttCOB.WithoutThrowOnNonSuccessfulConnectResponse();

      var op = _mqttCOB.Build();
      try {
        await _mqtt.ConnectAsync(op);
        Trace.TraceInformation($"Mqtt {(re ? "re" : "")}connected");
      } catch( Exception ex ) {
        Trace.TraceError($"Mqtt {(re ? "re" : "")}connected {ex}");
      }
      if( _mqtt.IsConnected ) _reTry = 0;

      foreach( MowerBase mb in Mowers ) {
        if( mb.Product.Topic is MqttTopic mt ) {
          await _mqtt.SubscribeAsync(mt.CmdOut, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
          Trace.TraceInformation($"Mqtt subscribed to {mt.CmdOut}");
        } else {
          Trace.TraceWarning($"Mqtt {mb.Product.Name} has no topic!");
        }
      }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("GeneratedRegex", "SYSLIB1045:In „GeneratedRegexAttribute“ konvertieren.", Justification = "<Ausstehend>")]
    public static string FormatJson(string json) {
      json = Regex.Replace(json, "(\"(?:cfg|dat)\")", "\r\n  $1");
      json = Regex.Replace(json, "(\"id\":[^[])", "\r\n    $1");
      json = Regex.Replace(json, "(\"(?:sn|lg|dt|tm|cmd|mz|rd|tq|al|rtk|head|modules)\")", "\r\n    $1");
      json = Regex.Replace(json, "(\"(?:4G|EA|ck|map)\":{)", "\r\n      $1");
      json = Regex.Replace(json, "(\"(?:ck|map)\")", "\r\n      $1");
      json = Regex.Replace(json, "(\"s[ct]\":{)", "\r\n    $1");
      json = Regex.Replace(json, "(\"(?:d|dd)\":\\[(?:\\[[^\\]]+\\],){2})((?:\\[[^\\]]+\\],){3})", "\r\n      $1\r\n        $2\r\n        ");
      json = Regex.Replace(json, "(\"(?:distm|ots)\")", "\r\n      $1");
      json = Regex.Replace(json, "\"(mac|fw|bt|dmp|ls|rsi|rain|moules)\"", "\r\n    \"$1\"");
      json = Regex.Replace(json, "(})$", "\r\n$1");
      return json;
    }

    Task Mqtt_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e) {
      string tpc = e.ApplicationMessage.Topic;
      string json = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
      int idx = Mowers.FindIndex((m) => m.Product.Topic?.CmdOut == tpc);

      Trace.TraceInformation($"Receive topic: {tpc}");
      Trace.TraceInformation($"Receive json: {json}");
      if( 0 <= idx && idx < Mowers.Count ) {
        try {
          if( e.ApplicationMessage.PayloadSegment.Array is byte[] ba ) {
            if( Mowers[idx] is MowerP0 mo && Json.Read<MqttP0>(ba) is MqttP0 m0 ) mo.Mqtt = m0;
            if( Mowers[idx] is MowerP1 mn && Json.Read<MqttP1>(ba) is MqttP1 m1 ) mn.Mqtt = m1;
          }
        } catch( Exception ex ) {
          Trace.TraceError($"Recveive {ex}");
        }
        Mowers[idx].Json = FormatJson(json);
        RecvMqtt?.Invoke(this, new RecvEventArgs() { MowIdx = idx });
      }
      return Task.CompletedTask;
    }
    async Task Mqtt_DisconnectedAsync(MqttClientDisconnectedEventArgs e) {
      Trace.TraceInformation($"Mqtt disconnected {e.ReasonString}");
      await Task.Delay(TimeSpan.FromSeconds(30));
      if( _reTry++ < 3 ) await ConnAndSub(true);
      else Trace.TraceWarning($"Mqtt disconnected {e.ReasonString} {_reTry}");
    }

    public void Exit() {
      if( _mqtt != null && _mqtt.IsConnected ) {
        _mqtt.ApplicationMessageReceivedAsync -= Mqtt_ApplicationMessageReceivedAsync;
        foreach( MowerBase mb in Mowers ) {
          if( mb.Product.Topic is MqttTopic mt ) _mqtt.UnsubscribeAsync(mt.CmdOut);
        }
        _mqtt.DisconnectedAsync -= Mqtt_DisconnectedAsync;
        try { _mqtt.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection); } catch( Exception ex ) { Trace.TraceError($"Exit {ex}"); }
      }
    }

    public bool Connected { get { return _mqtt != null && _mqtt.IsConnected; } }
    public void Publish(string s, int i) {
      if( Mowers[i].Product.Topic is MqttTopic mt ) {
        string str = s.StartsWith('{') && s.EndsWith('}') ? s : '{' + s + '}';
        var msg = new MqttApplicationMessageBuilder()
                      .WithTopic(mt.CmdIn)
                      .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                      .WithPayload(str).Build();

        try {
          _mqtt.PublishAsync(msg);
          Trace.TraceInformation($"Publish topic: {mt.CmdIn}");
          Trace.TraceInformation($"Publish json: {str}");
        } catch( Exception ex ) {
          Trace.TraceError($"Publish {ex}");
        }
      } else Trace.TraceWarning($"Publish {Mowers[i].Product.Name} has no topic.");
    }
  }
}
