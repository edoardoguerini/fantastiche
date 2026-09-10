# Icone

Font Awesome Pro 7.3.1 self-hosted, con la stessa organizzazione degli asset di ACKSD richiesta dall’utente. CSS originale, sette webfont referenziati e licenza sono in `src/assets/fontawesome/`; l’import globale è in `src/routes/__root.tsx`. Vite gestisce gli URL e include gli asset nella build. Nessun kit, CDN o token npm.

Usare il componente condiviso:

```tsx
import { Icon } from '@/components/common/icon'

<Icon name="plus" />
<Icon name="trophy" variant="regular" />
```

Default Classic Light, coerente con i bordi discreti della UI. La dimensione segue il testo. `IconName` contiene le icone usate: estenderlo quando serve un nuovo glifo, verificandolo nel CSS locale. Le icone accanto a un testo sono decorative; `label` serve solo per icone autonome con significato. I pulsanti composti dalla sola icona devono avere un nome accessibile sul pulsante.

Il CSS del fornitore è escluso da Prettier per conservarlo originale. Non modificare i webfont o la licenza. Il picker di icone e le famiglie aggiuntive di ACKSD non sono necessari a questo incremento.
