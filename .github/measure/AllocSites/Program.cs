// Prints the GC pause time (SuspendEEStart to RestartEEStop) and the number of GCs, then sums the GC's allocation samples (GCAllocationTick, about one per 100 KB allocated) of a
// nettrace by allocated type, by the innermost Boogie or Dafny frame of the allocating stack,
// and by that frame's caller chain of three frames, and prints the top sites in MB.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

var path = args[0];
var top = args.Length > 1 ? int.Parse(args[1]) : 25;
var etlx = TraceLog.CreateFromEventPipeDataFile(path);
using var log = new TraceLog(etlx);
var byType = new Dictionary<string, double>();
var byFrame = new Dictionary<string, double>();
var byChain = new Dictionary<string, double>();
double total = 0;
long samples = 0;
double pauseMs = 0, suspendAt = -1;
int gcs = 0;
foreach (var e in log.Events) {
  if (e.EventName == "GC/SuspendEEStart") { suspendAt = e.TimeStampRelativeMSec; continue; }
  if (e.EventName == "GC/RestartEEStop" && suspendAt >= 0) { pauseMs += e.TimeStampRelativeMSec - suspendAt; suspendAt = -1; continue; }
  if (e.EventName == "GC/Start") { gcs++; continue; }
  if (e is not GCAllocationTickTraceData tick) continue;
  double mb = tick.AllocationAmount64 / 1e6;
  total += mb; samples++;
  byType[tick.TypeName ?? "?"] = byType.GetValueOrDefault(tick.TypeName ?? "?") + mb;
  var frames = new List<string>();
  for (var s = e.CallStack(); s != null; s = s.Caller) {
    var m = s.CodeAddress.FullMethodName;
    if (string.IsNullOrEmpty(m)) continue;
    if (m.StartsWith("Microsoft.Boogie") || m.StartsWith("Microsoft.Dafny") || m.StartsWith("VC.") || m.StartsWith("Boogie")) frames.Add(m);
    if (frames.Count == 3) break;
  }
  var frame = frames.Count > 0 ? frames[0] : "(no Boogie or Dafny frame)";
  byFrame[frame] = byFrame.GetValueOrDefault(frame) + mb;
  var chain = string.Join("  <-  ", frames);
  byChain[chain] = byChain.GetValueOrDefault(chain) + mb;
}
Console.WriteLine($"GCs: {gcs}, GC pause: {pauseMs / 1000:F2} s, allocation samples: {samples}, allocated (sampled) MB: {total:F0}");
void Print(string title, Dictionary<string, double> d) {
  Console.WriteLine($"\n## {title}\n\n| MB | % | site |\n|---:|---:|---|");
  foreach (var (k, v) in d.OrderByDescending(p => p.Value).Take(top))
    Console.WriteLine($"| {v:F0} | {100 * v / total:F1} | `{k.Replace("|", "\\|")}` |");
}
Print("By type", byType);
Print("By innermost Boogie/Dafny frame", byFrame);
Print("By chain of three Boogie/Dafny frames", byChain);
