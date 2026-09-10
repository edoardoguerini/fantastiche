import { z } from 'zod'

const requiredText = (max: number) =>
  z
    .string()
    .trim()
    .min(1, 'Compila questo campo.')
    .max(max, `Usa al massimo ${max} caratteri.`)
const slots = z
  .number('Inserisci un numero.')
  .int('Inserisci un numero intero.')
  .min(0, 'Il valore minimo è 0.')
  .max(100, 'Il valore massimo è 100.')

export const createLeagueSchema = z
  .object({
    name: requiredText(100),
    seasonName: requiredText(50),
    organizerName: requiredText(150),
    organizerEmail: requiredText(256).email('Inserisci un’email valida.'),
    budget: z
      .number('Inserisci un numero.')
      .int('Inserisci un numero intero.')
      .min(1, 'Il budget minimo è 1 credito.')
      .max(1_000_000, 'Il budget massimo è 1.000.000 di crediti.'),
    goalkeepers: slots,
    defenders: slots,
    midfielders: slots,
    forwards: slots,
  })
  .superRefine((value, context) => {
    const total =
      value.goalkeepers + value.defenders + value.midfielders + value.forwards
    if (total < 1 || total > 100)
      context.addIssue({
        code: 'custom',
        path: ['forwards'],
        message: 'La rosa deve contenere da 1 a 100 giocatori.',
      })
    if (value.budget < total)
      context.addIssue({
        code: 'custom',
        path: ['budget'],
        message: 'Prevedi almeno un credito per ogni giocatore in rosa.',
      })
  })

export type CreateLeagueValues = z.infer<typeof createLeagueSchema>
