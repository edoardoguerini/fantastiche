import { useEffect, useId, useRef, useState } from 'react'
import { Icon } from '@/components/common/icon'
import { leagueLogoAccept, leagueLogoError } from '../utils/league-logo-file'
import './league-logo-dropzone.css'

function LogoPreview({ file }: { file: File }) {
  const image = useRef<HTMLImageElement>(null)
  useEffect(() => {
    const url = URL.createObjectURL(file)
    if (image.current) image.current.src = url
    return () => URL.revokeObjectURL(url)
  }, [file])
  return <img ref={image} alt="Anteprima del logo" width="64" height="64" />
}

export function LeagueLogoDropzone({
  file,
  onChange,
  disabled,
}: {
  file: File | null
  onChange: (file: File | null) => void
  disabled: boolean
}) {
  const input = useRef<HTMLInputElement>(null)
  const id = useId()
  const [dragging, setDragging] = useState(false)
  const error = leagueLogoError(file)
  return (
    <div className="league-logo-field">
      <label htmlFor={id}>
        Logo della lega <span>(facoltativo)</span>
      </label>
      <div
        className="league-logo-dropzone"
        data-dragging={dragging && !disabled}
        data-invalid={!!error}
        data-disabled={disabled}
        onDragOver={(event) => {
          event.preventDefault()
          if (!disabled) setDragging(true)
        }}
        onDragLeave={(event) => {
          if (
            !(event.relatedTarget instanceof Node) ||
            !event.currentTarget.contains(event.relatedTarget)
          )
            setDragging(false)
        }}
        onDrop={(event) => {
          event.preventDefault()
          setDragging(false)
          if (!disabled && event.dataTransfer.files.length)
            onChange(event.dataTransfer.files.item(0))
        }}
      >
        <input
          ref={input}
          id={id}
          type="file"
          accept={leagueLogoAccept}
          aria-label="File logo della lega"
          aria-describedby={`${id}-hint${error ? ` ${id}-error` : ''}`}
          aria-invalid={!!error}
          disabled={disabled}
          hidden
          onChange={(event) => {
            const selected = event.currentTarget.files?.[0]
            if (selected) onChange(selected)
            event.currentTarget.value = ''
          }}
        />
        <button
          type="button"
          className="league-logo-select"
          disabled={disabled}
          aria-label={
            file ? 'Sostituisci logo della lega' : 'Scegli logo della lega'
          }
          aria-describedby={`${id}-hint${error ? ` ${id}-error` : ''}`}
          onClick={() => input.current?.click()}
        >
          <span className="league-logo-preview">
            {file && !error ? (
              <LogoPreview file={file} />
            ) : (
              <Icon name="cloud-arrow-up" />
            )}
          </span>
          <span className="league-logo-copy">
            <strong>{file ? file.name : 'Trascina qui il logo'}</strong>
            <span>
              {file ? 'Seleziona un’altra immagine' : 'oppure scegli un file'}
            </span>
          </span>
        </button>
        {file && (
          <button
            type="button"
            className="league-logo-remove"
            aria-label="Rimuovi logo"
            disabled={disabled}
            onClick={() => onChange(null)}
          >
            <Icon name="xmark" />
          </button>
        )}
      </div>
      <p id={`${id}-hint`} className="field-hint">
        PNG, JPEG o WebP · massimo 2 MB
      </p>
      {error && (
        <p className="field-error" role="alert" id={`${id}-error`}>
          {error}
        </p>
      )}
    </div>
  )
}
