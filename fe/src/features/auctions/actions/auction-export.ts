import { z } from 'zod'
import { api } from '@/lib/api/client'

const rosterExportSchema = z.object({
  fileName: z.string().regex(/^fantastiche-rosters-[a-zA-Z0-9-]+\.csv$/),
  csv: z.string().startsWith('$,$,$\n'),
})

export async function exportAuctionRoster(
  sessionId: string,
  signal: AbortSignal,
) {
  return rosterExportSchema.parse(
    await api.get(`/Auctions/Sessions/${sessionId}/Roster/Export`, signal),
  )
}
