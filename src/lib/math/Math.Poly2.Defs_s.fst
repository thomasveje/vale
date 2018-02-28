module Math.Poly2.Defs_s
open FStar.Seq
unfold let max = FStar.Math.Lib.max

// Polynomials cn * x^n + ... + c0 * x^0
// where coefficients ck are treated mod 2

let valid (s:seq bool) : bool =
  length s = 0 || index s (length s - 1)

// Each coefficient of a polynomial is 0 (false) or 1 (true).
// Each polynomial has a unique valid representation:
//   - zero is []
//   - a nonzero polynomial has a 1 as its high bit (no extra zeros beyond highest-order term)
// The unique representation ensures that mathematically equal polynomials are = in F*.
// s[0] is lowest-order term (x^0).
let poly = s:(seq bool){valid s}

let degree (p:poly) : int = length p - 1
let zero = create 0 false
let one = create 1 true
let monomial (n:nat) : poly = append (create n false) one

let shift (p:poly) (n:nat) : poly =
  if length p = 0 then p
  else append (create n false) p

// Index any coefficient, where all coefficients beyond highest-order term are zero
// (and n < 0 returns zero)
let poly_index (p:poly) (n:int) : bool =
  if 0 <= n && n < length p then index p n
  else false

unfold let ( .[] ) = poly_index

let rec of_seq (s:seq bool) : Pure poly
  (requires True)
  (ensures fun p ->
    length p <= length s /\
    (forall (i:nat).{:pattern (p.[i]) \/ (index s i)} i < length s ==> p.[i] == index s i)
  )
  (decreases (length s))
  =
  if valid s then s
  else
    of_seq (slice s 0 (length s - 1))

[@"opaque_to_smt"]
let of_fun (len:nat) (f:nat -> bool) : Pure poly
  (requires True)
  (ensures fun p ->
    length p <= len /\
    (forall (i:nat).{:pattern p.[i] \/ (f i)} i < len ==> p.[i] == f i) /\
    (forall (i:int).{:pattern p.[i]} p.[i] ==> 0 <= i /\ i < len)
  )
  =
  of_seq (init len f)

[@"opaque_to_smt"]
let add (a b:poly) : Pure poly
  (requires True)
  (ensures fun p ->
    let len = max (length a) (length b) in
    length p <= len /\
    (forall (i:int).{:pattern p.[i] \/ a.[i] \/ b.[i]} p.[i] == (a.[i] <> b.[i]))
  )
  =
  let len = max (length a) (length b) in
  of_fun len (fun (i:nat) -> a.[i] <> b.[i])

// f j + f (j + 1) + ... + f (k - 1)
let rec sum_of_bools (j k:int) (f:int -> bool) : Tot bool (decreases (k - j)) =
  if j >= k then false
  else (sum_of_bools j (k - 1) f) <> f (k - 1)

let mul_element_fun (a b:poly) (k i:int) : bool = a.[i] && b.[k - i]

// a0 * bk + a1 * b(k-1) + ... + ak * b0
let mul_element (a b:poly) (k:int) : bool =
  sum_of_bools 0 (k + 1) (mul_element_fun a b k)

[@"opaque_to_smt"]
let mul (a b:poly) : Pure poly
  (requires True)
  (ensures fun p ->
    let len = length a + length b in
    length p <= len /\
    (forall (i:nat).{:pattern p.[i]} i < len ==> p.[i] == mul_element a b i)
  )
  =
  let len = length a + length b in
  of_fun len (fun (i:nat) -> mul_element a b i)

