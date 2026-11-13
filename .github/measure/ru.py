# Per-check resource counts of Dafny runs, from its csv logs, each compared with the first.
# Usage: ru.py name=file.csv ...   A check is a row's TestResult.DisplayName; a name that
# occurs more than once is numbered by occurrence.
import collections, csv, sys

def load(path):
    rows, seen = {}, collections.Counter()
    for r in csv.DictReader(open(path, newline="")):
        name = r["TestResult.DisplayName"]
        seen[name] += 1
        rows[(name, seen[name])] = (r["TestResult.Outcome"], int(r["TestResult.ResourceCount"] or 0))
    return rows

runs = [(a.split("=", 1)[0], load(a.split("=", 1)[1])) for a in sys.argv[1:]]
ref_name, ref = runs[0]
print(f"| run against {ref_name} | checks | same RU | other RU | RU sum ratio | median relative ΔRU of the others | other outcome |")
print("|---|---:|---:|---:|---:|---:|---:|")
for name, rows in runs[1:]:
    keys = ref.keys() & rows.keys()
    same = sum(ref[k][1] == rows[k][1] for k in keys)
    rel = sorted(abs(rows[k][1] - ref[k][1]) / max(ref[k][1], 1) for k in keys if ref[k][1] != rows[k][1])
    med = f"{rel[len(rel) // 2]:.2%}" if rel else "-"
    outcome = sum(ref[k][0] != rows[k][0] for k in keys)
    ratio = sum(rows[k][1] for k in keys) / max(sum(ref[k][1] for k in keys), 1)
    print(f"| {name} | {len(keys)} | {same} | {len(keys) - same} | {ratio:.4f} | {med} | {outcome} |")
