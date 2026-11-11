# Solver time on the background.  For a sample of per-check logs, times Z3 on
#   read:   the background (everything before the first "(push 1)"), then exit
#   solve:  the background and (check-sat), with no obligation
#   whole:  the whole log, the check itself
# and prints medians and ratios.  The background's share of a check is at most solve / whole.
import os, random, subprocess, sys, time, statistics
z3, d, k = sys.argv[1], sys.argv[2], int(sys.argv[3])
cap = sys.argv[4] if len(sys.argv) > 4 else "120"  # seconds per Z3 run
fs = sorted(os.listdir(d)); random.Random(1).shuffle(fs); fs = fs[:k]
T = {"read": [], "solve": [], "whole": []}
for f in fs:
    t = open(os.path.join(d, f), errors="replace").read()
    i = t.find("(push 1)")
    if i < 0: continue
    for name, text in (("read", t[:i] + "\n(exit)\n"), ("solve", t[:i] + "\n(check-sat)\n(exit)\n"), ("whole", t)):
        s = time.perf_counter()
        subprocess.run([z3, "-in", "-smt2", "-T:" + cap], input=text.encode(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        T[name].append(time.perf_counter() - s)
n = len(T["whole"]); capped = sum(x >= float(cap) - 0.5 for x in T["whole"])
print(f"Z3 runs capped at {cap} s; {capped} of {n} whole checks reached the cap.\n")
print("| sample | median read s | median solve s | median whole s | read / whole (sums) | solve / whole (sums) | solve / whole (median) |")
print("|---:|---:|---:|---:|---:|---:|---:|")
m = statistics.median
print(f"| {n} | {m(T['read']):.3f} | {m(T['solve']):.3f} | {m(T['whole']):.3f} | {sum(T['read'])/sum(T['whole']):.3f} | "
      f"{sum(T['solve'])/sum(T['whole']):.3f} | {m(s/w for s, w in zip(T['solve'], T['whole'])):.3f} |")
