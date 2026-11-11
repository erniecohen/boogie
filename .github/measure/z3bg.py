# Solver time on the background: for a sample of per-check logs, the time Z3 takes to read and
# assert the background (everything before the first "(push 1)", then exit) against the time of
# the whole check (the whole log).  Prints medians and the ratio of sums.
import os, random, subprocess, sys, time, statistics
z3, d, k = sys.argv[1], sys.argv[2], int(sys.argv[3])
fs = sorted(os.listdir(d)); random.Random(1).shuffle(fs); fs = fs[:k]
bgt = []; allt = []
for f in fs:
    t = open(os.path.join(d, f), errors="replace").read()
    i = t.find("(push 1)")
    if i < 0: continue
    for text, acc in ((t[:i] + "\n(exit)\n", bgt), (t, allt)):
        s = time.perf_counter()
        subprocess.run([z3, "-in", "-smt2", "-T:120"], input=text.encode(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        acc.append(time.perf_counter() - s)
print(f"| sample | median background s | median whole check s | background / whole (sums) |")
print(f"|---:|---:|---:|---:|")
print(f"| {len(bgt)} | {statistics.median(bgt):.3f} | {statistics.median(allt):.3f} | {sum(bgt)/sum(allt):.3f} |")
