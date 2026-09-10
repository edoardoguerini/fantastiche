type LobbyQuote = {
  text: string
  author: string
  source?: string
}

// Citazioni brevi verificate; la frase di Cruyff è tradotta dall'inglese.
// Le battute originali hanno un'attribuzione distinta dai personaggi reali.
export const bombLobbyQuotes: readonly LobbyQuote[] = [
  {
    text: 'Rigore è quando arbitro fischia.',
    author: 'Vujadin Boškov',
    source:
      'https://www.gazzetta.it/Calcio/Serie-A/Sampdoria/06-08-2015/10-frasi-celebri-boskov-120847622233.shtml',
  },
  {
    text: 'Hai studiato il listone per tre settimane. Adesso deciderai di pancia.',
    author: 'Spogliatoio Fantastiche',
  },
  {
    text: 'Non dire gatto se non ce l’hai nel sacco.',
    author: 'Giovanni Trapattoni',
    source:
      'https://sport.sky.it/olimpiadi/2014/02/18/curiosita_sochi_svendsen_fotofinish_visintin_caduta_maltempo',
  },
  {
    text: '«Non lo voglio nemmeno» è spesso l’inizio dell’offerta più alta.',
    author: 'Spogliatoio Fantastiche',
  },
  {
    text: 'Sono arrivato come un re, me ne vado come una leggenda.',
    author: 'Zlatan Ibrahimović',
    source:
      'https://www.gazzetta.it/Calciomercato/13-05-2016/psg-ibrahimovic-la-mia-ultima-partita-parigi-ero-re-vado-via-leggenda-150627129827.shtml',
  },
  {
    text: 'Respira. Controlla il budget. Poi ignora entrambi i consigli.',
    author: 'Spogliatoio Fantastiche',
  },
  {
    text: 'Giocare a calcio è molto semplice, ma giocare un calcio semplice è la cosa più difficile che ci sia.',
    author: 'Johan Cruyff',
    source:
      'https://www.cruyff.com/en/accessories/others/johan-quotes/playing-football-is-very-simple-but-playing-simple/CCA221210-100_ONESIZE.html',
  },
  {
    text: 'Il vero fuoriclasse è quello che riesce a non dire quanto offrirà.',
    author: 'Spogliatoio Fantastiche',
  },
]

export function lobbyQuoteIndex(bombId: string, seconds: number): number {
  // Stessa Bomba e stesso tempo server: stessa frase anche dopo un refresh.
  const seed = Array.from(bombId).reduce(
    (value, character) => (value * 31 + character.charCodeAt(0)) >>> 0,
    0,
  )
  const elapsed = Math.max(0, Math.min(60, 60 - seconds))
  const slot = Math.min(7, Math.floor(elapsed / 8))
  return ((seed % 4) * 2 + slot) % bombLobbyQuotes.length
}
