import { z } from 'zod'

export const loginSchema = z.object({
  email: z
    .email('Inserisci un indirizzo email valido.')
    .max(256, 'L’indirizzo email è troppo lungo.'),
  password: z
    .string()
    .min(1, 'Inserisci la password.')
    .max(1024, 'La password è troppo lunga.'),
})
export type LoginValues = z.infer<typeof loginSchema>
