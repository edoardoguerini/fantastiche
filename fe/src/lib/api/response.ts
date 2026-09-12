import { z } from 'zod'
import { ApiError } from './error'

const envelopeSchema = z.object({
  isSuccess: z.boolean(),
  data: z.unknown(),
  errors: z.array(z.object({ code: z.string(), message: z.string() })),
})

export async function parseApiResponse<T>(response: Response): Promise<T> {
  const parsed = envelopeSchema.safeParse(
    await response.json().catch(() => null),
  )
  if (!response.ok || !parsed.success || !parsed.data.isSuccess) {
    const firstError = parsed.success ? parsed.data.errors[0] : undefined
    throw new ApiError(
      response.status,
      firstError?.code ?? 'api.invalid_response',
      firstError?.message ??
        'Il servizio ha restituito una risposta inattesa. Riprova tra poco.',
      parsed.success ? parsed.data.data : undefined,
    )
  }
  return parsed.data.data as T
}
