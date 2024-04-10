using System;
using System.Diagnostics;
using System.IO;

namespace AvaApp {
  internal class StampTraceListener : TextWriterTraceListener {
    public StampTraceListener(string path) : base(path) {
      if(Writer is StreamWriter) ((StreamWriter)Writer).AutoFlush = true;
    }

    public override void TraceEvent(TraceEventCache? cache, string src, TraceEventType type, int id, string? msg) {
      bool filter = Filter != null && !Filter.ShouldTrace(cache, src, type, id, msg, null, null, null);

      if(!string.IsNullOrEmpty(msg) && !filter) {
        string? dt = cache?.DateTime.ToLocalTime().ToString("dd.MM.yy HH:mm:ss.fff");
        string et = type.ToString().Substring(0, 1);

        WriteLine($"{dt} {et}: {msg}");
      }
    }
  }
}
