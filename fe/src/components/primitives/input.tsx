import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils/cn'

export function Input({ className, ...props }: ComponentProps<'input'>) {
  return (
    <input
      data-slot="input"
      className={cn(
        'h-12 w-full rounded-lg border border-input bg-background/60 px-3.5 text-base text-foreground placeholder:text-muted-foreground/60 focus:border-ring disabled:opacity-60 aria-invalid:border-destructive',
        className,
      )}
      {...props}
    />
  )
}
