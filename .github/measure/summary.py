# Tables of the runs: wall, CPU (user + sys, the Dafny process and the solvers it waited for),
# mean busy cores (CPU / wall) and peak RSS.  Noop runs are grouped over their repetitions (mean ± sd).
import collections, re, statistics, sys

runs = {}
for line in open(sys.argv[1]):
    p = line.split()
    if len(p) < 5: continue
    name, wall, user, sys_, rss = p[0], *map(float, p[1:5])
    runs[name] = dict(wall=wall, cpu=user + sys_, rss=rss / 1e6, verdict=" ".join(p[5:]))

def fmt(xs):
    xs = [x for x in xs if x is not None]
    if not xs: return "n/a"
    m = statistics.mean(xs)
    return f"{m:.1f}" if len(xs) == 1 else f"{m:.1f} ± {statistics.stdev(xs):.1f}"

groups = collections.defaultdict(list)
for name, r in runs.items():
    groups[re.sub(r"-r\d+$", "", name)].append(r)

print("| run | n | wall s | CPU s | busy cores | peak RSS GB | outcome |")
print("|---|---:|---:|---:|---:|---:|---|")
for g in sorted(groups):
    rs = groups[g]
    print(f"| {g} | {len(rs)} | {fmt([r['wall'] for r in rs])} | {fmt([r['cpu'] for r in rs])} | "
          f"{fmt([r['cpu'] / r['wall'] for r in rs])} | {fmt([r['rss'] for r in rs])} | "
          f"{rs[0]['verdict']} |")
