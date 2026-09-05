/** API-05 error envelope returned on every non-2xx response. */
export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    correlation_id?: string
    tenant_id?: string
    details?: Array<{ field: string; code: string; message: string }>
  }
  /** Present on 429 (rate limit) responses — seconds until retry is allowed. */
  retryAfter?: number
}
