import { cn } from '@/lib/utils/cn'

export function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <div className={cn('brand', compact && 'brand--compact')}>
      <img
        src="/brand/fantastiche-logo.png"
        alt="Fantastiche Fantacalcio"
        width="1170"
        height="1170"
        fetchPriority="high"
      />
    </div>
  )
}
