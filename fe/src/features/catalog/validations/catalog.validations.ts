import { z } from 'zod'
export const importCatalogSchema = z.object({
  seasonName: z
    .string()
    .trim()
    .min(1, 'Indica la stagione.')
    .max(50, 'La stagione può contenere al massimo 50 caratteri.'),
  file: z.custom<File>(
    (value) => value instanceof File && value.size > 0,
    'Seleziona il file CSV originale.',
  ),
})
