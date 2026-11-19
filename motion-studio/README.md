# Motion Studio

Interface simples para motion control: vídeo de referência (movimento) + imagem do personagem + prompt do cenário.

## Uso

1. Inicie o ComfyUI (porta 8188).
2. `./start.sh` (ou `python3 server.py`) e abra http://127.0.0.1:8765
3. Arraste o vídeo e a imagem, escreva o cenário, clique em **Gerar vídeo**.

O resultado fica em `ComfyUI/output/motion-studio/`.

- **Ver pose do vídeo**: extrai só o esqueleto (~1 min) para você checar o movimento antes de gerar (~9 min).
  Se o esqueleto não tiver quadril e pernas, um personagem de corpo inteiro tende a se deformar.
- **Força do controle** (0,4 a 1,0; padrão 0,8): menor dá mais liberdade ao modelo e menos rigidez na pose.
  O padrão 0,8 ainda não foi comparado com 1,0 em testes.

## Como funciona

- `server.py`: serve a página e repassa `/c/*` ao ComfyUI (evita CORS). Só biblioteca padrão.
- `workflow.json`: grafo da API do ComfyUI (MiniMax H3 GGUF Q4 + pose). A página preenche os nós:
  - `140` imagem do personagem · `160` vídeo de referência · `131` prompt
  - `133` duração (s) · `150/151` largura/altura pedidas · `154` máx. de pixels por quadro
  - `129` seed · `92` saída
- Modelos exigidos: `sdpose_wholebody_fp16`, `rt_detr_v4-x-hgnet_fp16`,
  `minimax_h3_fun_controlnet_union_pruned_int8_convrot`, além do MiniMax H3 GGUF e do text encoder GGUF.

## Limites (GPU de 8 GB)

- Use 2–5 s. Acima disso, ou com qualidade Alta, pode dar OOM.
- A imagem é o primeiro quadro do vídeo (modelo imagem→vídeo), então o prompt de cenário
  influencia o vídeo, mas a imagem ancora o visual inicial.
