# N against U: per-check solver logs of one run; the background is what is sent before the first
# "(push 1)".  Prints N (checks), U (distinct backgrounds), the share of bytes in backgrounds, and
# distinct axiom texts with and without the namer's @@<n> suffixes.
import hashlib, os, re, sys, collections
d = sys.argv[1]
bg = collections.Counter(); bgb = tot = 0; ax = set(); axn = set()
for f in os.listdir(d):
    t = open(os.path.join(d, f), errors="replace").read()
    i = t.find("(push 1)")
    if i < 0: continue
    b = t[:i]; bg[hashlib.sha256(b.encode()).hexdigest()] += 1; bgb += len(b); tot += len(t)
    for a in b.split("\n(assert")[1:]:
        h = hashlib.sha256(a.encode()).digest(); ax.add(h)
        axn.add(hashlib.sha256(re.sub(r"@@\d+", "@@", a).encode()).digest())
n = sum(bg.values())
print(f"| checks N | distinct backgrounds U | U/N | background share of bytes | distinct axiom texts | after removing @@n |")
print(f"|---:|---:|---:|---:|---:|---:|")
print(f"| {n} | {len(bg)} | {len(bg)/max(n,1):.3f} | {bgb/max(tot,1):.3f} | {len(ax)} | {len(axn)} |")
