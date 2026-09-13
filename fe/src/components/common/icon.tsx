import { cn } from '@/lib/utils/cn'
import '@/assets/fontawesome/css/jelly.css'

type IconName =
  | 'ellipsis-vertical'
  | 'cloud-arrow-up'
  | 'eye'
  | 'eye-slash'
  | 'download'
  | 'bomb'
  | 'volume-low'
  | 'volume-xmark'
  | 'xmark'
  | 'coins'
  | 'money-bill'
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
  | 'chevron-left'
  | 'chevron-right'
  | 'hand-point-up'
  | 'plus'
  | 'arrow-left'
  | 'arrow-up-right'
  | 'right-from-bracket'
  | 'trophy'
  | 'list'
  | 'shirt'
  | 'clock-rotate-left'
  | 'clock'
  | 'bolt'
  | 'users'
  | 'user-group'

export function Icon({
  name,
  variant = 'light',
  className,
  label,
}: {
  name: IconName
  variant?: 'light' | 'regular' | 'solid' | 'jelly'
  className?: string
  label?: string
}) {
  return (
    <i
      className={cn(
        variant === 'jelly' ? 'fa-jelly' : 'fa-classic',
        `fa-${variant === 'jelly' ? 'regular' : variant}`,
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
