import { z } from 'zod'

export const inviteSchema = z.object({
  displayName: z
    .string()
    .trim()
    .min(1, 'Inserisci il nome.')
    .max(150, 'Usa al massimo 150 caratteri.'),
  email: z
    .string()
    .trim()
    .email('Inserisci un indirizzo email valido.')
    .max(256),
})
export function acceptanceFormSchema(
  requiresTeam: boolean,
  requiresPassword: boolean,
) {
  const password = z
    .string()
    .min(12, 'Usa almeno 12 caratteri.')
    .max(128, 'Usa al massimo 128 caratteri.')
    .regex(/[a-z]/, 'Aggiungi una lettera minuscola.')
    .regex(/[A-Z]/, 'Aggiungi una lettera maiuscola.')
    .regex(/[0-9]/, 'Aggiungi un numero.')
    .regex(/[^a-zA-Z0-9]/, 'Aggiungi un simbolo.')
  return z
    .object({
      teamName: requiresTeam
        ? z
            .string()
            .trim()
            .min(1, 'Scegli il nome della squadra.')
            .max(100, 'Usa al massimo 100 caratteri.')
        : z.string(),
      password: requiresPassword ? password : z.string(),
      confirmPassword: z.string(),
    })
    .refine(
      (value) => !requiresPassword || value.password === value.confirmPassword,
      { path: ['confirmPassword'], message: 'Le password non coincidono.' },
    )
}
export function readInvitationToken(url: string) {
  const parsed = new URL(url)
  const token =
    new URLSearchParams(parsed.hash.slice(1)).get('token') ??
    parsed.searchParams.get('token')
  return token && /^[0-9a-f]{64}$/i.test(token) ? token : null
}
