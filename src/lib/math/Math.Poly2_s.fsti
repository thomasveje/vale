module Math.Poly2_s
module D = Math.Poly2.Defs_s
open FStar.Seq

// Polynomials cn * x^n + ... + c0 * x^0
// where coefficients ck are treated mod 2
// Each coefficient is 0 (false) or 1 (true)

val poly : Type0
val degree (p:poly) : int // note: degree zero == -1
val zero : poly
val one : poly
val monomial (n:nat) : poly // x^n
val shift (p:poly) (n:nat) : poly // x^n * p

// Index any coefficient, where all coefficients beyond highest-order term are zero
// (and n < 0 returns zero)
val poly_index (p:poly) (n:int) : bool
unfold let ( .[] ) = poly_index

val of_seq (s:seq bool) : Pure poly
  (requires True)
  (ensures fun p ->
    degree p < length s /\
    (forall (i:nat).{:pattern (p.[i]) \/ (index s i)} i < length s ==> p.[i] == index s i)
  )

val of_fun (len:nat) (f:nat -> bool) : Pure poly
  (requires True)
  (ensures fun p ->
    degree p < len /\
    (forall (i:nat).{:pattern p.[i] \/ (f i)} i < len ==> p.[i] == f i) /\
    (forall (i:int).{:pattern p.[i]} p.[i] ==> 0 <= i /\ i < len)
  )

val add (a b:poly) : poly
val mul (a b:poly) : poly
val div (a b:poly) : poly
val mod (a b:poly) : poly

unfold let coerce (#a:Type0) (b:Type0) (x:a{a == b}) : b = x
unfold let to_poly (p:poly{poly == D.poly}) : D.poly = coerce D.poly p
unfold let of_poly (p:D.poly{poly == D.poly}) : poly = coerce poly p

val reveal_defs (_:unit) : Lemma
  (ensures
    poly == D.poly /\
    (forall (p:poly).{:pattern (degree p)} degree p == D.degree (to_poly p)) /\
    zero == of_poly D.zero /\
    one == of_poly D.one /\
    (forall (n:nat).{:pattern (monomial n)} monomial n == of_poly (D.monomial n)) /\
    (forall (p:poly) (n:nat).{:pattern (shift p n)} shift p n == of_poly (D.shift (to_poly p) n)) /\
    (forall (p:poly) (n:nat).{:pattern (poly_index p n)} poly_index p n == D.poly_index (to_poly p) n) /\
    (forall (a b:poly).{:pattern (add a b)} add a b == of_poly (D.add (to_poly a) (to_poly b))) /\
    (forall (a b:poly).{:pattern (mul a b)} mul a b == of_poly (D.mul (to_poly a) (to_poly b)))
  )
