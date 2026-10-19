# Changelog

Novidades de cada versão do Glide para Windows. O Glide mostra as novas entradas uma vez após se atualizar, e as notas de cada versão no GitHub vêm daqui, então escreva para quem usa o Glide. Adicione uma entrada para cada mudança que vale a pena conhecer na próxima versão antes de criar a tag. Esta é uma tradução de CHANGELOG.md, que é o original.

## 0.9.3 — 2026-10-19

### Novidades

- **Layout em mosaico.** Win+Ctrl+T alterna o espaço de trabalho atual entre a faixa com rolagem e um layout em mosaico, em que todas as colunas dividem a tela e cabem juntas, sem rolagem. Cada espaço de trabalho mantém o próprio layout, mesmo depois de reiniciar o Glide. Percorrer as larguras de uma coluna (Win+Ctrl+R) dá a ela uma parte maior ou menor, arrastar a borda a redimensiona, e Win+Ctrl+F continua deixando a coluna com largura total até você pressionar de novo.
- **Escolha o layout com que os novos espaços de trabalho começam** em Configurações › Geral › Layout: Rolagem, como antes, ou Mosaico. Os espaços de trabalho que você alternou com Win+Ctrl+T mantêm o seu.

## 0.9.2 — 2026-10-16

### Novidades

- **Novidades também fala o seu idioma.** As notas que o Glide mostra após uma atualização, e em Novidades, agora estão no mesmo idioma do resto do Glide.

## 0.9.1 — 2026-10-16

### Novidades

- **O Glide fala o seu idioma.** O Glide agora está disponível em francês, alemão, japonês, chinês simplificado, coreano e português do Brasil, além de inglês e espanhol, inclusive o instalador. Ele segue o idioma de exibição do Windows, ou você escolhe um em Configurações › Geral › Idioma.

## 0.9.0 — 2026-10-15

### Novidades

- **Veja onde a janela arrastada vai ficar.** Enquanto você arrasta uma janela, um contorno na cor do contorno de foco mostra a coluna em que ela entraria, incluindo a barra de abas. Sobre outro monitor, uma barra mostra onde ela ficaria entre as colunas desse monitor. Sem destaque, ela volta para o lugar de origem.
- **Um menu da bandeja mais completo.** Ele lista os espaços de trabalho em uso com o número de janelas de cada um; escolha um para ir até lá. Em **Atalhos** está cada ação com o seu atalho, para executá-la pelo menu. Quando “Assumir os atalhos do Windows” está desativado, o menu também lista os atalhos que o Windows mantém.
- **Todos os atalhos em uma página.** Win+Ctrl+/ (a tecla com ? em um teclado americano) mostra todos; pressione de novo ou Esc para fechar. Ele também fica no menu da bandeja, em Atalhos.
- **Relatar um problema**, no menu da bandeja e em Configurações › Geral. O Glide reúne o registro e as configurações em um zip, sem os títulos das janelas, e abre um novo issue no GitHub para você descrever o que aconteceu e anexar o zip. Nada é enviado automaticamente.
- **O Glide fala espanhol.** Quando o Windows está em espanhol, o Glide também está, inclusive o instalador. Novidades continua em inglês.

### Correções

- **Tela cheia continua em tela cheia.** Um vídeo, um jogo ou F11 em um navegador não é mais puxado de volta para a sua coluna. O Glide o deixa em paz, oculta o contorno de foco, o escurecimento e a barra de abas sobre ele, e deixa a roda horizontal e os gestos do touchpad chegarem até ele.
- Alternar para outra área de trabalho virtual do Windows e voltar mantém as suas colunas na mesma ordem.
- Fechar uma janela não tira mais o foco das janelas do próprio Glide, como Novidades ou Configurações.

## 0.8.1 — 2026-10-14

### Correções

- Clicar em **Continuar** na janela Novidades fechava o Glide. Agora, fechar uma janela mantém o Glide em execução.
- O ícone da bandeja mostra todos os espaços de trabalho em uso: uma grade 3×3 com os nove espaços de trabalho, os em uso acesos e o ativo mais brilhante. Com apenas um em uso, ele mostra o número como antes.

## 0.8.0 — 2026-10-14

### Novidades

- **Escurecer as janelas sem foco.** As janelas atrás da que está em foco no mesmo monitor ficam mais escuras, para que a janela em foco se destaque. Desativado por padrão; ative e defina o quanto escurecer em Configurações › Aparência.
- **Todos os espaços de trabalho em uso são listados.** A dica do ícone da bandeja e o menu mostram todos os espaços de trabalho que têm janelas. O ícone continua mostrando o ativo.
- **Os números dos espaços de trabalho na tela ao alternar** podem ser desativados em Configurações › Aparência.
- **Instalar atualizações automaticamente.** Desativado por padrão. Ative em Configurações › Atualizações, e o Glide instala uma nova versão assim que a encontra e depois reinicia.

### Mudanças

- As versões ainda não têm assinatura de código, então o Windows SmartScreen pode avisar na primeira vez que você executar o Glide: escolha **Mais informações › Executar assim mesmo**.
- A política de privacidade do instalador não tem mais um link para uma política de assinatura de código.

## 0.7.0 — 2026-10-14

### Novidades

- **O Glide se mantém atualizado.** Duas vezes por dia ele busca uma nova versão e a oferece pelo ícone da bandeja. Todo download é verificado antes de ser instalado, e o Glide devolve as suas janelas ao lugar, atualiza e reinicia. Desative em Configurações › Atualizações, ou durante a instalação.
- **Novidades.** Após uma atualização, o Glide mostra o que mudou, como aqui. Encontre-o de novo no menu da bandeja.
- **Os apps voltam para onde você os deixa.** O Glide lembra o monitor, o espaço de trabalho, a largura da coluna e a posição de cada app na faixa. A janela de um app reaberto abre ali, e após uma reinicialização as suas janelas abertas são organizadas do mesmo jeito. Desative ou esqueça tudo em Configurações › Geral.
- **O Glide é de código aberto**, sob a licença MIT: [github.com/QuackByte/glide-windows](https://github.com/QuackByte/glide-windows).

### Mudanças

- O instalador mostra a política de privacidade do Glide e deixa você escolher se o Glide busca atualizações.

## 0.6.0 — 2026-10-14

### Novidades

- **Ícones de apps** ao lado dos títulos das janelas na visão geral e nas barras de abas.
- **A visão geral rola** quando uma faixa é longa demais para a tela, em vez de encolher os blocos até ficarem ilegíveis. A roda rola a linha sob o ponteiro.

### Correções

- As barras de abas deslizam com a sua coluna em vez de saltar à frente dela.
- Depois de empilhar ou desempilhar, o contorno de foco podia aparecer onde a janela estava antes. Agora ele acompanha a janela.

## 0.5.0 — 2026-10-14

### Novidades

- **Vários monitores**: cada monitor tem a sua própria faixa e nove espaços de trabalho. Win+Alt+←/→ movem o foco entre monitores; Win+Alt+Shift+←/→ enviam a coluna em foco para o outro.
- **Arrastar e soltar**: solte uma janela em uma barra de abas para adicioná-la às abas, sobre outra janela para empilhá-la ali, ou em outro monitor para movê-la.
- **Um contorno de foco para todos os apps**, inclusive os apps Electron, com uma espessura que você pode definir.
- **Gestos de três dedos no touchpad** deslizam pela faixa.
- “Abrir o Glide ao entrar” no menu da bandeja.

## 0.4.1 — 2026-10-14

A primeira versão do Glide para Windows.
