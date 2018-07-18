module Vale_reverse_bytes_quad32_buffer_win

open X64.Machine_s
open X64.Memory_i
open X64.Vale.State_i
open X64.Vale.Decls_i
open Types_s
open X64.Util

val va_code_reverse_bytes_quad32_buffer_win: unit -> va_code
let va_code_reverse_bytes_quad32_buffer_win = va_code_reverse_bytes_quad32_buffer_win

  //va_pre and va_post should correspond to the pre- and postconditions generated by Vale
let va_pre (va_b0:va_code) (va_s0:va_state) (stack_b:buffer64)
(b:buffer128)  =
  ((va_require_total va_b0 (va_code_reverse_bytes_quad32_buffer_win ()) va_s0) /\
    (va_get_ok va_s0) /\ (valid_taint_buf128 b (va_get_mem va_s0) (va_get_memTaint va_s0) Secret)
    /\ (locs_disjoint [(loc_buffer stack_b); (loc_buffer b)]) /\ (buffer_readable (va_get_mem
    va_s0) stack_b) /\ (buffer_readable (va_get_mem va_s0) b) /\ (buffer_length stack_b) >= 2 /\
    (valid_stack_slots (va_get_mem va_s0) (va_get_reg Rsp va_s0) stack_b 0 (va_get_memTaint va_s0)) /\ (va_get_reg Rcx
    va_s0) == (buffer_addr b (va_get_mem va_s0)) /\ (buffer_length b) == 1)

let va_post (va_b0:va_code) (va_s0:va_state) (va_sM:va_state) (va_fM:va_fuel) (stack_b:buffer64)
(b:buffer128)  =
   ((va_ensure_total va_b0 va_s0 va_sM va_fM) /\ (va_get_ok va_sM)
    /\ (va_get_reg Rbx va_sM) == (va_get_reg Rbx va_s0) /\ (va_get_reg Rbp va_sM) == (va_get_reg
    Rbp va_s0) /\ (va_get_reg Rdi va_sM) == (va_get_reg Rdi va_s0) /\ (va_get_reg Rsi va_sM) ==
    (va_get_reg Rsi va_s0) /\ (va_get_reg Rsp va_sM) == (va_get_reg Rsp va_s0) /\ (va_get_reg R12
    va_sM) == (va_get_reg R12 va_s0) /\ (va_get_reg R13 va_sM) == (va_get_reg R13 va_s0) /\
    (va_get_reg R14 va_sM) == (va_get_reg R14 va_s0) /\ (va_get_reg R15 va_sM) == (va_get_reg R15
    va_s0) /\ (va_get_xmm 6 va_sM) == (va_get_xmm 6 va_s0) /\ (va_get_xmm 7 va_sM) == (va_get_xmm 7
    va_s0) /\ (va_get_xmm 8 va_sM) == (va_get_xmm 8 va_s0) /\ (va_get_xmm 9 va_sM) == (va_get_xmm 9
    va_s0) /\ (va_get_xmm 10 va_sM) == (va_get_xmm 10 va_s0) /\ (va_get_xmm 11 va_sM) ==
    (va_get_xmm 11 va_s0) /\ (va_get_xmm 12 va_sM) == (va_get_xmm 12 va_s0) /\ (va_get_xmm 13
    va_sM) == (va_get_xmm 13 va_s0) /\ (va_get_xmm 14 va_sM) == (va_get_xmm 14 va_s0) /\
    (va_get_xmm 15 va_sM) == (va_get_xmm 15 va_s0) /\ (modifies_buffer128 b (va_get_mem va_s0)
    (va_get_mem va_sM)) /\ (buffer128_read b 0 (va_get_mem va_sM)) == (reverse_bytes_quad32
    (buffer128_read b 0 (va_get_mem va_s0))) /\ (va_state_eq va_sM (va_update_trace va_sM
    (va_update_mem va_sM (va_update_flags va_sM (va_update_xmm 15 va_sM (va_update_xmm 14 va_sM
    (va_update_xmm 13 va_sM (va_update_xmm 12 va_sM (va_update_xmm 11 va_sM (va_update_xmm 10 va_sM
    (va_update_xmm 9 va_sM (va_update_xmm 8 va_sM (va_update_xmm 7 va_sM (va_update_xmm 6 va_sM
    (va_update_xmm 5 va_sM (va_update_xmm 4 va_sM (va_update_xmm 3 va_sM (va_update_xmm 2 va_sM
    (va_update_xmm 1 va_sM (va_update_xmm 0 va_sM (va_update_reg R15 va_sM (va_update_reg R14 va_sM
    (va_update_reg R13 va_sM (va_update_reg R12 va_sM (va_update_reg R11 va_sM (va_update_reg R10
    va_sM (va_update_reg R9 va_sM (va_update_reg R8 va_sM (va_update_reg Rsp va_sM (va_update_reg
    Rbp va_sM (va_update_reg Rdi va_sM (va_update_reg Rsi va_sM (va_update_reg Rdx va_sM
    (va_update_reg Rcx va_sM (va_update_reg Rbx va_sM (va_update_reg Rax va_sM (va_update_ok va_sM
    va_s0))))))))))))))))))))))))))))))))))))))

val va_lemma_reverse_bytes_quad32_buffer_win(va_b0:va_code) (va_s0:va_state) (stack_b:buffer64)
(b:buffer128) : Ghost ((va_sM:va_state) * (va_fM:va_fuel))
  (requires va_pre va_b0 va_s0 stack_b b )
  (ensures (fun (va_sM, va_fM) -> va_post va_b0 va_s0 va_sM va_fM stack_b b ))
let va_lemma_reverse_bytes_quad32_buffer_win = va_lemma_reverse_bytes_quad32_buffer_win
