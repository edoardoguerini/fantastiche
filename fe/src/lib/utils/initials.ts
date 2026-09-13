// Iniziali per i segnaposto di loghi e avatar: due lettere al massimo.
export function initials(name: string) {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return ''
  const letters =
    words.length === 1 ? words[0]!.slice(0, 2) : words[0]![0]! + words[1]![0]!
  return letters.toUpperCase()
}
