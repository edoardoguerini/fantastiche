import { mutationOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { authenticatedUserSchema } from '../types/auth.types'
import type { LoginValues } from '../validations/auth.validations'

export const loginMutationOptions = () =>
  mutationOptions({
    mutationFn: async (values: LoginValues) =>
      authenticatedUserSchema.parse(await api.post('/Auth/Login', values)),
    retry: false,
  })
export const logoutMutationOptions = () =>
  mutationOptions({
    mutationFn: () => api.post('/Auth/Logout', {}),
    retry: false,
  })
