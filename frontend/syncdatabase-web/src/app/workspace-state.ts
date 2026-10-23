import { Injectable } from '@angular/core';

export interface StatusConexao {
  conectado: boolean;
  ambiente?: string;
  nome?: string;
  mensagem: string;
}

export interface ResultadoComparacaoEstrutura {
  dataComparacao: string;
  comparacaoEstruturalCompleta?: boolean;
  tabelasApenasNaProducao: string[];
  tabelasApenasNaHomologacao: string[];
  tabelasEmAmbosAmbientes: string[];
  diferencasColunas: Record<string, unknown>[];
  diferencasChavesPrimarias: Record<string, unknown>[];
  diferencasChavesEstrangeiras: Record<string, unknown>[];
  diferencasIndices: Record<string, unknown>[];
  diferencasConstraints: Record<string, unknown>[];
  diferencasOutrosObjetos: Record<string, unknown>[];
  categoriasNaoComparadas: string[];
}

export interface RegistroDivergenteDados {
  chavePrimaria: Record<string, unknown>;
  colunasDivergentes: string[];
}

export interface ResultadoComparacaoDados {
  schema: string;
  tabela: string;
  comparavel: boolean;
  motivoNaoComparavel?: string;
  dataComparacao: string;
  tamanhoLote: number;
  registrosProducao: number;
  registrosHomologacao: number;
  registrosApenasNaProducao: number;
  registrosApenasNaHomologacao: number;
   registrosDivergentes: RegistroDivergenteDados[];
  registrosComDiferencas: number;
  diferencasPorColuna: Record<string, number>;
  colunasIgnoradasPorTipo: string[];
  amostrasChaveAnonimizadasApenasNaProducao: string[];
  amostrasChaveAnonimizadasApenasNaHomologacao: string[];
 
}

export interface CampoDivergenteRegistro {
  coluna: string;
  tipoDados: string;
  valorProducaoJson: string;
  valorHomologacaoJson: string;
}

export interface ResultadoPreviaAtualizacaoRegistro {
  schema: string;
  tabela: string;
  registroEncontradoNaProducao: boolean;
  registroEncontradoNaHomologacao: boolean;
  podeAplicar: boolean;
  mensagem?: string;
  previewId?: string;
  colunasIgnoradasPorTipo: string[];
  diferencas: CampoDivergenteRegistro[];
}

export interface RegistroHistorico {
  operacao: string;
  resumo: string;
  data: string;
}

export interface TarefaApi {
  id: string;
  tipo: string;
  parametros: Record<string, unknown>;
  status: 'Pendente' | 'EmProcessamento' | 'Concluida' | 'Falhou';
  resultado: Record<string, unknown> | null;
  erro: string | null;
  criadaEm: string;
  iniciadaEm: string | null;
  finalizadaEm: string | null;
  tentativas: number;
}

@Injectable({ providedIn: 'root' })
export class WorkspaceState {
  origem: StatusConexao = { conectado: false, mensagem: 'Aguardando verificação' };
  destino: StatusConexao = { conectado: false, mensagem: 'Aguardando verificação' };
  resultado: ResultadoComparacaoEstrutura | null = null;
  resultadoDados: ResultadoComparacaoDados | null = null;
  historico: RegistroHistorico[] = [];
}
