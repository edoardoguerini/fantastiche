import { cn } from '@/lib/utils/cn'

type IconName =
  | 'coins'
  | 'stopwatch'
  | 'flag'
  | 'check'
  | 'futbol'
  | 'magnifying-glass'
  | 'arrow-up'
  | 'arrow-down'
  | 'play'
  | 'pause'
  | 'sliders'
  | 'chevron-up'
  | 'chevron-down'
  | 'hand-point-up'
  | 'plus'
  | 'arrow-left'
  | 'arrow-up-right'
  | 'right-from-bracket'
  | 'trophy'

export function Icon({
  name,
  variant = 'light',
  className,
  label,
}: {
  name: IconName
  variant?: 'light' | 'regular' | 'solid'
  className?: string
  label?: string
}) {
  return (
    <i
      className={cn(
        'fa-classic',
        `fa-${variant}`,
        `fa-${name}`,
        'fa-fw',
        className,
      )}
      aria-hidden={label ? undefined : true}
      aria-label={label}
      role={label ? 'img' : undefined}
    />
  )
}
