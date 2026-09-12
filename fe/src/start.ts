import { createMiddleware, createStart } from '@tanstack/react-start'
import { apiErrorAdapter } from '@/lib/api/error-serialization'

const privateResponses = createMiddleware().server(async ({ next }) => {
  const result = await next()
  // Include redirect ed errori, anche quando non si arriva al rendering.
  result.response.headers.set('Cache-Control', 'private, no-store')
  return result
})

export const startInstance = createStart(() => ({
  requestMiddleware: [privateResponses],
  serializationAdapters: [apiErrorAdapter],
}))
