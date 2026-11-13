; Direct specialization of pinned arguments-encoding map axioms to [Box]bool.
; The three distinct values may be the prelude's TInt, TReal, TBool.
; Guarded-row variant of the supplied kernel. Expected: sat or unknown.
; Original expected: unsat. This is a logical encoding probe, not captured solver output.
(set-option :timeout 15000)
(declare-sort U 0)
(declare-fun dec (U) Bool)
(declare-fun enc (Bool) U)
(assert (forall ((b Bool)) (= (dec (enc b)) b)))
(declare-fun sel (U U) U)
(declare-fun upd (U U U) U)
(declare-const a U)
(declare-const b U)
(declare-const c U)
(assert (distinct a b c))
(assert (forall ((m U) (x U) (v U)) (=> (= (enc (dec v)) v) (= (sel (upd m x v) x) v))))
(assert (forall ((m U) (x U) (y U) (v U))
  (=> (distinct x y) (= (sel (upd m x v) y) (sel m y)))))
(assert (forall ((m U) (n U))
  (=> (forall ((x U)) (= (dec (sel m x)) (dec (sel n x)))) (= m n))))
(check-sat)
