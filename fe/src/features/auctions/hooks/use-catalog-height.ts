import { useLayoutEffect, useRef } from 'react'

/** Mantiene il listone laterale nello spazio tra intestazione e barra fissa. */
export function useCatalogHeight(active: boolean) {
  const ref = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    const surface = ref.current
    const session = surface?.closest('.auction-session')
    const footer = session?.querySelector('.auction-bottom-bar')
    if (!active || !surface || !session || !footer) return

    let frame = 0
    function measure() {
      if (!surface || !footer || !surface.getClientRects().length) return
      const bounds = surface.getBoundingClientRect()
      const top = Math.max(16, bounds.top)
      const height = Math.max(0, footer.getBoundingClientRect().top - top - 16)
      surface.style.setProperty('--catalog-viewport-top', `${top}px`)
      surface.style.setProperty('--catalog-viewport-left', `${bounds.left}px`)
      surface.style.setProperty('--catalog-viewport-width', `${bounds.width}px`)
      surface.style.setProperty('--catalog-viewport-height', `${height}px`)
    }
    function schedule() {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(measure)
    }
    const observer = new ResizeObserver(schedule)
    observer.observe(surface)
    observer.observe(session)
    observer.observe(footer)
    window.addEventListener('resize', schedule)
    window.addEventListener('scroll', schedule, { passive: true })
    measure()
    return () => {
      cancelAnimationFrame(frame)
      observer.disconnect()
      window.removeEventListener('resize', schedule)
      window.removeEventListener('scroll', schedule)
      surface.style.removeProperty('--catalog-viewport-height')
      surface.style.removeProperty('--catalog-viewport-top')
      surface.style.removeProperty('--catalog-viewport-left')
      surface.style.removeProperty('--catalog-viewport-width')
    }
  }, [active])

  return ref
}
