# STOCK FLOW — Game Design Document

**Versione:** 0.2 — Aggiornamento architettura plugin e modello di business
**Data:** Aprile 2026
**Genere:** Simulatore gestionale / Puzzle di ottimizzazione
**Piattaforma:** PC (Steam)
**Engine:** Unity (URP)
**Prospettiva:** Isometrica 3D con zoom e rotazione libera

---

## 1. Concept

Stock Flow è un simulatore di magazzino automatico in cui il giocatore progetta, costruisce e ottimizza un sistema logistico composto da conveyor, trasloelevatori, scaffalature e stazioni operative. L'obiettivo è creare il magazzino più efficiente possibile per soddisfare flussi di ordini crescenti.

Il gioco si ispira alla loop di Factorio — piazza, osserva, individua il collo di bottiglia, migliora, ripeti — applicato al mondo reale della logistica di magazzino.

Stock Flow è progettato fin dall'inizio come piattaforma estensibile: i giocatori possono creare componenti custom, e nella sua evoluzione professionale può collegarsi a veri WMS (Warehouse Management System), diventando un digital twin leggero per aziende e system integrator.

---

## 2. Modello di Business — Dual Track

### 2.1 — Il modello ArmA / VBS

Stock Flow adotta il modello pionerato da Bohemia Interactive con ArmA (gioco consumer moddabile) e VBS (simulatore militare professionale costruito sullo stesso motore).

**Track Consumer — "Stock Flow" (Steam)**
Il gioco base venduto a €19.99 su Steam. Finanzia lo sviluppo del motore di simulazione, costruisce una community di giocatori e modder, e genera visibilità organica nel settore logistico. La community che ricrea magazzini reali (Amazon, IKEA, Zalando) diventa di fatto una vetrina per il prodotto professionale.

**Track Professionale — "Stock Flow Pro"**
Licenza annuale (€2.000–10.000) destinata a system integrator, consulenti logistici e aziende. Stesso core engine del gioco, con in più: collegamento WMS via API, import/export dati reali, modalità digital twin in tempo reale, reportistica avanzata, supporto tecnico dedicato.

### 2.2 — Sinergia tra i due track

La community consumer alimenta il track professionale in tre modi:

- **Validazione tecnologica:** migliaia di giocatori stressano il motore di simulazione gratuitamente, trovando bug e limiti prima dei clienti B2B.
- **Catalogo componenti:** i modder creano componenti custom (shuttle, AGV, sorter specifici) che arricchiscono anche la versione Pro.
- **Awareness:** i responsabili logistici scoprono Stock Flow Pro perché i loro figli/colleghi giocano alla versione consumer, o perché vedono riproduzioni di magazzini reali online.

### 2.3 — Pricing

| Prodotto | Prezzo | Target |
|----------|--------|--------|
| Stock Flow (Steam) | €19.99 | Gamer, appassionati logistica, studenti |
| DLC componenti avanzati | €4.99–9.99 | Gamer power-user |
| Stock Flow Pro | €2.000–5.000/anno | System integrator, consulenti |
| Stock Flow Enterprise | €5.000–10.000/anno | Aziende con integrazione WMS dedicata |
| Setup e consulenza | A progetto | Personalizzazioni per singolo cliente |

---

## 3. Pilastri di Design

### 3.1 — Costruisci
Il giocatore piazza componenti su una griglia: conveyor dritti, curve, merge, diverter, traslo, scaffali, baie di carico/scarico. Ogni pezzo ha regole di connessione e costi.

### 3.2 — Osserva
Una volta avviata la simulazione, i pacchi fluiscono nel sistema. Il giocatore osserva il comportamento emergente del layout che ha costruito.

### 3.3 — Ottimizza
Dashboard in tempo reale e heatmap evidenziano inefficienze. Il giocatore itera sul layout per migliorare throughput, ridurre tempi e eliminare colli di bottiglia.

### 3.4 — Scala
Man mano che il magazzino gestisce più SKU, volumi più alti e ordini più complessi, il giocatore deve ripensare l'architettura. Quello che funzionava con 50 ordini/ora crolla a 500.

### 3.5 — Estendi
Il giocatore (o l'azienda) può creare componenti custom, definire protocolli di comunicazione personalizzati, e collegare il simulatore a sistemi esterni.

---

## 4. Componenti Piazzabili

Ogni componente occupa celle sulla griglia e ha proprietà configurabili.

### 4.1 — Conveyor

| Tipo | Celle | Descrizione |
|------|-------|-------------|
| Dritto | 1×1 | Trasporta UdC in una direzione. Velocità configurabile. |
| Curva 90° | 1×1 | Cambia direzione del flusso. |
| Merge | 1×1 | Due ingressi, un'uscita. Logica alternata o prioritaria. |
| Diverter | 1×1 | Un ingresso, due uscite. Regole di routing configurabili. |
| Accumulo | 1×3 | Buffer con capacità N pacchi. Trattiene e rilascia su condizione. |
| Salita/Discesa | 1×2 | Cambio di livello (piano terra ↔ mezzanino). |

**Proprietà comuni:** velocità (m/s), direzione, stato (attivo/fermo/errore).

### 4.2 — Trasloelevatore (Traslo)

Unità automatica che opera all'interno di una corsia di scaffalature.

- **Struttura:** colonna verticale su binario + piattaforma + forche telescopiche
- **Movimento:** asse Z (corsia), asse Y (altezza), asse X (ingresso/uscita forche)
- **Ciclo:** riceve missione → si posiziona → preleva/deposita → torna al punto I/O
- **Configurabile:** velocità traslazione, velocità sollevamento, ciclo singolo/combinato (deposita + preleva nello stesso viaggio)
- **Vincoli:** una corsia = un traslo (espandibile in futuro con shuttle multi-livello)

### 4.3 — Scaffalatura

| Tipo | Descrizione |
|------|-------------|
| Porta-pallet singola profondità | Classica scaffalatura, accesso diretto a ogni UdC |
| Doppia profondità | Due UdC in fila, richiede forche telescopiche doppie |
| Gravitazionale (drive-in) | FIFO/LIFO, alta densità, accesso limitato |

**Proprietà:** numero livelli, altezza cella, peso max per cella, classe di stoccaggio assegnata.

### 4.4 — Stazioni

| Tipo | Funzione |
|------|----------|
| Baia di ricevimento | Punto di ingresso merci nel magazzino |
| Baia di spedizione | Punto di uscita ordini completati |
| Stazione di picking | Operatore (simulato) che prepara ordini multi-linea |
| Stazione di controllo qualità | Verifica random con tempo di processamento |
| Stazione ricarica | Per veicoli AGV (espansione futura) |

---

## 5. Sistema di Componenti Custom e Plugin

### 5.1 — Filosofia

Stock Flow è una piattaforma, non solo un gioco. I componenti built-in coprono i casi comuni, ma il mondo della logistica è pieno di macchinari specifici (shuttle, miniload, sorter a vassoi, AGV con logiche proprietarie). Invece di implementarli tutti, Stock Flow permette a giocatori e aziende di creare i propri.

### 5.2 — Livello 1: Editor Visuale (In-Game)

Accessibile a tutti i giocatori, non richiede codice.

L'editor permette di creare un componente custom come **composizione di primitive**:

- **Definizione porte I/O:** quante, posizione sulla griglia, direzione (ingresso/uscita/bidirezionale)
- **Parametri fisici:** velocità, capacità, tempi ciclo, assi di movimento
- **Logica di routing:** regole condizionali configurabili con UI a dropdown (per SKU, per classe, round-robin, carico minimo, priorità)
- **Aspetto visivo:** scelta tra mesh template (colonna, piattaforma, nastro, braccio) con colori e scala personalizzabili

**Esempi di cosa si può creare con il Livello 1:**
- Sorter a 5 uscite (1 ingresso + logica smistamento + 5 uscite)
- Miniload con parametri diversi dal traslo standard
- Buffer intelligente con logica di rilascio custom
- Conveyor a velocità variabile con zone di accelerazione/decelerazione

I componenti creati possono essere salvati, condivisi tramite Steam Workshop, e usati in qualsiasi scenario.

### 5.3 — Livello 2: Plugin C# (Power User / B2B)

Per utenti avanzati e integratori. Richiede programmazione.

Ogni plugin implementa l'interfaccia `IStockFlowComponent`:

```csharp
public interface IStockFlowComponent
{
    // Identità
    string ComponentId { get; }
    string DisplayName { get; }
    
    // Porte I/O
    IReadOnlyList<PortDefinition> Ports { get; }
    
    // Ciclo di simulazione
    void OnTick(SimulationContext context, float deltaTime);
    void OnEntityArrived(PortId port, Entity entity);
    void OnMissionReceived(Mission mission);
    
    // Comunicazione esterna (opzionale)
    IExternalAdapter ExternalAdapter { get; }
    
    // Modello 3D (opzionale, altrimenti usa mesh di default)
    string CustomMeshPath { get; }
}
```

Il plugin viene compilato come DLL e caricato dal plugin loader all'avvio.

### 5.4 — Adapter Pattern per Comunicazione Esterna

Il Simulation Engine parla **esclusivamente** il suo protocollo interno — messaggi tipizzati come `EntityArrived`, `MissionComplete`, `RequestNextMission`. Non sa nulla di REST, WebSocket o MQTT.

La traduzione da/verso il mondo esterno avviene tramite **adapter**:

```
[Sistema esterno (WMS, PLC, ERP...)]
        │
        │  REST / WebSocket / MQTT / OPCUA / CSV / ...
        ▼
[External Adapter]
        │  Traduce protocollo esterno ↔ messaggi interni
        ▼
[Simulation Engine - protocollo interno]
        │
        ▼
[Componente simulato]
```

**Adapter built-in (forniti con Stock Flow Pro):**
- REST JSON adapter
- WebSocket adapter
- CSV/XML file import adapter (per replay dati storici)

**Adapter custom (creati dall'utente):**
Implementano l'interfaccia `IExternalAdapter`:

```csharp
public interface IExternalAdapter
{
    // Connessione
    Task ConnectAsync(AdapterConfig config);
    Task DisconnectAsync();
    bool IsConnected { get; }
    
    // Traduzione IN: mondo esterno → messaggi interni
    IAsyncEnumerable<InternalMessage> ReceiveAsync();
    
    // Traduzione OUT: messaggi interni → mondo esterno
    Task SendAsync(InternalMessage message);
}
```

Questo design isola completamente la complessità dei protocolli. Supportare un nuovo WMS significa scrivere un solo adapter, senza toccare né il simulation engine né i componenti.

### 5.5 — Gestione del Timing con Connessioni Esterne

Un WMS reale opera in tempo reale, la simulazione supporta velocità variabili (1x–10x). Questo crea un problema di sincronizzazione.

**Regola semplice per Fase 2:** quando una connessione esterna è attiva, la simulazione è bloccata a 1x (tempo reale). L'utente vede un indicatore "LIVE MODE" nell'UI e i controlli di velocità sono disabilitati.

**Evoluzione futura (Fase 3):** modalità replay in cui i messaggi WMS registrati durante una sessione live vengono riprodotti a velocità variabile per analisi post-hoc. I messaggi sono bufferizzati con timestamp e riprodotti rispettando i delta temporali relativi, scalati per la velocità di simulazione.

### 5.6 — Workshop e Community

- **Steam Workshop:** condivisione componenti custom, layout completi, scenari
- **Portale web (post-lancio):** catalogo componenti con rating, documentazione API, forum
- **Tag system:** ogni componente è taggato (conveyor, storage, sorting, transport, station) per ricerca e filtraggio

---

## 6. Simulazione

### 6.1 — Architettura

La logica di simulazione è separata dal rendering Unity.

```
[Simulation Engine (C# puro)]
    ├── Grid Manager — gestione griglia e componenti
    ├── Entity Manager — pacchi, UdC, ordini
    ├── Routing Engine — grafo + regole di instradamento
    ├── Mission Controller — assegnazione missioni ai traslo
    ├── Plugin Loader — caricamento componenti custom e adapter
    ├── Clock — tempo simulato con velocità variabile
    └── Metrics Collector — raccolta dati in tempo reale

[Unity Renderer]
    ├── Legge lo stato dal Simulation Engine ogni frame
    ├── Interpola posizioni per movimento fluido
    ├── Gestisce camera, UI, input, effetti visivi
    └── NON contiene logica di business
```

Questo approccio permette:
- Fast-forward fino a 10x senza problemi
- Test automatici della simulazione senza Unity
- Caricamento plugin e adapter a runtime
- Riuso del motore per il collegamento WMS
- Replay e analisi post-sessione

### 6.2 — Entità simulate

**UdC (Unità di Carico):** l'oggetto fisico che si muove nel magazzino. Ha proprietà: ID, SKU, peso, dimensione, destinazione, timestamp ingresso.

**Ordine:** richiesta di prelievo composta da 1-N linee d'ordine, ciascuna con SKU e quantità. Ha priorità e deadline.

**Missione:** istruzione atomica per un traslo o operatore. Tipo: stoccaggio, prelievo, rilocazione.

### 6.3 — Material Flow Control (MFC)

Il cuore della simulazione è il grafo di routing:

- Ogni componente piazzato è un nodo
- Le connessioni tra componenti sono archi
- Ogni arco ha: capacità, tempo di attraversamento, regola di priorità
- I diverter hanno regole configurabili dal giocatore:
  - Per SKU / classe merce
  - Per destinazione
  - Round-robin
  - Carico minimo (manda al ramo meno saturo)
- I componenti custom definiscono le proprie regole tramite `OnEntityArrived` e `OnTick`

### 6.4 — Velocità simulazione

| Velocità | Utilizzo | Disponibilità |
|----------|----------|---------------|
| Pausa | Modifica layout, analisi | Sempre |
| 1x | Osservazione normale | Sempre |
| 2x | Monitoraggio rapido | Sempre |
| 5x | Test di carico | Solo offline |
| 10x | Simulazione notturna / stress test | Solo offline |
| LIVE | Tempo reale sincronizzato con WMS | Solo con connessione esterna |

---

## 7. Metriche e Dashboard

### 7.1 — KPI principali (sempre visibili)

- **Throughput** — UdC/ora in ingresso e in uscita
- **Tempo medio evasione ordine** — dal ricevimento alla spedizione
- **Saturazione magazzino** — % celle occupate
- **Utilizzo traslo** — % tempo attivo vs idle vs in attesa
- **Utilizzo conveyor** — % capacità usata per segmento
- **Ordini in ritardo** — numero e % sul totale

### 7.2 — Visualizzazioni overlay

Attivabili dal giocatore sopra la vista 3D:

- **Heatmap flusso** — colore dal verde (fluido) al rosso (congestionato) su ogni segmento conveyor
- **Heatmap accesso celle** — celle molto accedute in rosso, celle "morte" in blu
- **Percorsi traslo** — traccia visiva dei movimenti recenti
- **Coda ordini** — timeline visiva degli ordini in attesa, in lavorazione, completati
- **Bottleneck highlight** — il sistema evidenzia automaticamente il componente più limitante con un contorno pulsante

### 7.3 — Grafici storici

- Throughput nel tempo (linea)
- Distribuzione tempi di evasione (istogramma)
- Utilizzo per componente (barre comparative)
- Saturazione nel tempo (area)

Esportabili in CSV per analisi esterna.

---

## 8. Modalità di Gioco

### 8.1 — Campagna (Scenario)

Serie di livelli con vincoli e obiettivi crescenti.

**Esempio di progressione:**

| Livello | Nome | Sfida |
|---------|------|-------|
| 1 | Primo giorno | 1 baia, 1 conveyor dritto, 1 traslo, 20 ordini. Tutorial. |
| 2 | Raddoppio | Volumi x2, budget per un secondo traslo. Introduce merge. |
| 3 | Multi-SKU | 5 classi merceologiche, logica ABC di stoccaggio. |
| 4 | Black Friday | Picco di ordini 10x, devi preparare il magazzino in anticipo. |
| 5 | Surgelati | Zona a temperatura controllata con vincoli di adiacenza. |
| 6 | E-commerce | Ordini mono-linea ad alta frequenza, ottimizza per velocità. |
| 7 | Full Pallet | GDO, pochi SKU ma volumi enormi, ottimizza per throughput massivo. |
| 8 | Omnichannel | Mix e-commerce + GDO, due flussi diversi nello stesso magazzino. |
| ... | ... | Complessità crescente fino a magazzini multi-piano con shuttle. |

Ogni livello ha obiettivi a stelle:
- ★ Completa tutti gli ordini
- ★★ Entro il tempo limite
- ★★★ Con throughput sopra soglia

### 8.2 — Sandbox

Terreno libero, budget infinito, generatore di ordini configurabile. Il giocatore costruisce il magazzino dei sogni e lo stressa a piacere.

Parametri configurabili:
- Dimensione area (S / M / L / XL)
- Profilo ordini (frequenza, linee per ordine, distribuzione SKU)
- Catalogo SKU (numero, classi, rotazione ABC)
- Budget (infinito o limitato)

### 8.3 — Challenge settimanali (post-lancio)

Uno scenario con vincoli specifici, classifica globale basata sulle metriche. Incentiva la community a trovare soluzioni creative.

### 8.4 — Modalità Digital Twin (Stock Flow Pro)

Connessione live a un WMS reale. Il magazzino simulato riceve ordini reali ed esegue missioni in sincrono. La simulazione diventa uno specchio del magazzino fisico, utile per monitoraggio, training e what-if analysis.

---

## 9. Progressione e Unlock

### 9.1 — Albero tecnologico

Il giocatore sblocca componenti avanzati completando livelli:

```
Conveyor base → Conveyor alta velocità → Sorter automatico
Traslo standard → Traslo dual-fork → Shuttle multi-livello
Scaffale singola prof. → Doppia profondità → Gravitazionale
Picking manuale → Pick-to-light → Goods-to-person
```

### 9.2 — Economia

Ogni livello ha un budget iniziale e costi per componente. Completare obiettivi bonus sblocca budget extra per i livelli successivi.

| Componente | Costo base |
|------------|-----------|
| Conveyor dritto | 100 |
| Curva | 120 |
| Merge | 300 |
| Diverter | 400 |
| Traslo standard | 5.000 |
| Campata scaffale (5 livelli) | 800 |
| Baia I/O | 1.500 |
| Stazione picking | 2.000 |

I costi sono bilanciati per creare trade-off: un secondo traslo costa quanto 50 metri di conveyor. Cosa conviene di più?

---

## 10. Interfaccia Utente

### 10.1 — Layout schermo

```
┌─────────────────────────────────────────────┐
│  [Velocità] [Pausa/Play]    KPI bar    [€]  │  ← Top bar
├───────┬─────────────────────────────┬───────┤
│       │                             │       │
│  C    │                             │  I    │
│  o    │      Vista 3D isometrica    │  n    │
│  m    │         del magazzino       │  f    │
│  p    │                             │  o    │
│  o    │                             │       │
│  n    │                             │  P    │
│  e    │                             │  a    │
│  n    │                             │  n    │
│  t    │                             │  e    │
│  i    │                             │  l    │
│       │                             │       │
├───────┴─────────────────────────────┴───────┤
│           Barra ordini / notifiche           │  ← Bottom bar
└─────────────────────────────────────────────┘
```

- **Pannello sinistro:** palette componenti piazzabili, raggruppati per categoria
- **Pannello destro:** dettagli del componente selezionato, configurazione proprietà, mini-metriche
- **Top bar:** controlli simulazione, KPI principali, budget
- **Bottom bar:** coda ordini, notifiche eventi, alert
- **Indicatore LIVE MODE:** visibile quando una connessione esterna è attiva

### 10.2 — Interazione costruzione

- **Click sinistro** — Piazza componente / Seleziona componente esistente
- **Click destro** — Ruota componente
- **Drag** — Piazza conveyor in serie (tipo disegno linea)
- **Canc** — Elimina componente selezionato
- **Ctrl+Z / Ctrl+Y** — Undo / Redo
- **Rotellina** — Zoom
- **Click centrale + drag** — Pan camera
- **Q / E** — Ruota camera di 90°

---

## 11. Audio e Visual Identity

### 11.1 — Stile grafico

Estetica industriale pulita, low-poly stilizzato. Palette colori:

- Pavimento: grigio chiaro con griglia sottile
- Scaffalature: grigio scuro / antracite
- Conveyor: giallo industriale con rulli visibili
- Pacchi: cartone marrone con etichette colorate per classe SKU
- Traslo: arancione/blu meccanico
- Componenti custom: bordo ciano per distinguerli dai built-in
- UI: sfondo scuro, accenti verde/arancione/rosso per stati

Riferimenti visivi: Mini Motorways, Shapez, Factorio (per la leggibilità).

### 11.2 — Audio

- Ronzio costante e soddisfacente dei conveyor in movimento
- Clank metallico delle forche del traslo
- Beep di conferma quando un pacco raggiunge destinazione
- Suono di "livello su" quando si raggiunge un obiettivo
- Alert sonoro per colli di bottiglia o ordini in ritardo
- Musica ambient industriale, rilassante ma con ritmo

### 11.3 — Juice e feedback

- Pacchi che oscillano leggermente sul conveyor (shader, non fisica)
- Particelle verdi al completamento ordine
- Screen shake leggero al raggiungimento di throughput record
- Numeri che fluttuano brevemente ("+1 ordine completato") sopra le baie
- Effetto "onda" sui conveyor quando si avvia la simulazione dopo una pausa

---

## 12. Collegamento WMS (Fase 2 — Stock Flow Pro)

### 12.1 — Architettura di integrazione

```
[WMS esterno]
    │
    │  Protocollo nativo (REST / WS / MQTT / ...)
    ▼
[External Adapter (intercambiabile)]
    │
    │  Protocollo interno Stock Flow
    ▼
[Simulation Engine]
    ├── Traduttore missioni
    ├── Gestione anagrafica articoli (sync)
    └── Log completo per audit
```

### 12.2 — Adapter built-in

| Adapter | Protocollo | Caso d'uso |
|---------|-----------|------------|
| REST JSON | HTTP REST | WMS moderni (Manhattan, Körber, Odoo) |
| WebSocket | WS/WSS | Comunicazione bidirezionale real-time |
| File Import | CSV/XML | Replay dati storici, test offline |

### 12.3 — Adapter custom

Creati dall'utente implementando `IExternalAdapter`. Distribuiti come DLL nel plugin system. Casi d'uso: protocolli proprietari, OPCUA per PLC, connettori ERP (SAP), message broker (RabbitMQ, Kafka).

### 12.4 — Casi d'uso B2B

- **Pre-vendita impianti:** un system integrator mostra al cliente come funzionerà il magazzino prima di costruirlo
- **Ottimizzazione layout:** un'azienda testa modifiche al layout esistente senza fermare le operazioni
- **Training operatori:** simulazione realistica per formare nuovi addetti al WMS
- **Test di carico:** simulare il Black Friday con dati di ordini reali dello scorso anno
- **What-if analysis:** "cosa succede se aggiungo un traslo nella corsia 7?"

---

## 13. Roadmap di Sviluppo

### Fase 0 — Fondamenta (Mese 1–2)
- Apprendimento Unity, ProBuilder, C# gameplay
- Prototipo griglia con piazzamento conveyor base
- Movimento pacco su conveyor dritto
- Setup architettura Simulation Engine separato da Unity

### Fase 1A — Core gameplay (Mese 3–4)
- Tutti i tipi di conveyor (curve, merge, diverter)
- Traslo con ciclo completo (prelievo/deposito)
- Scaffalature con celle indirizzabili
- Sistema ordini base e generatore

### Fase 1B — Metriche e polish (Mese 5–6)
- Dashboard KPI in tempo reale
- Heatmap overlay
- Fast-forward
- Undo/redo piazzamento
- Audio base

### Fase 1C — Contenuto e game loop (Mese 7–8)
- 10–15 livelli campagna con progressione
- Modalità sandbox
- Albero tecnologico e economia
- Sistema a stelle

### Fase 1D — Release prep (Mese 9–10)
- Polish grafico e UI
- Tutorial e onboarding
- Salvataggio/caricamento
- Editor componenti visuale (Livello 1)
- Steam Workshop integration base
- Steam page e marketing
- Beta testing

### Fase 2 — WMS Integration (Mese 11–14)
- Plugin system con IStockFlowComponent e loader DLL
- Adapter pattern con IExternalAdapter
- Adapter built-in REST, WebSocket, CSV
- Modalità LIVE (1x forzato con connessione esterna)
- Documentazione API per sviluppatori e system integrator
- Beta con 2–3 aziende partner

### Fase 3 — Piattaforma (Post-lancio, ongoing)
- Portale community per componenti e adapter
- Visual scripting per logiche custom (alternativa a C#)
- Replay e analisi post-sessione con velocità variabile
- Modalità multiplayer cooperativa (due giocatori sullo stesso magazzino)
- OPCUA adapter per digital twin con PLC reali
- Marketplace componenti premium

---

## 14. Rischi e Mitigazioni

| Rischio | Impatto | Mitigazione |
|---------|---------|-------------|
| Scope creep sui componenti | Alto | MVP con 6 componenti base, editor custom per il resto |
| Performance con molti pacchi | Medio | Architettura ECS-like, object pooling, LOD |
| Simulazione non realistica | Medio | Consultare specifiche reali di traslo e conveyor |
| Complessità UI per nuovi giocatori | Alto | Tutorial graduale stile campagna, tooltip ovunque |
| Plugin system troppo complesso per i modder | Medio | Livello 1 visuale copre 80% dei casi, Livello 2 solo per power user |
| Collegamento WMS troppo complesso | Alto | Fase 2 separata, adapter pattern isola la complessità |
| Sicurezza plugin di terze parti | Medio | Sandbox per plugin, review per lo store, firma digitale per Pro |
| Timing simulazione vs tempo reale | Medio | Fase 2: solo 1x live. Fase 3: replay bufferizzato |

---

## 15. Riferimenti e Ispirazioni

**Giochi:**
- **Factorio** — Loop costruzione/ottimizzazione, moddabilità, soddisfazione del flusso
- **Shapez** — Semplicità visiva, puzzle di routing
- **Mini Motorways** — Estetica pulita, tensione crescente
- **Satisfactory** — Costruzione 3D, conveyor belt aesthetic

**Modello di business:**
- **ArmA / VBS (Bohemia Interactive)** — Gioco consumer moddabile + versione professionale per eserciti. Stesso motore, due mercati, la community consumer alimenta il prodotto B2B.
- **Kerbal Space Program / kOS** — Gioco con scripting integrato che attrae sia gamer che ingegneri.

**Software industriale (competitor B2B):**
- **Emulate3D (Rockwell)** — Digital twin per automazione magazzino
- **Demo3D / FlexSim** — Simulazione logistica professionale
- **Visual Components** — Simulazione 3D per robotica e produzione

---

*Questo documento è un draft vivente. Ogni sezione verrà espansa e dettagliata durante lo sviluppo.*
