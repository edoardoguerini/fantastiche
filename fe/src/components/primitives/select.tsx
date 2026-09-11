import type { ComponentProps } from 'react'
import * as SelectPrimitive from '@radix-ui/react-select'
import { cn } from '@/lib/utils/cn'

// Composizione shadcn/ui su Radix Select, adattata ai token del progetto.
export const Select = SelectPrimitive.Root
export const SelectValue = SelectPrimitive.Value

function Chevron({ up = false }: { up?: boolean }) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={up ? 'm6 15 6-6 6 6' : 'm6 9 6 6 6-6'} />
    </svg>
  )
}

export function SelectTrigger({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      className={cn(
        'flex min-h-10 w-full min-w-0 items-center justify-between gap-2 rounded-lg border border-input bg-transparent px-3 py-2 text-sm text-foreground outline-none focus-visible:border-ring disabled:cursor-not-allowed disabled:opacity-50 [&>span]:truncate',
        className,
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild>
        <Chevron />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  )
}

export function SelectContent({
  className,
  children,
  position = 'popper',
  sideOffset = 5,
  ...props
}: ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        data-slot="select-content"
        position={position}
        sideOffset={sideOffset}
        className={cn(
          'relative z-[110] max-h-[min(20rem,var(--radix-select-content-available-height))] min-w-32 overflow-hidden rounded-xl border border-border bg-secondary text-foreground shadow-xl',
          className,
        )}
        {...props}
      >
        <SelectPrimitive.ScrollUpButton className="flex h-6 items-center justify-center">
          <Chevron up />
        </SelectPrimitive.ScrollUpButton>
        <SelectPrimitive.Viewport
          className={cn(
            'p-1',
            position === 'popper' &&
              'w-full min-w-[var(--radix-select-trigger-width)]',
          )}
        >
          {children}
        </SelectPrimitive.Viewport>
        <SelectPrimitive.ScrollDownButton className="flex h-6 items-center justify-center">
          <Chevron />
        </SelectPrimitive.ScrollDownButton>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  )
}

export function SelectItem({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
      className={cn(
        'relative flex min-h-10 w-full cursor-default select-none items-center rounded-lg py-2 pr-8 pl-3 text-sm outline-none focus:bg-primary/15 focus:text-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-40',
        className,
      )}
      {...props}
    >
      <span className="absolute right-2 flex size-4 items-center justify-center">
        <SelectPrimitive.ItemIndicator>
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="m20 6-11 11-5-5" />
          </svg>
        </SelectPrimitive.ItemIndicator>
      </span>
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
  )
}
