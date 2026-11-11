# Tables of the runs: wall, CPU (user + sys, the Dafny process and the solvers it waited for),
# mean busy cores (CPU / wall), peak RSS, and from dotnet-counters the bytes allocated and the
# time paused by GC.  Noop runs are grouped over their repetitions (mean ± sd).
import collections, csv, glob, os, re, statistics, sys

runs = {}
for line in open(sys.argv[1]):
    p = line.split()
    if len(p) < 5: continue
    name, wall, user, sys_, rss = p[0], *map(float, p[1:5])
    runs[name] = dict(wall=wall, cpu=user + sys_, rss=rss / 1e6, verdict=" ".join(p[5:]))

for f in sys.argv[2:]:
    name = os.path.basename(f)[len("counters-"):-len(".csv")]
    if name not in runs: continue
    alloc = pause = 0.0
    with open(f, newline="") as fh:
        for row in csv.reader(fh):
            if len(row) < 5: continue
            counter, value = row[2], row[4]
            try: v = float(value)
            except ValueError: continue
            if counter.startswith("Allocation Rate"): alloc += v
            elif counter.startswith("Time paused by GC") or counter.startswith("Time spent in GC"): pause += v
    runs[name]["alloc_gb"] = alloc / 1e9
    runs[name]["pause_s"] = pause / 1e3

def fmt(xs):
    xs = [x for x in xs if x is not None]
    if not xs: return "n/a"
    m = statistics.mean(xs)
    return f"{m:.1f}" if len(xs) == 1 else f"{m:.1f} ± {statistics.stdev(xs):.1f}"

groups = collections.defaultdict(list)
for name, r in runs.items():
    groups[re.sub(r"-r\d+$", "", name)].append(r)

print("| run | n | wall s | CPU s | busy cores | peak RSS GB | allocated GB | GC pause s | outcome |")
print("|---|---:|---:|---:|---:|---:|---:|---:|---|")
for g in sorted(groups):
    rs = groups[g]
    print(f"| {g} | {len(rs)} | {fmt([r['wall'] for r in rs])} | {fmt([r['cpu'] for r in rs])} | "
          f"{fmt([r['cpu'] / r['wall'] for r in rs])} | {fmt([r['rss'] for r in rs])} | "
          f"{fmt([r.get('alloc_gb') for r in rs])} | {fmt([r.get('pause_s') for r in rs])} | {rs[0]['verdict']} |")
