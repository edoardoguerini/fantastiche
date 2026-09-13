# Icone

Font Awesome Pro 7.3.1 self-hosted, con la stessa organizzazione degli asset di ACKSD richiesta dall’utente. CSS originale, webfont referenziati e licenza sono in `src/assets/fontawesome/`; l’import globale è in `src/routes/__root.tsx`. Vite gestisce gli URL e include gli asset nella build. Nessun kit, CDN o token npm.

Usare il componente condiviso:

```tsx
import { Icon } from '@/components/common/icon'

<Icon name="plus" />
<Icon name="trophy" variant="regular" />
<Icon name="list" variant="jelly" />
```

Default Classic Light, coerente con i bordi discreti della UI. La dimensione segue il testo. `IconName` contiene le icone usate: estenderlo quando serve un nuovo glifo, verificandolo nel CSS locale. Le icone accanto a un testo sono decorative; `label` serve solo per icone autonome con significato. I pulsanti composti dalla sola icona devono avere un nome accessibile sul pulsante.

La bottombar della sala d’asta usa Jelly Regular, richiesta dall’utente: `bolt`, `list`, `shirt`, `clock` e `sliders`, tutti verificati nella famiglia. Il relativo webfont e il CSS di famiglia originali provengono dagli asset locali di ACKSD; il CSS viene importato dal componente Icon. Jelly copre un sottoinsieme del catalogo: verificare il glifo prima di usarne altri con questa variante.

Tutti i campi password usano `eye` per mostrare e `eye-slash` per nascondere il valore, in Classic Light. Il pulsante mantiene un’area di 44 × 44 px, il nome accessibile “Mostra password” / “Nascondi password” e lo stato `aria-pressed`.

Il CSS del fornitore è escluso da Prettier per conservarlo originale. Non modificare i webfont o la licenza. Il picker di icone e le altre famiglie di ACKSD non sono necessari a questo incremento.
