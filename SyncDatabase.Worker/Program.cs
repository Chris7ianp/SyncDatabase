
using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Interfaces.Estrutura;
using SyncDatabase.Application.Servicos.Comparacao;
using SyncDatabase.Infrastructure.PostgreSql.Comparacao;
using SyncDatabase.Infrastructure.PostgreSql.Estrutura;
using SyncDatabase.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<
    ILeitorEstruturaBanco,
    LeitorEstruturaPostgreSql>();

builder.Services.AddScoped<
    ILeitorColunasBanco,
    LeitorColunasPostgreSql>();

builder.Services.AddScoped<
    ILeitorChavesPrimariasBanco,
    LeitorChavesPrimariasPostgreSql>();

builder.Services.AddScoped<
    IComparadorDados,
    ComparadorDadosPostgreSql>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
