import { z } from 'zod'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { receiptSchema } from '../types/auction.types'

const requestId = z.string().uuid()
export const pendingCommandSchema = z.discriminatedUnion('kind', [
  z.object({
    kind: z.literal('Players'),
    body: z.object({
      requestId,
      playerId: z.string().uuid(),
      durationSeconds: z.number().int().min(5).max(30),
      increments: z.array(z.number().int().positive()).min(1).max(10),
    }),
  }),
  z.object({
    kind: z.literal('Bids'),
    body: z.object({
      requestId,
      playerAuctionId: z.string().uuid(),
      amount: z.number().int().positive(),
    }),
  }),
  z.object({
    kind: z.literal('Control'),
    body: z.object({
      requestId,
      action: z.enum(['Pause', 'Resume', 'SkipTurn', 'Reorder', 'Complete']),
      teamOrder: z.array(z.string().uuid()).nullable().optional(),
    }),
  }),
])
export type PendingCommand = z.infer<typeof pendingCommandSchema>
export type CommandKind = PendingCommand['kind']
const key = (userId: string, sessionId: string) =>
  `fantastiche:auction:${userId}:${sessionId}`
export function readPending(
  userId: string,
  sessionId: string,
): PendingCommand | null {
  try {
    const value = sessionStorage.getItem(key(userId, sessionId))
    if (!value) return null
    return pendingCommandSchema.parse(JSON.parse(value))
  } catch {
    return null
  }
}
export function storePending(
  userId: string,
  sessionId: string,
  command: PendingCommand | null,
) {
  if (command)
    sessionStorage.setItem(key(userId, sessionId), JSON.stringify(command))
  else sessionStorage.removeItem(key(userId, sessionId))
}
export async function sendAuctionCommand(
  sessionId: string,
  command: PendingCommand,
  signal?: AbortSignal,
) {
  try {
    return receiptSchema.parse(
      await api.post(
        `/Auctions/Sessions/${sessionId}/${command.kind}`,
        command.body,
        signal,
      ),
    )
  } catch (error) {
    if (error instanceof ApiError) {
      const receipt = receiptSchema.safeParse(error.data)
      if (
        receipt.success &&
        receipt.data.requestId === command.body.requestId &&
        receipt.data.sessionId === sessionId
      )
        return receipt.data
    }
    throw error
  }
}
export async function recoverAuctionCommand(
  sessionId: string,
  command: PendingCommand,
  signal?: AbortSignal,
) {
  try {
    return receiptSchema.parse(
      await api.get(
        `/Auctions/Sessions/${sessionId}/Commands/${command.body.requestId}`,
        signal,
      ),
    )
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}
