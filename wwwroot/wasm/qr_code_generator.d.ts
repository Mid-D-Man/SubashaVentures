/* tslint:disable */
/* eslint-disable */
export function generate_qr_code(data: string, size: number, dark_color: string, light_color: string): string;
export function generate_enhanced_qr_code(data: string, size: number, dark_color: string, light_color: string, error_level?: string | null, logo_url?: string | null, use_gradient?: boolean | null, gradient_direction?: string | null, gradient_color1?: string | null, gradient_color2?: string | null, margin?: number | null): string;
export function get_supported_error_levels(): string;
export function get_supported_gradient_directions(): string;
export class QrCodeError {
  private constructor();
  free(): void;
  readonly message: string;
}

export type InitInput = RequestInfo | URL | Response | BufferSource | WebAssembly.Module;

export interface InitOutput {
  readonly memory: WebAssembly.Memory;
  readonly __wbg_qrcodeerror_free: (a: number, b: number) => void;
  readonly qrcodeerror_message: (a: number) => [number, number];
  readonly generate_qr_code: (a: number, b: number, c: number, d: number, e: number, f: number, g: number) => [number, number, number, number];
  readonly generate_enhanced_qr_code: (a: number, b: number, c: number, d: number, e: number, f: number, g: number, h: number, i: number, j: number, k: number, l: number, m: number, n: number, o: number, p: number, q: number, r: number, s: number) => [number, number, number, number];
  readonly get_supported_error_levels: () => [number, number];
  readonly get_supported_gradient_directions: () => [number, number];
  readonly __wbindgen_export_0: WebAssembly.Table;
  readonly __wbindgen_free: (a: number, b: number, c: number) => void;
  readonly __wbindgen_malloc: (a: number, b: number) => number;
  readonly __wbindgen_realloc: (a: number, b: number, c: number, d: number) => number;
  readonly __externref_table_dealloc: (a: number) => void;
  readonly __wbindgen_start: () => void;
}

export type SyncInitInput = BufferSource | WebAssembly.Module;
/**
* Instantiates the given `module`, which can either be bytes or
* a precompiled `WebAssembly.Module`.
*
* @param {{ module: SyncInitInput }} module - Passing `SyncInitInput` directly is deprecated.
*
* @returns {InitOutput}
*/
export function initSync(module: { module: SyncInitInput } | SyncInitInput): InitOutput;

/**
* If `module_or_path` is {RequestInfo} or {URL}, makes a request and
* for everything else, calls `WebAssembly.instantiate` directly.
*
* @param {{ module_or_path: InitInput | Promise<InitInput> }} module_or_path - Passing `InitInput` directly is deprecated.
*
* @returns {Promise<InitOutput>}
*/
export default function __wbg_init (module_or_path?: { module_or_path: InitInput | Promise<InitInput> } | InitInput | Promise<InitInput>): Promise<InitOutput>;
