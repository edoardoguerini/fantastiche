export const leagueLogoAccept = 'image/png,image/jpeg,image/webp'

export function leagueLogoError(file: File | null): string | null {
  if (!file) return null
  if (!leagueLogoAccept.split(',').includes(file.type))
    return 'Scegli un’immagine PNG, JPEG o WebP.'
  if (file.size === 0) return 'Il file selezionato è vuoto.'
  if (file.size > 2 * 1024 * 1024) return 'Il logo deve pesare al massimo 2 MB.'
  return null
}

export function readLeagueLogo(file: File): Promise<string> {
  const error = leagueLogoError(file)
  if (error) return Promise.reject(new Error(error))
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const data = String(reader.result)
      resolve(data.slice(data.indexOf(',') + 1))
    }
    reader.onerror = () =>
      reject(
        new Error('Impossibile leggere il logo. Seleziona di nuovo il file.'),
      )
    reader.onabort = () => reject(new Error('Lettura del logo interrotta.'))
    reader.readAsDataURL(file)
  })
}
