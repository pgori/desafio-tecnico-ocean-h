using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Infrastructure.Persistencia;

/// <summary>
/// Atendimentos ficticios em varios pontos do fluxo, para a instancia publica nao abrir vazia.
///
/// Fica desligado por padrao e separado do <see cref="SeedInicial"/> porque catalogo e dado
/// de verdade, enquanto isto aqui e so vitrine. Os atendimentos sao criados chamando os
/// metodos do dominio, entao passam pelas mesmas regras de um atendimento real.
/// </summary>
public static class SeedDemonstracao
{
    public static async Task ExecutarAsync(LabDeskDbContext db, CancellationToken ct = default)
    {
        if (await db.Atendimentos.AnyAsync(ct))
            return;

        var pacientes = await db.Pacientes.OrderBy(p => p.Id).ToListAsync(ct);
        var exames = await db.Exames.Include(e => e.TipoTubo).ToDictionaryAsync(e => e.Codigo, ct);
        var hemolise = await db.MotivosRejeicao.FirstAsync(m => m.Codigo == "HEMOLISE", ct);

        var agora = DateTime.UtcNow;
        var sequencia = 0;

        Atendimento Abrir(int indicePaciente, Prioridade prioridade, int minutosAtras, params string[] codigos)
        {
            sequencia++;
            var numero = $"{agora:yyyyMMdd}-{sequencia:D4}";
            var selecionados = codigos.Select(c => exames[c]).ToList();
            var precisaJejum = selecionados.Any(e => e.ExigeJejum);

            return new Atendimento(
                numero,
                pacientes[indicePaciente],
                selecionados,
                prioridade,
                jejumConfirmado: precisaJejum,
                observacoes: null,
                agora.AddMinutes(-minutosAtras));
        }

        // 1. Acabou de chegar e ainda nao foi chamado.
        var naFila = Abrir(1, Prioridade.Normal, 12, "TSH", "T4L", "HEMOG");

        // 2. Idosa, prioridade preferencial, ja chamada para a sala de coleta.
        var emColeta = Abrir(0, Prioridade.Preferencial, 35, "GLI", "COLT", "TRIG", "CREA");
        emColeta.ChamarParaColeta(agora.AddMinutes(-4));

        // 3. Coletado, com os tubos esperando na bancada de triagem.
        var naTriagem = Abrir(2, Prioridade.Normal, 55, "HEMOG", "HBA1C", "TP");
        naTriagem.ChamarParaColeta(agora.AddMinutes(-40));
        naTriagem.RegistrarColeta(true, "Bruno - coleta", agora.AddMinutes(-38));

        // 4. Uma amostra hemolisada: o exame voltou para a fila de recoleta.
        var comPendencia = Abrir(3, Prioridade.Normal, 90, "HEMOG", "CREA");
        comPendencia.ChamarParaColeta(agora.AddMinutes(-80));
        var tubos = comPendencia.RegistrarColeta(true, "Bruno - coleta", agora.AddMinutes(-78));

        // 5. Fluxo completo, ja liberado para os setores.
        var concluido = Abrir(4, Prioridade.Preferencial, 140, "HEMOG", "VHS");
        concluido.ChamarParaColeta(agora.AddMinutes(-130));
        var tubosConcluidos = concluido.RegistrarColeta(true, "Bruno - coleta", agora.AddMinutes(-128));

        db.Atendimentos.AddRange(naFila, emColeta, naTriagem, comPendencia, concluido);

        // As amostras so ganham Id depois de gravadas, e a triagem trabalha por Id.
        await db.SaveChangesAsync(ct);

        var hemolisada = tubos.First(a => a.TipoTubo.Cor == "Roxa");
        comPendencia.RejeitarAmostra(hemolisada.Id, hemolise, "Carla - triagem", "Soro avermelhado", agora.AddMinutes(-70));
        comPendencia.AceitarAmostra(tubos.First(a => a.Id != hemolisada.Id).Id, "Carla - triagem", agora.AddMinutes(-70));

        foreach (var amostra in tubosConcluidos)
        {
            concluido.AceitarAmostra(amostra.Id, "Carla - triagem", agora.AddMinutes(-120));
            concluido.EncaminharAmostra(amostra.Id, "Carla - triagem", agora.AddMinutes(-119));
        }

        await db.SaveChangesAsync(ct);
    }
}
