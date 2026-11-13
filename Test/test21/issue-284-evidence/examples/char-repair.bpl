// Standalone local repair schema, legacy character range for concreteness.
// The actual product patch retains its existing Unicode preprocessing.
// Expected semantic result: the last assertion is false, with or without erasure.
type Ty;
const TChar: Ty;
type char;
function $Is<T>(T,Ty): bool;
function char#IsChar(n: int): bool { 0 <= n && n < 65536 }
function char#FromInt(int): char;
function char#ToInt(char): int;
axiom (forall n: int ::
  { char#FromInt(n) }
  char#IsChar(n) ==> char#ToInt(char#FromInt(n)) == n);
axiom (forall ch: char ::
  { char#ToInt(ch) }
  $Is(ch,TChar) ==>
    char#FromInt(char#ToInt(ch)) == ch &&
    char#IsChar(char#ToInt(ch)));
axiom (forall ch: char ::
  { $Is(ch,TChar) }
  $Is(ch,TChar) <==>
    char#IsChar(char#ToInt(ch)) &&
    char#FromInt(char#ToInt(ch)) == ch);
procedure Negative(c: char)
  requires $Is(c,TChar);
{
  assert char#IsChar(char#ToInt(c));
  assert false;
}
