import { mutationOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { readLeagueLogo } from '../utils/league-logo-file'
import { leagueSchema } from '../types/leagues.types'
import {
  createLeagueSchema,
  type CreateLeagueValues,
} from '../validations/league.validations'

export const createLeagueMutationOptions = () =>
  mutationOptions({
    mutationFn: async (
      values: CreateLeagueValues & { logoFile?: File | null },
    ) => {
      const payload = createLeagueSchema.parse(values)
      const logo = values.logoFile
        ? await readLeagueLogo(values.logoFile)
        : undefined
      return leagueSchema.parse(
        await api.post('/Leagues', { ...payload, ...(logo ? { logo } : {}) }),
      )
    },
    retry: false,
  })
