# Submarine — Regras do jogo e linguagem do domínio

Este arquivo é a fonte de verdade das regras. Se a regra mudar, muda aqui primeiro, depois no código.

## Linguagem ubíqua (use estes nomes no código e na conversa)

| Termo | Significado |
|---|---|
| **Run** (`SubmarineRun`) | Uma tentativa de uma fase. Começa ao iniciar, termina ao chegar no destino ou ficar sem oxigênio. |
| **Level / LevelRules** | Números da fase: oxigênio, velocidades, penalidades, tempos para estrelas. |
| **Heading** | Direção em graus. 0° = cima, cresce no sentido horário. |
| **Force** | Força do motor, nível discreto de 1 a `ForceLevels`. |
| **Impulse** | Um empurrão: `Heading` + `Force`. |
| **MovementModule** | Módulo de movimento: parte 1 (bússola) e parte 2 (barra de força). |
| **OxygenSupply** | Oxigênio. Cai sempre. |
| **OxygenRefillPuzzle** | Minigame de sequência de setas que restaura oxigênio. |
| **Collision / CollisionKind** | Batida: `Wall`, `Obstacle`, `Enemy`. |
| **Stars** | 1 a 3, por tempo até o destino. |
| **Sonar / Ping** | `SonarModule`: carga do ping. Cooldown entre pings. |
| **Campaign / Stage** | `Campaign`: progresso nas fases. Cada fase (stage) tem seu `LevelDefinition` e seu labirinto (prefab). |
| **Cave / CaveSettings** | Mapa irregular gerado por seed. Vira um prefab de labirinto. |
| **Exit Hint** | Ponto laranja da saída: sempre ligado, ou só por um tempo após pegar um coletável. |
| **RunContext** | A run que está valendo agora. As views leem daqui, sem referência na cena. |

## Regras

### Objetivo
Sair da origem e chegar ao destino no **menor tempo possível**. Tempo menor = mais estrelas.

### Movimento (tecla Espaço, puro skill)
1. **Parte 1 — direção:** bússola gira no sentido horário sem parar. 1º Espaço trava a direção. **Não dá para desfazer.** Opcional: `headingStepDegrees` (ex: 15) faz a bússola parar só em passos fixos. 0 = livre.
2. **Parte 2 — força:** barra horizontal vai e volta. 2º Espaço trava num dos níveis de força. Quantidade de níveis = `forceLevels`.
3. O submarino recebe um **impulso** na direção e força escolhidas e desliza com inércia (Rigidbody2D + damping).
4. Volta para a parte 1.

**Rotação:** só o módulo gira o submarino. Batida não faz ele rodar (rotação congelada). Para voltar a rotação do ambiente, ligue `Allow Environment Rotation` no `SubmarineBody`.

### Oxigênio (setas do teclado)
- Cai o tempo todo (`OxygenDrainPerSecond`).
- Ao cair até `RefillTriggerPercent` (padrão 75%), aparece uma **sequência de setas** (ex: ↑ ↓ ←).
- Cada tecla usa um espaço da sequência, certa ou errada (verde / vermelho). **Errar nunca trava** a sequência.
- Terminou a sequência: restaura `RefillRestoreAmount`, menos `RefillMistakePenalty` (padrão 25%) por erro. Mínimo 0.
- Aparece sequência nova até o oxigênio ficar cheio. Só volta a aparecer quando cair de novo até o gatilho.
- Roda **junto** com o movimento: Espaço para movimento, setas para oxigênio.

### Radar e sonar (tecla M)
O jogador **não vê o mapa**. Só vê o radar: tela redonda, submarino sempre no centro (triângulo vermelho), o mapa se move em volta. O mapa pode ser bem maior que a tela.

**Rotação do radar** (`RadarView → Orientation`):
- `HeadingUp` (padrão): o triângulo fica sempre apontando para cima. O mapa gira em volta do centro quando o submarino vira. A grade gira junto (`Rotate Grid`). `Turn Speed` suaviza a virada (0 = instantâneo).
- `NorthUp`: o mapa fica parado e o triângulo gira.
- **Bússola de movimento:** no `HeadingUp` ela mostra as direções como o radar mostra: "cima" na bússola = frente do submarino (triângulo), esquerda = esquerda na tela. Marcas e letra N giram com o mapa. No `NorthUp` ela é absoluta (cima = norte).

- **Sempre ligado:** grade, triângulo do submarino e **ponto laranja da saída**. O ponto fica na borda mostrando o lado da saída (não o caminho). Perto da saída, ele mostra o lugar real.
- **Ping (M):** copia a imagem que a câmera de sonar (sempre ligada, só vê a layer `Sonar`, sem luz 2D) faz em volta do submarino. Mostra tudo que está na layer `Sonar` num raio de `SonarViewRadius`. Uma onda abre do centro até a borda em `SonarWaveSeconds` revelando paredes, obstáculos e inimigos. A imagem fica `SonarHoldSeconds` e apaga em `SonarFadeSeconds`.
- A imagem fica **fixa no mundo**: se o submarino anda, ela desliza no radar.
- **Modos do sonar** (`LevelDefinition → Sonar Mode`):
  - `Manual` (padrão): ping com **M** e cooldown.
  - `Auto`: o sonar dá ping sozinho a cada `SonarAutoIntervalSeconds` (o primeiro já no começo). **M não faz nada.** Dificuldade mais baixa.
  - `Always`: sem ping. O radar mostra tudo ao redor o tempo todo, ao vivo (inclusive inimigos). Dificuldade mais baixa ainda.
- **Sem spam** (Manual): depois de um ping, espera `SonarCooldownSeconds` (padrão 8s). Apertar M antes disso não faz nada (rótulo pisca vermelho).
- Tudo que deve aparecer no sonar precisa estar na layer **Sonar**. A cor do sprite vira a cor do eco.
- Debug: **F2** mostra o mundo real.

### Dica da saída (ponto laranja)
Por fase (`LevelDefinition → Exit Hint Mode`):
- `Always` (padrão): o ponto da saída fica sempre no radar.
- `OnPickup`: o ponto **só aparece por `Exit Hint Seconds`** depois de pegar um **coletável** (`ExitHintPickup`) que já está no mapa desde o começo da fase. Pegar outro enquanto ainda aparece recomeça o tempo. O ponto pisca nos últimos 3s.
- Os coletáveis também aparecem no sonar (ciano). Em fase `Always` eles se removem sozinhos.

### Cavernas (mapas irregulares)
- `CaveSettings` (asset) → botão **Bake Cave Prefab** no Inspector gera um prefab de labirinto ao lado do asset: paredes irregulares (sem textura, só a forma) com colisão, `StartPoint`, saída, obstáculos, inimigos e coletáveis.
- O gerador (`CaveGenerator`, domínio) sorteia rocha, suaviza (autômato celular), mantém só a maior área aberta (tudo é alcançável), alarga passagens de 1 célula e põe início/saída nas duas pontas mais distantes (`Min Path Cells`). Mesma seed = mesma caverna.
- Dicas: `Rock Percent` maior = mais estreito e tortuoso. `Min Path Cells` maior = mais difícil.
- Gerar de novo substitui o prefab (edições à mão se perdem): duplique o prefab antes se mexeu nele.
- Ponha o prefab no campo `Maze Prefab` de uma fase do `StageCatalog`.

### Batidas
| Tipo | Efeito padrão |
|---|---|
| Parede | -10 de oxigênio de uma vez |
| Obstáculo | -5 de uma vez + queda 1.5x por 3s |
| Inimigo | -15 de uma vez + queda 2x por 5s |

- Há um **cooldown** (0.5s) entre penalidades, para encostar na parede não drenar todo frame.
- Tudo configurável por fase em `LevelDefinition` (ScriptableObject).

### Fim da fase
- **Vitória:** chegou ao destino. Estrelas por tempo (`ThreeStarSeconds`, `TwoStarSeconds`; mais lento que isso = 1 estrela).
- **Derrota:** oxigênio chegou a 0.

### Fases e seleção de fase
- `StageCatalog` (asset) lista as fases em ordem. Cada fase: nome, `LevelDefinition` (números, dificuldade) e **prefab do labirinto** (com `StartPoint`, `DestinationZone`, paredes na layer `Sonar`).
- **Desbloqueio:** fase 1 sempre aberta. Fase N só abre depois de **completar** a fase N-1 (`Campaign`).
- **Seletor** (dentro do display do radar): seta esquerda some na 1ª fase, seta direita some na última. Nome da fase no meio. Fase completa mostra **melhor tempo e estrelas**. Fase bloqueada mostra LOCKED e o botão PLAY fica desligado. Teclas: setas e Enter.
- **PLAY:** seletor some, labirinto da fase aparece, submarino vai pro `StartPoint`, a run começa e o radar + HUD aparecem.
- **R ou ESC:** volta para o seletor (ESC abandona a fase sem salvar). Completar grava melhor tempo e melhor número de estrelas (separados) em PlayerPrefs (`CampaignStore`).
- `RETURN` dispara o `UnityEvent On Return` do `StageFlow` (ligue no menu principal depois).
- Fase nova: duplique um `LevelDefinition` e um prefab de labirinto, mude, e adicione uma linha no `StageCatalog`.
- Debug: `Unlock All` no `StageFlow`.

## Camadas

```
Presentation (Unity)  →  UseCases  →  Domain (C# puro)
```

- **Domain** (`SubmarineLDJam.Domain`): regras. Sem `UnityEngine`. Testável sem abrir a cena.
- **UseCases** (`SubmarineLDJam.UseCases`): `RunService` recebe comandos e publica eventos de domínio.
- **Presentation** (`SubmarineLDJam.Presentation`): MonoBehaviours, física, input, UI. Domínio nunca conhece essa camada.

Fluxo: tecla → `RunBootstrap` → `RunService` → `SubmarineRun` → evento (`ImpulseFired`) → `SubmarineBody` aplica força no Rigidbody2D.

**Sem referências de cena:** `RunBootstrap.Begin(level)` publica o `RunService` em `RunContext`. Toda view herda `RunView`, que acha a run sozinha e chama `OnBind` / `OnUnbind` quando a run começa/acaba. Uma fase nova = um `RunService` novo = as views se religam sozinhas. `StageFlow` controla o ciclo seletor → run → seletor.

## HUD de teste

Menu **Submarine → Create Test Setup In Scene** cria a cena completa: 3 fases (Level_01..03 + Maze_01..03 prefabs + `StageCatalog`), seletor de fase, submarino e HUD já ligados. Assets que já existem são mantidos; os objetos da cena são refeitos.

- `CompassView` — bússola 360° (+ marcas dos passos). `ForceBarView` — barra com divisórias dos níveis.
- `OxygenBarView` — `Image` Filled. `RefillSequenceView` — slots com sprites de seta (só `Up Sprite` já basta, o resto gira).
- `RunStatusView` — tempo, resultado, **R** volta ao seletor (sem `StageFlow`, recarrega a cena).
- `RadarView` — radar, ping, ponto da saída, carga do sonar. `RadarSprites` gera os sprites em código (troque pelos seus nos `Image`).
- As 3 fases de teste usam cavernas (`Levels/Caves/Cave_01..03`, seeds fixas). Fase 1 com dica sempre ligada, fases 2 e 3 com coletável. Se você já tem um `StageCatalog`, ele é mantido: apague-o ou troque os prefabs nele.

## Decisões em aberto
- O motor pode ser usado de novo enquanto o submarino ainda desliza? (hoje: sim, sem espera)
- Fase tem limite de tempo que causa derrota? (hoje: não, só estrelas)
- Estrelas consideram oxigênio restante também? (hoje: só tempo)
- Labirinto vive na cena (colliders/Tilemap). Se precisar de lógica de mapa no domínio, vira entidade `Maze` depois.
