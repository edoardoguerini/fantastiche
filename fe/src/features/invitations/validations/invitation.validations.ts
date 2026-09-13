import { z } from 'zod'

export const inviteSchema = z.object({
  email: z
    .string()
    .trim()
    .email('Inserisci un indirizzo email valido.')
    .max(256),
})

export type PasswordRuleId = 'length' | 'upper' | 'lower' | 'digit' | 'symbol'

// Le stesse regole alimentano la checklist visibile e la validazione al submit.
export const passwordRules: {
  id: PasswordRuleId
  label: string
  message: string
  test: (value: string) => boolean
}[] = [
  {
    id: 'length',
    label: 'Almeno 12 caratteri',
    message: 'Usa almeno 12 caratteri.',
    test: (value) => value.length >= 12,
  },
  {
    id: 'upper',
    label: 'Una maiuscola',
    message: 'Aggiungi una maiuscola.',
    test: (value) => /[A-Z]/.test(value),
  },
  {
    id: 'lower',
    label: 'Una minuscola',
    message: 'Aggiungi una minuscola.',
    test: (value) => /[a-z]/.test(value),
  },
  {
    id: 'digit',
    label: 'Un numero',
    message: 'Aggiungi un numero.',
    test: (value) => /[0-9]/.test(value),
  },
  {
    id: 'symbol',
    label: 'Un simbolo',
    message: 'Aggiungi un simbolo.',
    test: (value) => /[^a-zA-Z0-9]/.test(value),
  },
]

export function passwordRuleStatus(value: string) {
  return Object.fromEntries(
    passwordRules.map((rule) => [rule.id, rule.test(value)]),
  ) as Record<PasswordRuleId, boolean>
}

export function acceptanceFormSchema(
  requiresTeam: boolean,
  requiresActivation: boolean,
) {
  const password = z
    .string()
    .max(128, 'Usa al massimo 128 caratteri.')
    .superRefine((value, ctx) => {
      for (const rule of passwordRules)
        if (!rule.test(value))
          ctx.addIssue({ code: 'custom', message: rule.message })
    })
  return z.object({
    displayName: requiresActivation
      ? z
          .string()
          .trim()
          .min(1, 'Inserisci il nome.')
          .max(150, 'Usa al massimo 150 caratteri.')
      : z.string(),
    teamName: requiresTeam
      ? z
          .string()
          .trim()
          .min(1, 'Scegli il nome della squadra.')
          .max(100, 'Usa al massimo 100 caratteri.')
      : z.string(),
    password: requiresActivation ? password : z.string(),
  })
}

export function readInvitationToken(url: string) {
  const parsed = new URL(url)
  const token =
    new URLSearchParams(parsed.hash.slice(1)).get('token') ??
    parsed.searchParams.get('token')
  return token && /^[0-9a-f]{64}$/i.test(token) ? token : null
}
