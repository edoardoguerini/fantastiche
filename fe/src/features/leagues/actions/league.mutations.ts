import { mutationOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { leagueSchema } from '../types/leagues.types'
import {
  createLeagueSchema,
  type CreateLeagueValues,
} from '../validations/league.validations'

export const createLeagueMutationOptions = () =>
  mutationOptions({
    mutationFn: async (values: CreateLeagueValues) =>
      leagueSchema.parse(
        await api.post('/Leagues', createLeagueSchema.parse(values)),
      ),
    retry: false,
  })
