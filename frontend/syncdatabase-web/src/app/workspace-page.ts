import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
// import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';

import {
  CampoDivergenteRegistro,
  RegistroDivergenteDados,
  RegistroHistorico,
  ResultadoComparacaoDados,
  ResultadoComparacaoEstrutura,
  ResultadoPreviaAtualizacaoRegistro,
  StatusConexao,
  TarefaApi,
  WorkspaceState
} from './workspace-state';

type WorkspacePageName = 'overview' | 'compare' | 'tables' | 'history' | 'settings';

@Component({
  selector: 'app-workspace-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './workspace-page.html'
})
export class WorkspacePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly state = inject(WorkspaceState);
  private readonly cdr = inject(ChangeDetectorRef);

  pagina: WorkspacePageName = 'overview';
  carregando = false;
  erro: string | null = null;

  schemaDados = 'public';
  tabelaDados = '';
  carregandoDados = false;
  erroDados: string | null = null;
  chavePrimariaJson = '';
  previaAtualizacao: ResultadoPreviaAtualizacaoRegistro | null = null;
  carregandoPrevia = false;
  aplicandoAtualizacao = false;
  mensagemAtualizacao: string | null = null;
  tarefas: TarefaApi[] = [];
  carregandoHistorico = false;
  erroHistorico: string | null = null;
  private comparacaoDadosSubscription?: Subscription;

  get resultado(): ResultadoComparacaoEstrutura | null { return this.state.resultado; }
  set resultado(valor: ResultadoComparacaoEstrutura | null) { this.state.resultado = valor; }
  get origem(): StatusConexao { return this.state.origem; }
  set origem(valor: StatusConexao) { this.state.origem = valor; }
  get destino(): StatusConexao { return this.state.destino; }
  set destino(valor: StatusConexao) { this.state.destino = valor; }
  get resultadoDados(): ResultadoComparacaoDados | null { return this.state.resultadoDados; }
  set resultadoDados(valor: ResultadoComparacaoDados | null) { this.state.resultadoDados = valor; }
  get historico(): RegistroHistorico[] { return this.state.historico; }
  set historico(valor: RegistroHistorico[]) { this.state.historico = valor; }

  ngOnInit(): void {
  const pagina = this.route.snapshot.data['page'] as WorkspacePageName | undefined;

  if (!pagina) {
    void this.router.navigateByUrl('/overview');
    return;
  }

  this.pagina = pagina;

  if (pagina === 'compare') {
    void this.testarConexoes();
  }

  if (pagina === 'history') {
    void this.carregarHistorico();
  }
}

  get tituloPagina(): string {
    const titulos: Record<WorkspacePageName, string> = {
      overview: 'Visão geral',
      compare: 'Comparar bancos',
      tables: 'Explorador de tabelas',
      history: 'Histórico de execuções',
      settings: 'Configurações'
    };
    return titulos[this.pagina];
  }

  async testarConexoes(): Promise<void> {
    const [producao, homologacao] = await Promise.allSettled([
      firstValueFrom(this.http.get<StatusConexao>('/api/conexoes/testar-producao')),
      firstValueFrom(this.http.get<StatusConexao>('/api/conexoes/testar-homologacao'))
    ]);
    this.origem = this.statusDaResposta(producao, 'Produção');
    this.destino = this.statusDaResposta(homologacao, 'Homologação');
  }

  comparar(): void {
    if (this.carregando) return;
    this.carregando = true;
    this.erro = null;
    this.http.get<ResultadoComparacaoEstrutura>('/api/comparacao/estrutura').subscribe({
      next: resultado => {
        this.resultado = resultado;
        this.carregando = false;
        this.registrarHistorico('Comparação estrutural', `${resultado.tabelasEmAmbosAmbientes.length} tabelas compartilhadas`);
      },
      error: () => {
        this.erro = 'Não foi possível comparar os bancos. Verifique as conexões e tente novamente.';
        this.carregando = false;
      }
    });
  }

 compararDados(): void {
  if (this.carregandoDados || !this.schemaDados.trim() || !this.tabelaDados.trim()) return;

  this.carregandoDados = true;
  this.erroDados = null;
  this.resultadoDados = null;

  console.log('[Comparação] Iniciando:', this.schemaDados, this.tabelaDados);

  this.comparacaoDadosSubscription = this.http.get<ResultadoComparacaoDados>(
    '/api/comparacao/dados',
    {
      params: {
        schema: this.schemaDados.trim(),
        tabela: this.tabelaDados.trim(),
        tamanhoLote: 200
      }
    }
  ).subscribe({
    next: resultado => {
      console.log('[Comparação] Resposta recebida:', resultado);

      this.resultadoDados = resultado;
      this.carregandoDados = false;
      this.cdr.detectChanges();
      this.comparacaoDadosSubscription = undefined;

      console.log('[Comparação] Carregamento finalizado:', this.carregandoDados);
    },
    error: erro => {
      console.error('[Comparação] Erro:', erro);

      this.erroDados = 'Não foi possível comparar os dados desta tabela. Verifique os nomes e as conexões.';
      this.carregandoDados = false;
      this.cdr.detectChanges();
      this.comparacaoDadosSubscription = undefined;
    },
    complete: () => {
      console.log('[Comparação] Observable concluído.');
    }
  });
}

consultarRegistroDivergente(registro: RegistroDivergenteDados): void {
  this.chavePrimariaJson = JSON.stringify(registro.chavePrimaria);
  this.previaAtualizacao = null;
  this.erroDados = null;
  this.mensagemAtualizacao = null;

  void this.criarPreviaAtualizacao();
}

  async criarPreviaAtualizacao(): Promise<void> {
  if (
    this.carregandoPrevia ||
    !this.schemaDados.trim() ||
    !this.tabelaDados.trim()
  ) {
    return;
  }

  let chavePrimaria: Record<string, unknown>;

  try {
    chavePrimaria = JSON.parse(this.chavePrimariaJson) as Record<string, unknown>;

    if (
      !chavePrimaria ||
      Array.isArray(chavePrimaria) ||
      typeof chavePrimaria !== 'object' ||
      Object.keys(chavePrimaria).length === 0
    ) {
      throw new Error('Chave primária inválida.');
    }
  } catch {
    this.erroDados =
      'Informe a chave primária como JSON válido, por exemplo: {"id": "0aba1788-3a13-4f31-a8fe-d88aede530b5"}.';
    return;
  }

  this.carregandoPrevia = true;
  this.erroDados = null;
  this.mensagemAtualizacao = null;
  this.previaAtualizacao = null;

  try {
    console.log('[Prévia] Enviando consulta...');

    const resultado = await firstValueFrom(
      this.http.post<ResultadoPreviaAtualizacaoRegistro>(
        '/api/sincronizacao/registro/previa',
        {
          schema: this.schemaDados.trim(),
          tabela: this.tabelaDados.trim(),
          chavePrimaria
        }
      )
    );

    console.log('[Prévia] Resposta recebida:', resultado);

    this.previaAtualizacao = resultado;
  } catch (error: any) {
    console.error('[Prévia] Erro:', error);

    this.erroDados =
      error?.error?.mensagem ??
      error?.error?.detail ??
      'Não foi possível consultar o registro.';
  } finally {
    this.carregandoPrevia = false;
      this.cdr.detectChanges();
    console.log('[Prévia] Carregamento finalizado:', this.carregandoPrevia);
  }
}

  async aplicarAtualizacao(): Promise<void> {
    const previa = this.previaAtualizacao;
    if (!previa?.podeAplicar || !previa.previewId || this.aplicandoAtualizacao) return;
    const confirmada = window.confirm(
      `Atualizar somente este registro existente em Homologação (${previa.schema}.${previa.tabela}) com os valores de Produção?`
    );
    if (!confirmada) return;

    this.aplicandoAtualizacao = true;
    this.erroDados = null;
    try {
      const resultado = await firstValueFrom(this.http.post<{ aplicado: boolean; mensagem: string }>(
        '/api/sincronizacao/registro/aplicar',
        { previewId: previa.previewId, confirmado: true }
      ));
      this.mensagemAtualizacao = resultado.mensagem;
      this.previaAtualizacao = null;
      this.registrarHistorico('Atualização pontual', `${previa.schema}.${previa.tabela}: ${resultado.mensagem}`);
    } catch (error: any) {
      this.erroDados = error?.error?.mensagem ?? error?.error?.detail ?? 'Não foi possível aplicar a atualização. Gere uma nova prévia e tente novamente.';
      this.previaAtualizacao = null;
    } finally {
      this.aplicandoAtualizacao = false;
    }
  }

  formatarJson(valor: string): string {
    try { return JSON.stringify(JSON.parse(valor), null, 2); }
    catch { return valor; }
  }

  trackDiferenca(_index: number, item: CampoDivergenteRegistro): string { return item.coluna; }

  cancelarComparacaoDados(): void {
    this.comparacaoDadosSubscription?.unsubscribe();
    this.comparacaoDadosSubscription = undefined;
    this.carregandoDados = false;
  }

  temDiferencasDados(): boolean {
    return !!this.resultadoDados && Object.keys(this.resultadoDados.diferencasPorColuna).length > 0;
  }

  totalDiferencas(resultado: ResultadoComparacaoEstrutura): number {
    return resultado.tabelasApenasNaProducao.length + resultado.tabelasApenasNaHomologacao.length +
      resultado.diferencasColunas.length + resultado.diferencasChavesPrimarias.length +
      resultado.diferencasChavesEstrangeiras.length + resultado.diferencasIndices.length +
      resultado.diferencasConstraints.length + resultado.diferencasOutrosObjetos.length;
  }

  private registrarHistorico(operacao: string, resumo: string): void {
    this.historico = [{ operacao, resumo, data: new Date().toISOString() }, ...this.historico].slice(0, 20);
  }

  private statusDaResposta(
    resposta: PromiseSettledResult<StatusConexao | undefined>,
    ambiente: string
  ): StatusConexao {
    if (resposta.status === 'fulfilled' && resposta.value) return resposta.value;
    return { conectado: false, ambiente, mensagem: 'Não foi possível conectar.' };
  }

  
async carregarHistorico(): Promise<void> {
  this.carregandoHistorico = true;
  this.erroHistorico = null;
  this.cdr.detectChanges();

  console.log('[Histórico] Iniciando consulta à API...');

  try {
    const resposta = await firstValueFrom(
      this.http.get<TarefaApi[]>('/api/tarefas', {
        params: { limite: 50 }
      })
    );

    this.tarefas = resposta;
    console.log('[Histórico] Tarefas carregadas:', this.tarefas.length);
  } catch (error) {
    console.error('[Histórico] Falha na consulta:', error);
    this.erroHistorico = 'Falha ao carregar o histórico. Consulte o console.';
  } finally {
    this.carregandoHistorico = false;
    this.cdr.detectChanges();
    console.log('[Histórico] Carregamento finalizado.');
  }
}


  duracaoTarefa(tarefa: TarefaApi): string {
  if (!tarefa.iniciadaEm || !tarefa.finalizadaEm) {
    return '—';
  }

  const inicio = new Date(tarefa.iniciadaEm).getTime();
  const fim = new Date(tarefa.finalizadaEm).getTime();

  if (!Number.isFinite(inicio) || !Number.isFinite(fim) || fim < inicio) {
    return '—';
  }

  return `${((fim - inicio) / 1000).toFixed(1)} s`;
}
}
