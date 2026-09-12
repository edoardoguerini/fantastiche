import { createSerializationAdapter } from '@tanstack/react-router'
import { ApiError } from './error'

// Trasferire solo i campi UX: nessuno stack o payload interno nell'HTML.
export const apiErrorAdapter = createSerializationAdapter({
  key: 'FantasticheApiError',
  test: (value): value is ApiError => value instanceof ApiError,
  toSerializable: (error) => ({
    status: error.status,
    code: error.code,
    message: error.message,
  }),
  fromSerializable: (value) =>
    new ApiError(value.status, value.code, value.message),
})
