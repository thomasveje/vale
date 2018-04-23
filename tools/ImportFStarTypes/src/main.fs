open Ast
open Parse
open Parse_util
open Microsoft.FSharp.Text.Lexing
open System.IO

exception Err of string
exception InternalErr of string
exception LocErr of loc * exn
let err (s:string):'a = raise (Err s)

type string_tree_raw =
| StLeaf of string
| StList of string * string * string_tree list
and string_tree = int(*printed length in characters*) * string_tree_raw

let st_leaf (s:string):string_tree =
  (s.Length, StLeaf s)

let make_st_list (l:string) (r:string) (ts:string_tree list):string_tree =
  let len = List.fold (fun len t -> len + fst t + 1) (l.Length + r.Length) ts in
  (len, StList (l, r, ts))

let st_list = make_st_list "" ""
let st_paren = make_st_list "(" ")"
let st_angle = make_st_list "<" ">"
let st_brace = make_st_list "{" "}"
let st_bracket = make_st_list "[" "]"

let list_opt (m:'a option) (f:'a -> 'b list):'b list = match m with None -> [] | Some x -> f x in

let rec string_of_tree (t:string_tree):string =
  match snd t with
  | StLeaf s -> s
  | StList (l, r, ts) -> l + String.concat " " (List.map string_of_tree ts) + r

let rec print_tree (indent:string) (t:string_tree):unit =
  let (len, r) = t in
  match r with
  | StLeaf s ->
      printfn "%s%s" indent s
  | StList _ when len < 100 ->
      printfn "%s%s" indent (string_of_tree t)
  | StList (l, r, (t1::ts)) when fst t1 < 100 ->
      printfn "%s%s%s" indent l (string_of_tree t1);
      List.iter (print_tree (indent + "  ")) ts;
      (if r.Length > 0 then printfn "%s%s" indent r)
  | StList (l, r, (t1::ts)) when l.Length = 0 && r.Length = 0 ->
      print_tree indent t1;
      List.iter (print_tree (indent + "  ")) ts
  | StList (l, r, ts) ->
      (if l.Length > 0 then printfn "%s%s" indent l);
      List.iter (print_tree (indent + "  ")) ts;
      (if r.Length > 0 then printfn "%s%s" indent r)

let rec tree_of_raw_exp (e:raw_exp):string_tree =
  match e with
  | RLet _ -> st_leaf "LET"
  | RFun _ -> st_leaf "FUN"
  | RArrow _ -> st_leaf "->"
  | RTildeArrow _ -> st_leaf "~>"
  | RDollarDollar _ -> st_leaf "$$"
  | RPlus _ -> st_leaf "+"
  | RRefine _ -> st_leaf "REFINE"
  | RLtGt _ -> st_leaf "<>"
  | RMeta _ -> st_leaf "META"
  | RComma _ -> st_leaf ","
  | RAttribute _ -> st_leaf "ATTRIBUTE"
  | RAttributes _ -> st_leaf "ATTRIBUTES"
  | RDecreases _ -> st_leaf "DECREASES"
  | RDollar _ -> st_leaf "$"
  | RHash _ -> st_leaf "#"
  | RColon _ -> st_leaf ":"
  | RSColon _ -> st_leaf "::"
  | RSemi _ -> st_leaf ";"
  | RMatch _ -> st_leaf "MATCH"
  | RWith _ -> st_leaf "WITH"
  | RBar _ -> st_leaf "|"
  | RHashDot _ -> st_leaf "#."
  | RType _ -> st_leaf "TYPE"
  | RAscribed _ -> st_leaf "ASCRIBED"
  | RPattern _ -> st_leaf "PATTERN"
  | RDecl (_, s) -> st_leaf s
  | RQualifier (_, s) -> st_leaf s
  | RUnitValue _ -> st_leaf "UNIT_VALUE"
  | RInt (_, i) -> st_leaf (i.ToString())
  | RString _ -> st_leaf "STRING"
  | RId (_, s) -> st_leaf s
  | RList es -> st_paren (List.map tree_of_raw_exp es)

let rec string_of_raw_exp (e:raw_exp):string =
  string_of_tree (tree_of_raw_exp e)

let rec simplify_raw_exp (e:raw_exp):raw_exp =
  match e with
  | RList [RDollarDollar _; e; _; _; _] -> simplify_raw_exp e
  | RList ((RAscribed _)::e::_) -> simplify_raw_exp e
  | RList [RLet _; _; RMeta _; e] -> simplify_raw_exp e
  | RList es ->
      let es = List.filter (fun e -> match e with RMeta _ -> false | _ -> true) es in
      let es = List.map simplify_raw_exp es in
      RList es
  | _ -> e

let string_opt m f = match m with None -> "" | Some x -> f x in

let string_of_id (id:id):string =
  match id with
  | { name = None; index = None } -> "__"
  | _ -> (string_opt id.name (fun x -> x)) + (string_opt id.index (fun i -> "@" + i.ToString()))

let tree_of_id (id:id):string_tree =
  st_leaf (string_of_id id)

let tree_of_aqual (a:aqual) (t:string_tree):string_tree =
  match a with
  | Explicit -> t
  | Implicit -> st_list [st_leaf "#"; t]
  | Equality -> st_list [st_leaf "$"; t]

let rec tree_of_univ (u:univ):string_tree =
  match u with
  | UId x -> tree_of_id x
  | UInt i -> st_leaf (i.ToString())
  | UPlus (u1, u2) -> st_paren [tree_of_univ u1; st_leaf " + "; tree_of_univ u2]
  | UMax us -> st_paren (st_leaf "max" :: List.map tree_of_univ us)

let rec tree_of_exp (e:exp):string_tree =
  match e with
  | EId x -> tree_of_id x
  | EInt i -> st_leaf (i.ToString())
  | EUnitValue -> st_leaf "UNIT_VALUE"
  | EType u -> st_paren [st_leaf "Type"; tree_of_univ u]
  | EComp (e1, e2, es) -> st_paren (st_list [st_leaf "!!"; tree_of_exp e1] :: List.map tree_of_exp (e2::es))
  | EApp (e, aes) -> st_paren (tree_of_exp e :: List.map tree_of_aqual_exp aes)
  | EAppUnivs (e, us) -> st_paren ([tree_of_exp e; st_leaf "<"] @ List.map tree_of_univ us @ [st_leaf ">"])
  | EArrow (a, x, e1, e2) ->
      st_paren [
        st_list [st_list [tree_of_aqual a (tree_of_id x); st_leaf ":"]; tree_of_exp e1; st_leaf "->"];
        tree_of_exp e2
      ]
  | ETyped (e1, e2) -> st_paren [tree_of_exp e1; st_leaf ":"; tree_of_exp e2]
  | ERefine (x, e1, e2) ->
      st_paren [st_list [st_list [tree_of_id x; st_leaf ":"]; tree_of_exp e1]; st_brace [tree_of_exp e2]]
  | EAscribed (e1, e2) -> st_paren [tree_of_exp e1; st_leaf ":>"; tree_of_exp e2]
  | EPattern (pats, e) ->
      st_paren [st_leaf "PATTERN"; st_paren (List.map st_paren (List.map (List.map tree_of_exp) pats)); tree_of_exp e]
  | ELet (b, e1, e2) -> st_paren [st_leaf "let"; tree_of_binder b; st_leaf "="; tree_of_exp e1; st_leaf "in"; tree_of_exp e2]
  | EFun (bs, e) -> st_paren ([st_leaf "fun"] @ trees_of_binders bs @ [st_leaf "->"; tree_of_exp e])
  | EUnsupported s -> st_paren [st_leaf "UNSUPPORTED"; st_leaf ("\"" + s + "\"")]
and tree_of_aqual_exp ((a:aqual), (e:exp)):string_tree =
  tree_of_aqual a (tree_of_exp e)
and tree_of_binder ((a, x, tOpt):binder):string_tree =
  let st =
    match tOpt with
    | None -> tree_of_id x
    | Some t -> st_list [st_list [tree_of_id x; st_leaf ":"]; tree_of_exp t]
  in
  tree_of_aqual a st
and trees_of_binders (bs:binder list):string_tree list =
  List.map tree_of_binder bs

let string_of_exp (e:exp):string =
  string_of_tree (tree_of_exp e)

let tree_of_decl (d:decl):string_tree =
  let
    {
      d_name = x;
      d_qualifiers = quals;
      d_category = category;
      d_udecls = udecls;
      d_binders = bs;
      d_typ = t;
      d_body = body_opt;
    }
    = d
    in
  st_list
    ([st_list ([st_list ([st_leaf x; st_leaf "<"] @ List.map tree_of_id udecls @ [st_leaf ">"])]
      @ trees_of_binders bs @ [st_leaf "::"])]
    @ [tree_of_exp t]
    @ (list_opt body_opt (fun e -> [st_leaf "="; tree_of_exp e])))

let parse_id (s:string):id =
  let i = s.IndexOf("@") in
  if i >= 0 then
    let name = if i = 0 then None else Some (s.Substring(0, i)) in
    let index = Some (System.Int32.Parse(s.Substring(i + 1))) in
    { name = name ; index = index }
  else
    { name = Some s; index = None }

let parse_rid (e:raw_exp):id =
  match e with
  | RId (_, x) -> parse_id x
  | _ -> err ("parse_rid: " + string_of_raw_exp e)

let rec parse_univ (e:raw_exp):univ =
  match e with
  | RId (_, x) -> UId (parse_id x)
  | RInt (_, i) -> UInt i
  | RList [RPlus _; e1; e2] -> UPlus (parse_univ e1, parse_univ e2)
  | RList (RId (_, "max") :: us) -> UMax (parse_univs us)
  | _ -> err ("parse_univ: " + string_of_raw_exp e)
and parse_univs (es:raw_exp list):univ list =
  match es with
  | [e] -> [parse_univ e]
  | e::(RComma _)::es -> (parse_univ e)::(parse_univs es)
  | _ -> err ("parse_univs: " + string_of_raw_exp (RList es))

let get_aqual (e:raw_exp):aqual * raw_exp =
  match e with
  | RList [RDollar _; e] -> (Equality, e)
  | RList [(RHash _ | RHashDot _); e] -> (Implicit, e)
  | _ -> (Explicit, e)

let rec parse_exp (e:raw_exp):exp =
  match e with
  | RId (_, x) -> EId (parse_id x)
  | RInt (_, i) -> EInt i
  | RUnitValue _ -> EUnitValue
  | RList [RType _; e1] -> EType (parse_univ e1)
  | RList (((RId _ | RList _) as e0)::es) ->
    (
      match List.rev es with
      | (RList ((RDecreases _)::_))::(RList ((RAttributes _)::_))::es_rev
      | (RList ((RAttributes _)::_))::es_rev ->
        (
          let es = List.rev es_rev in
          match es with
          | e1::es -> EComp (parse_exp e0, parse_exp e1, parse_comma_exps es)
          | _ -> err ("parse_exp: EComp: " + string_of_raw_exp e)
        )
      | _ -> EApp (parse_exp e0, List.map parse_aqual_exp es)
    )
  | RList [RLtGt _; e; RList us] -> EAppUnivs (parse_exp e, parse_univs us)
  | RList [RArrow _; RList ((RMatch _)::_); _] -> EUnsupported "MATCH"
  | RList [RArrow _; e1; e2] ->
      let (a, e1) = get_aqual e1 in
      let (x, e1) =
        match e1 with
        | RList [RColon _; RId (_, x); e1] -> (parse_id x, e1)
        | _ -> err ("parse_exp: RArrow: " + string_of_raw_exp e)
        in
      EArrow (a, x, parse_exp e1, parse_exp e2)
  | RList [RColon _; e1; RId (_, ("Tm_type" | "Tm_delayed-resolved"))] -> parse_exp e1
  | RList [RColon _; e1; RList [RColon _; RId (_, "Tm_name"); _]] -> parse_exp e1
  | RList [RColon _; e1; e2] -> ETyped (parse_exp e1, parse_exp e2)
  | RList [RRefine _; RList [RColon _; RId (_, x); e1]; e2] -> ERefine (parse_id x, parse_exp e1, parse_exp e2)
  | RList [RAscribed _; e1; e2] -> ETyped (parse_exp e1, parse_exp e2)
  | RList [RPattern _; RList pats; e] ->
      let f (e:raw_exp):exp list =
        match e with
        | RList es -> List.map parse_exp es
        | _ -> err ("parse_exp: EPattern: " + string_of_raw_exp e)
        in
      EPattern (List.map f pats, parse_exp e)
  | RList [RLet _; b; e1; e2] -> ELet (parse_binder b, parse_exp e1, parse_exp e2)
  | RList [RFun _; RList bs; e] -> EFun (List.map parse_binder bs, parse_exp e)
//  | RList (e::_) | e -> EUnsupported (string_of_raw_exp e)
  | e -> EUnsupported (string_of_raw_exp e)
and parse_aqual_exp (e:raw_exp):(aqual * exp) =
  let (a, e) = get_aqual e in
  (a, parse_exp e)
and parse_comma_exps (es:raw_exp list):exp list =
  match es with
  | [] -> []
  | [e] -> [parse_exp e]
  | e::(RComma _)::es -> (parse_exp e)::(parse_comma_exps es)
  | _ -> err ("parse_comma_exps: " + string_of_raw_exp (RList es))
and parse_binder (e:raw_exp):binder = parse_binder_a Explicit e
and parse_binder_a (a:aqual) (e:raw_exp):binder =
  match e with
  | RId (_, x) -> (a, parse_id x, None)
  | RList [RColon _; RId (_, x); t] -> (a, parse_id x, Some (parse_exp t))
  | RList [RDollar _; b] -> parse_binder_a Equality b
  | RList [(RHash _ | RHashDot _); b] -> parse_binder_a Implicit b
  | _ -> err ("parse_binder: " + string_of_raw_exp e)

let parse_qualifier (e:raw_exp):string =
  match e with
  | RQualifier (_, s) -> s
  | _ -> err ("parse_qualifier: " + string_of_raw_exp e)

let parse_decl (e:raw_exp):decl =
  match e with
  | RList [RList quals; RDecl (_, category); RId (_, x); RList udecls; RList bs; t; body_opt] ->
      {
        d_name = x;
        d_qualifiers = List.map parse_qualifier quals
        d_category = category;
        d_udecls = List.map parse_rid udecls;
        d_binders = List.map parse_binder bs;
        d_typ = parse_exp t;
        d_body = match body_opt with RList [] -> None | RList [e] -> Some (parse_exp e) | _ -> err ("body: " + string_of_raw_exp body_opt);
      }
  | _ -> err ("parse_decl: " + string_of_raw_exp e)

let filter_decls (ds:decl list):decl list =
  let filter_decl_pair ((d:decl option), (dnext:decl option)):decl list =
    // turn "val f ... let f ..." into "let f..."
    match (d, dnext) with
    | (None, _) -> []
    | (Some d, None) -> [d]
    | (Some d, Some dnext) when d.d_name = dnext.d_name -> []
    | (Some d, Some _) -> [d]
    in
  let filter_decl (d:decl):bool =
    List.forall (fun x -> x <> "private") d.d_qualifiers
    in
  let abstract_decl (d:decl):decl =
    if List.forall (fun x -> x <> "abstract") d.d_qualifiers then d else
    {d with d_body = None}
    in
  let ds_opt = List.map Some ds in
  let ds_prev = ds_opt @ [None] in
  let ds_next = None::ds_opt in
  let ds = List.collect filter_decl_pair (List.zip ds_prev ds_next) in
  let ds = List.filter filter_decl ds in
  let ds = List.map abstract_decl ds in
  ds

let rec unsupported (e:exp):string option =
  match e with
  | EId x -> None
  | EInt i -> None
  | EUnitValue -> None
  | EType u -> None
  | EComp (e1, e2, es) -> exps_unsupported (e1::e2::es)
  | EApp (e, aes) -> exps_unsupported (e::(List.map snd aes))
  | EAppUnivs (e, us) -> unsupported e
  | EArrow (a, x, e1, e2) -> exps_unsupported [e1; e2]
  | ETyped (e1, e2) -> exps_unsupported [e1; e2]
  | ERefine (x, e1, e2) -> exps_unsupported [e1; e2]
  | EAscribed (e1, e2) -> exps_unsupported [e1; e2]
  | EPattern (pats, e) ->
      let upats = list_unsupported (List.map exps_unsupported pats) in
      list_unsupported [upats; unsupported e]
  | ELet (b, e1, e2) -> exps_unsupported (e1::e2::(binder_exps b))
  | EFun (bs, e) -> exps_unsupported (e::(List.collect binder_exps bs))
  | EUnsupported s -> Some s
and binder_exps (b:binder):exp list =
  let (_, _, e_opt) = b in
  list_opt e_opt (fun e -> [e])
and exps_unsupported (es:exp list):string option =
  list_unsupported (List.map unsupported es)
and list_unsupported (ss:string option list):string option =
  match ss with
  | [] -> None
  | (Some s)::_ -> Some s
  | None::t -> list_unsupported t

let decl_unsupported (d:decl):decl =
  let u_binders = exps_unsupported (List.collect binder_exps d.d_binders) in
  let u_typ = unsupported d.d_typ in
  let body_opt =
    match d.d_body with
    | None -> None
    | Some e -> match unsupported e with None -> Some e | Some _ -> None
    in
  let d = {d with d_body = body_opt} in
  let u = list_unsupported [u_binders; u_typ] in
  match u with
  | None -> d
  | Some s -> {d with d_udecls = []; d_binders = []; d_typ = EUnsupported s; d_body = None}

let rec index_to_var_id (xs:string list) (id:id):id =
  match id with
  | {index = None} -> id
  | {name = Some x; index = Some i} ->
      let y = List.item i xs in
      if y.StartsWith(x) then {name = Some y; index = None}
      else err ("expected variable " + x + " but found " + y)
  | {name = None; index = Some i} ->
      {name = Some (List.item i xs); index = None}

let rec index_to_var_univ (xs:string list) (u:univ):univ =
  let r = index_to_var_univ xs in
  match u with
  | UId x -> UId (index_to_var_id xs x)
  | UInt _ -> u
  | UPlus (u1, u2) -> UPlus (r u1, r u2)
  | UMax us -> UMax (List.map r us)

let rec index_to_var_exp (xs:string list) (e:exp):exp =
  let r = index_to_var_exp xs in
  match e with
  | EArrow (_, {name = None}, _, _)
  | EArrow (_, {index = Some _}, _, _)
  | ERefine ({name = None}, _, _)
  | ERefine ({index = Some _}, _, _) ->
      err ("index_to_var_exp: " + string_of_exp e)
  | EId x -> EId (index_to_var_id xs x)
  | EInt i -> e
  | EUnitValue -> e
  | EType u -> EType (index_to_var_univ xs u)
  | EComp (e1, e2, es) -> EComp (r e1, r e2, List.map r es)
  | EApp (e, aes) -> EApp (r e, List.map (fun (a, e) -> (a, r e)) aes)
  | EAppUnivs (e, us) -> EAppUnivs (r e, List.map (index_to_var_univ xs) us)
  | EArrow (a, ({name = Some x; index = None} as id), e1, e2) ->
      EArrow (a, id, r e1, index_to_var_exp (x::xs) e2)
  | ETyped (e1, e2) -> ETyped (r e1, r e2)
  | ERefine (({name = Some x; index = None} as id), e1, e2) ->
      ERefine (id, r e1, index_to_var_exp (x::xs) e2)
  | EAscribed (e1, e2) -> EAscribed (r e1, r e2)
  | EPattern (pats, e) -> EPattern (List.map (List.map r) pats, r e)
  | ELet (b, e1, e2) ->
      let (xs, bs) = index_to_var_binders xs [b] in
      ELet (List.head bs, r e1, index_to_var_exp xs e2)
  | EFun (bs, e) ->
      let (xs, bs) = index_to_var_binders xs bs in
      EFun (bs, index_to_var_exp xs e)
  | EUnsupported s -> e
and index_to_var_binders (xs:string list) (bs:binder list):(string list * binder list) =
  match bs with
  | [] -> (xs, bs)
  | (_, {name = None}, _)::_
  | (_, {index = Some _}, _)::_ ->
      err ("index_to_var_binders: " + string_of_tree (st_paren (trees_of_binders bs)))
  | (a, ({name = Some x; index = None} as id), e_opt)::bs ->
      let e_opt = Option.map (index_to_var_exp xs) e_opt in
      let (xs, bs) = index_to_var_binders (x::xs) bs in
      (xs, (a, id, e_opt)::bs)

let index_to_var_decl (d:decl):decl =
  let id_name (id:id):string =
    match id with
    | {name = None}
    | {index = Some _} -> err ("index_to_var_decl: " + d.d_name)
    | {name = Some x} -> x
    in
  try
    let xs = List.rev (List.map id_name d.d_udecls) in
    let (xs, bs) = index_to_var_binders xs d.d_binders in
    let t = index_to_var_exp xs d.d_typ in
    let body = Option.map (index_to_var_exp xs) d.d_body in
    {d with d_binders = bs; d_typ = t; d_body = body}
  with
  | :? System.ArgumentException ->
      {d with d_binders = []; d_typ = EUnsupported "bad variable index"; d_body = None}

let rec universe0_univ_int (u:univ):bigint =
  let r = universe0_univ_int in
  match u with
  | UId x -> bigint.Zero
  | UInt i -> i
  | UPlus (u1, u2) -> r u1 + r u2
  | UMax [] -> err "universe0_univ_int"
  | UMax us -> List.fold (fun i j -> if i > j then i else j) bigint.Zero (List.map r us)

let universe0_univ (u:univ):univ =
  UInt (universe0_univ_int u)

let rec universe0_exp (e:exp):exp =
  let r = universe0_exp in
  match e with
  | EId _ | EInt _ | EUnitValue | EUnsupported _ -> e
  | EType u -> EType (universe0_univ u)
  | EComp (e1, e2, es) -> EComp (r e1, r e2, List.map r es)
  | EApp (e, aes) -> EApp (r e, List.map (fun (a, e) -> (a, r e)) aes)
  | EAppUnivs (e, us) -> EAppUnivs (r e, List.map universe0_univ us)
  | EArrow (a, x, e1, e2) -> EArrow (a, x, r e1, r e2)
  | ETyped (e1, e2) -> ETyped (r e1, r e2)
  | ERefine (x, e1, e2) -> ERefine (x, r e1, r e2)
  | EAscribed (e1, e2) -> EAscribed (r e1, r e2)
  | EPattern (pats, e) -> EPattern (List.map (List.map r) pats, r e)
  | ELet (b, e1, e2) -> ELet (universe0_binder b, r e1, r e2)
  | EFun (bs, e) -> EFun (List.map universe0_binder bs, r e)
and universe0_binder (b:binder):binder =
  let (a, x, e_opt) = b in
  (a, x, Option.map universe0_exp e_opt)

let universe0_decl (d:decl):decl =
  let bs = List.map universe0_binder d.d_binders in
  let t = universe0_exp d.d_typ in
  let body = Option.map universe0_exp d.d_body in
  {d with d_udecls = []; d_binders = bs; d_typ = t; d_body = body}

type env = Map<string, decl>

let rec is_vale_type (leftmost:bool) (env:env) (e:exp):bool =
  let r = is_vale_type false env in
  match e with
  | EId _ -> is_vale_type_id env e
  | EApp (EAppUnivs (EId {name = Some ("Tot" | "GTot")}, _), [(_, e)]) -> r e
  | EApp (EId {name = Some ("Tot" | "GTot")}, [(_, e)]) -> r e
  | EApp (e, aes) -> is_vale_type_id env e && List.forall r (List.map snd aes)
  | EAppUnivs (e, _) -> r e
  | EArrow (_, {name = Some x}, ((EType (UInt i)) as t), e2) when leftmost && i.Equals(bigint.Zero) ->
      let dx = { d_name = x; d_qualifiers = []; d_category = "type"; d_udecls = []; d_binders = []; d_typ = t; d_body = None} in
      let env = Map.add x dx env in
      is_vale_type true env e2
  | EArrow (Explicit, _, e1, e2) -> r e1 && r e2
  // TODO: more cases
  | _ -> false
and is_vale_type_id (env:env) (e:exp):bool =
  match e with
  | EId {name = Some x} ->
    (
      match Map.tryFind x env with
      // REVIEW: is any "type" ok?
      | Some { d_category = "type" } -> true
      | _ ->
          //printfn "failed: %s" x;
          false
    )
  | EAppUnivs (e, _) -> is_vale_type_id env e
  | _ -> false

let to_vale_decl ((env:env), (ds_rev:decl list)) (d:decl):(env * decl list) =
  let d = universe0_decl d in
  let bs = d.d_binders in
  let bs_are_Type = List.forall (fun (_, _, e) -> match e with (Some (EType _)) -> true | _ -> false) bs in
  let ds = 
    match (bs, d.d_typ) with
    | (_, EType _) when bs_are_Type ->
        // TODO: keep body in some cases
        [{d with d_category = "type"; d_body = None}]
    | ([], _) when is_vale_type true env d.d_typ ->
        [{d with d_category = "val"; d_body = None}]
    | _ -> []
    in
  let env = List.fold (fun env d -> Map.add d.d_name d env) env ds in
  let ds_rev = ds @ ds_rev in
  (env, ds_rev)

let rec take_arrows (e:exp):(exp list * exp) =
  match e with
  | EArrow (_, _, e1, e2) ->
      let (es, e) = take_arrows e2 in
      (e1::es, e)
  | _ -> ([], e)

let rec trees_of_comma_list (es:string_tree list):string_tree list =
  match es with
  | [] -> []
  | [e] -> [e]
  | e::es -> (st_list [e; st_leaf ","])::(trees_of_comma_list es)

let tree_of_vale_id (id:id):string_tree =
  match id with
  | {name = Some x} ->
      let x = if x.StartsWith("'") then "_" + x else x in
      st_leaf (x.Replace("#", "_"))
  | _ -> err ("internal error: tree_of_vale_id: " + string_of_id id)

let rec tree_of_vale_exp (e:exp):string_tree =
  let r = tree_of_vale_exp in
  match e with
  | EId id -> tree_of_vale_id id
  | EInt i -> st_leaf (i.ToString())
  | EUnitValue -> st_leaf "tuple()"
  | EType (UInt i) -> st_leaf ("Type(" + i.ToString() + ")")
  | EApp (EAppUnivs (EId {name = Some ("Tot" | "GTot")}, _), [(_, e)]) -> r e
  | EApp (EId {name = Some ("Tot" | "GTot")}, [(_, e)]) -> r e
  | EApp (e, aes) ->
      // TODO: tuples are special case
      st_paren [r e; st_paren (trees_of_comma_list (List.map (snd >> r) aes))]
  | EAppUnivs (e, _) -> r e
  | EArrow _ ->
      let (es, e) = take_arrows e in
      st_paren [st_leaf "fun"; st_paren (trees_of_comma_list (List.map r es)); st_leaf "->"; r e]
  | _ -> err ("internal error: tree_of_vale_exp: " + string_of_exp e)

let rec take_params (e:exp):(binder list * exp) =
  match e with
  | EArrow (a, x, t, e2) ->
      let b = (a, x, Some t) in
      let (bs, e) = take_params e2 in
      (b::bs, e)
  | _ -> ([], e)

let tree_of_vale_decl (d:decl):string_tree =
  match d.d_category with
  | "type" ->
    (
      let ps =
        match d.d_binders with
        | [] -> []
        | bs ->
            let f (_, x, k) = st_list [tree_of_vale_id x; st_leaf ":"; tree_of_vale_exp (Option.get k)] in
            [st_paren (trees_of_comma_list (List.map f bs))]
        in
      let typing = [st_leaf ":"; tree_of_vale_exp d.d_typ; st_leaf "extern"; st_leaf ";"] in
      st_list ([st_leaf "type"; st_leaf d.d_name] @ ps @ typing)
    )
  | "val" ->
    (
      let (bs, t) = take_params d.d_typ in
      let (prefix, ps) =
        match bs with
        | [] -> ("const", [])
        | _ ->
            let f (a, x, t) =
              let tree_a = match a with Explicit -> [] | _ -> [st_leaf "#"] in
              st_list (tree_a @ [tree_of_vale_id x; st_leaf ":"; tree_of_vale_exp (Option.get t)])
              in
            ("function", [st_paren (trees_of_comma_list (List.map f bs))])
        in
      let typing = [st_leaf ":"; tree_of_vale_exp t; st_leaf "extern"; st_leaf ";"] in
      st_list ([st_leaf prefix; st_leaf d.d_name] @ ps @ typing)
    )
  | _ -> err ("internal error: tree_of_vale_decl: " + d.d_category)

let main (argv:string array) =
  let in_files_rev = ref ([]:string list) in
  let outfile = ref (None:string option) in
  let lexbufOpt = ref (None:LexBuffer<byte> option)
  let arg_list = argv |> Array.toList in
  let print_err (err:string):unit =
    printfn "Error:";
    match !lexbufOpt with
    | None -> printfn "%s" err;
    | Some lexbuf ->
        printfn "%s" err;
        printfn "\nerror at line %i column %i of string\n%s" (line lexbuf) (col lexbuf) (file lexbuf);
        exit 1
    in
  try
  (
    let parse_argv (args:string list) =
      let rec match_args (arg_list:string list) =
        match arg_list with
        | "-in" :: l ->
          match l with
          | [] -> failwith "Specify input file"
          | f :: l -> in_files_rev := f::!in_files_rev; match_args l
        | "-out" :: l ->
          match l with
          | [] -> failwith "Specify output file"
          | f :: l -> outfile := Some f; match_args l
        | f :: l ->
            failwith ("Unrecognized argument: " + f + "\n")
        | [] -> if List.length !in_files_rev = 0 then failwith "Use -in to specify input file"
        in
        match_args args
      in
    parse_argv (List.tail arg_list);
    let in_files = List.rev (!in_files_rev) in
    let read_file (name:string) =
      let lines = Array.toList (System.IO.File.ReadAllLines(name)) in
      let rec get_block (lines:string list):(string list * string list) =
        match lines with
        | [] -> ([], [])
        | h::t when h.Trim() = "" -> ([], lines)
        | h::t when h.StartsWith("#") -> ([], lines)
        | h::t ->
            let (b, lines) = get_block t in
            (h::b, lines)
        in
      let rec get_blocks (lines:string list) (blocks_rev:string list):string list =
        match lines with
        | [] -> List.rev blocks_rev
        | line::lines ->
            if line.StartsWith("Checked: ") then
              if line.Trim() = "Checked:" then get_blocks lines blocks_rev else
              let line = line.Substring("Checked: ".Length) in
              let (block, lines) = get_block (line::lines) in
              let block = String.concat " " block in
              get_blocks lines (block::blocks_rev)
            else
              get_blocks lines blocks_rev
        in
      get_blocks lines []
      in
    let blocks = List.collect read_file in_files in
    let parse_block (s:string):decl list =
      //printfn "%s" s;
      let bytes = System.Text.Encoding.UTF8.GetBytes(s) in
      let lexbuf = LexBuffer<byte>.FromBytes bytes in
      setInitialPos lexbuf s;
      lexbufOpt := Some lexbuf;
      let res = Parse.start Lex.lextoken lexbuf in
      lexbufOpt := None;
      //List.iter (fun e -> print_tree "" (tree_of_raw_exp e)) res;
      let res = List.map simplify_raw_exp res in
      let ds = List.map parse_decl res in
      let ds =
        match ds with
        | d::_ when List.exists (fun s -> s = "abstract") d.d_qualifiers -> [d]
        | _ -> ds
        in
      ds
      in
    let ds = List.collect parse_block blocks in
    let ds = filter_decls ds in
    let ds = List.map decl_unsupported ds in
    let ds = List.map index_to_var_decl ds in
    let (env, ds_rev) = List.fold to_vale_decl (Map.empty, []) ds in
    let ds = List.rev ds_rev in
    let duplicates = ref (Map.empty : Map<string, unit>) in
    List.iter
      (fun d ->
        if not (Map.containsKey d.d_name (!duplicates)) then
         (
          // REVIEW: why are there duplicates?
          print_tree "" (tree_of_vale_decl d); printfn "";
          duplicates := Map.add d.d_name () (!duplicates)
         )
      )
      ds;
    ()
  )
  with
  | Err err -> print_err err
  | ParseErr err -> print_err err
  | Failure err -> print_err err
  | err -> print_err (err.ToString ())

let () = main (System.Environment.GetCommandLineArgs ())
