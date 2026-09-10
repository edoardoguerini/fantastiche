import { z } from 'zod'

export const authenticatedUserSchema = z.object({
  id: z.string(),
  email: z.string(),
  displayName: z.string(),
  isSuperAdmin: z.boolean(),
})
export type AuthenticatedUser = z.infer<typeof authenticatedUserSchema>
