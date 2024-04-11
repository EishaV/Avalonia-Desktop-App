using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml;

namespace AvaApp.Texts {
  public class LocalizeExtension : MarkupExtension {
    public LocalizeExtension(string key) { Key = key; }

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) {
      var binding = new ReflectionBindingExtension($"[{Key}]") {
        Mode = BindingMode.OneWay,
        Source = Ressource.Loc
      };
      return binding.ProvideValue(serviceProvider);
    }
  }

  public class Ressource {
    [XmlType(TypeName = "string")]
    public struct Item {
      [XmlAttribute("name")]
      public string Key;
      [XmlText]
      public string Value;
    }

    internal static SortedDictionary<string, string> Loc => _loc;
    private static SortedDictionary<string, string> _loc = new ();

    static Ressource() {
      string lng = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
      string fn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Texts", "strings." + lng + ".xml");

      if( !File.Exists(fn) ) fn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Texts", "strings.de.xml");
      try {
        var xs = new XmlSerializer(typeof(Item[]), new XmlRootAttribute("resources"));
        Item[]? tmp;

        using(FileStream fs = new (fn, FileMode.Open)) tmp = (xs.Deserialize(fs) as Item[]);
        if(tmp != null) _loc = new(tmp.ToDictionary(item => item.Key, item => item.Value));
      } catch( Exception ex ) {
        Debug.WriteLine(ex.ToString());
      }
    }

    public static string Get(string name) { return Loc != null && Loc.ContainsKey(name) ? Loc[name] : name; }
  }
}
