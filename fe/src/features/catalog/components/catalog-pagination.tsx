import { Button } from '@/components/primitives/button'
export function CatalogPagination({
  page,
  pageSize,
  total,
  onPage,
  label,
}: {
  page: number
  pageSize: number
  total: number
  onPage: (page: number) => void
  label: string
}) {
  if (total <= pageSize && page <= 1) return null
  return (
    <nav className="catalog-pagination" aria-label={label}>
      <Button
        variant="ghost"
        disabled={page <= 1}
        onClick={() => onPage(page - 1)}
      >
        Precedente
      </Button>
      <span>
        {page} / {Math.max(1, Math.ceil(total / pageSize))}
      </span>
      <Button
        variant="ghost"
        disabled={page * pageSize >= total}
        onClick={() => onPage(page + 1)}
      >
        Successiva
      </Button>
    </nav>
  )
}
