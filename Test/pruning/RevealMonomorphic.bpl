// RUN: %parallel-boogie /prune:1 /errorTrace:0 /typeEncoding:m "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /prune:1 /errorTrace:0 /typeEncoding:a "%s" > "%t.a"
// RUN: %diff "%s.expect" "%t.a"

// The monomorphic encoding splits each axiom at its conjunctions and instantiates
// polymorphic axioms.  The new axioms stay hideable, so hide still prunes them.

function F(x: int): int uses {
  hideable axiom (forall x: int :: {F(x)} F(x) == 1) && (forall x: int :: {F(x)} F(x) > 0);
}

function G<T>(x: T): int uses {
  hideable axiom (forall<T> x: T :: {G(x)} G(x) == 2);
}

procedure Hidden() { hide *; assert F(0) == 1; } // error
procedure HiddenConjunct() { hide *; assert F(0) > 0; } // error
procedure Revealed() { hide *; reveal F; assert F(0) == 1 && F(0) > 0; }
procedure HiddenPolymorphic() { hide *; assert G(true) == 2; } // error
procedure RevealedPolymorphic() { hide *; reveal G; assert G(true) == 2 && G(3) == 2; }
procedure NotHidden() { assert F(0) == 1 && G(true) == 2; }
