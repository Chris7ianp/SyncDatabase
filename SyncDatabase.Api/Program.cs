using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Interfaces.Estrutura;
using SyncDatabase.Application.Servicos.Comparacao;
using SyncDatabase.Infrastructure.PostgreSql.Comparacao;
using SyncDatabase.Infrastructure.PostgreSql.Estrutura;
using System.Reflection;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var caminho = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(caminho))
        options.IncludeXmlComments(caminho);
});

builder.Services.AddScoped<
    ILeitorEstruturaBanco,
    LeitorEstruturaPostgreSql>();

builder.Services.AddScoped<
    IComparadorEstrutura,
    ComparadorEstruturaPostgreSql>();

builder.Services.AddScoped<
    ILeitorColunasBanco,
    LeitorColunasPostgreSql>();

builder.Services.AddScoped<
    IComparadorColunas,
    ComparadorColunas>();

builder.Services.AddScoped<
    ILeitorChavesPrimariasBanco,
    LeitorChavesPrimariasPostgreSql>();

builder.Services.AddScoped<
    IComparadorChavesPrimarias,
    ComparadorChavesPrimarias>();

builder.Services.AddScoped<
    ILeitorChavesEstrangeirasBanco,
    LeitorChavesEstrangeirasPostgreSql>();

builder.Services.AddScoped<
    IComparadorChavesEstrangeiras,
    ComparadorChavesEstrangeiras>();

builder.Services.AddScoped<ILeitorIndicesBanco, LeitorIndicesPostgreSql>();
builder.Services.AddScoped<IComparadorIndices, ComparadorIndices>();
builder.Services.AddScoped<ILeitorConstraintsBanco, LeitorConstraintsPostgreSql>();
builder.Services.AddScoped<IComparadorConstraints, ComparadorConstraints>();
builder.Services.AddScoped<ILeitorObjetosAdicionaisBanco, LeitorObjetosAdicionaisPostgreSql>();
builder.Services.AddScoped<IComparadorObjetosAdicionais, ComparadorObjetosAdicionais>();
builder.Services.AddScoped<IComparadorDados, ComparadorDadosPostgreSql>();
builder.Services.AddScoped<ISincronizadorRegistro, SincronizadorRegistroPostgreSql>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:56382"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();

app.UseCors("AngularDev");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
