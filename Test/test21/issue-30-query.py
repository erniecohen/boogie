# Preserve every submitted command through the sole proof query. Batch mode can
# log its final pop after closing solver input, and that cleanup line races with
# disposal. It is not part of the query and is not sent to the batch solver.
import pathlib
import sys

lines = pathlib.Path(sys.argv[1]).read_text().splitlines()
queries = [i for i, line in enumerate(lines) if line == "(check-sat)"]
assert len(queries) == 1, "expected exactly one proof query"
end = queries[0] + 1
allowed = {"", "(get-info :reason-unknown)", "(get-info :rlimit)",
           "(get-model)", "(pop 1)", "; Valid"}
assert all(line in allowed for line in lines[end:]), "unexpected command after proof query"
print("\n".join(lines[:end]))
