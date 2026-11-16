; https://github.com/erniecohen/dafny/issues/425
; The arguments encoding's same-index map row, in the form of a two-parameter map skeleton
; whose result type is the parameter r, with the native sorts int, real and bool.
; GUARDED is the row before the split: one row, whose premise holds for every native N
; with r == N's type code only if the payload is canonical at N.
; SPLIT is the row after it: one row per native N with N's type code in place of r, whose
; premise is canonicality at N, and one row whose premise is that r is no native type code.
; The first five check-sats must report unsat: GUARDED implies each row of SPLIT, and SPLIT
; implies GUARDED, given only the distinct constructor numbers the encoding asserts for the
; native type codes.  No cast law is assumed.  The last is a control and must not report
; unsat: neither form implies the legacy unguarded row.
(set-option :timeout 15000)
(declare-sort T 0)
(declare-sort U 0)
(declare-fun Ctor (T) Int)
(declare-const intType T)
(declare-const realType T)
(declare-const boolType T)
(assert (= (Ctor intType) 0))
(assert (= (Ctor realType) 1))
(assert (= (Ctor boolType) 2))
(declare-fun int_2_U (Int) U)
(declare-fun U_2_int (U) Int)
(declare-fun real_2_U (Real) U)
(declare-fun U_2_real (U) Real)
(declare-fun bool_2_U (Bool) U)
(declare-fun U_2_bool (U) Bool)
(declare-fun sel (T T U U) U)
(declare-fun sto (T T U U U) U)
(define-fun canonical ((r T) (v U)) Bool
  (and (=> (= r intType) (= (int_2_U (U_2_int v)) v))
       (=> (= r realType) (= (real_2_U (U_2_real v)) v))
       (=> (= r boolType) (= (bool_2_U (U_2_bool v)) v))))
(define-fun guarded () Bool
  (forall ((i T) (r T) (m U) (x U) (v U))
    (=> (canonical r v) (= (sel i r (sto i r m x v) x) v))))
(define-fun split-int () Bool
  (forall ((i T) (m U) (x U) (v U))
    (=> (= (int_2_U (U_2_int v)) v) (= (sel i intType (sto i intType m x v) x) v))))
(define-fun split-real () Bool
  (forall ((i T) (m U) (x U) (v U))
    (=> (= (real_2_U (U_2_real v)) v) (= (sel i realType (sto i realType m x v) x) v))))
(define-fun split-bool () Bool
  (forall ((i T) (m U) (x U) (v U))
    (=> (= (bool_2_U (U_2_bool v)) v) (= (sel i boolType (sto i boolType m x v) x) v))))
(define-fun split-other () Bool
  (forall ((i T) (r T) (m U) (x U) (v U))
    (=> (and (not (= r intType)) (not (= r realType)) (not (= r boolType)))
        (= (sel i r (sto i r m x v) x) v))))
(push 1)
(assert guarded)
(assert (not split-int))
(check-sat)
(pop 1)
(push 1)
(assert guarded)
(assert (not split-real))
(check-sat)
(pop 1)
(push 1)
(assert guarded)
(assert (not split-bool))
(check-sat)
(pop 1)
(push 1)
(assert guarded)
(assert (not split-other))
(check-sat)
(pop 1)
(push 1)
(assert split-int)
(assert split-real)
(assert split-bool)
(assert split-other)
(assert (not guarded))
(check-sat)
(pop 1)
(push 1)
(assert split-int)
(assert split-real)
(assert split-bool)
(assert split-other)
(assert (not (forall ((i T) (r T) (m U) (x U) (v U)) (= (sel i r (sto i r m x v) x) v))))
(check-sat)
(pop 1)
