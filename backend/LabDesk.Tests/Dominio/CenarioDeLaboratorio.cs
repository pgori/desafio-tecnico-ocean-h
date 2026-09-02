using System.Reflection;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Pacientes;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Monta um catalogo pequeno de laboratorio para os testes de dominio.
///
/// As entidades tem Id gerado pelo banco, entao aqui ele e atribuido por reflexao.
/// E o preco de manter os setters privados no dominio: preferi pagar isso no teste
/// a abrir a entidade so para facilitar a montagem do cenario.
/// </summary>
public class CenarioDeLaboratorio
{
    private int _proximoId = 1;

    public TipoTubo Azul { get; }
    public TipoTubo Amarela { get; }
    public TipoTubo Roxa { get; }
    public TipoTubo Cinza { get; }

    /// <summary>Hemograma: tubo roxo, setor de Hematologia, sem jejum.</summary>
    public Exame Hemograma { get; }

    /// <summary>Hemoglobina glicada: mesmo tubo roxo do hemograma, mas outro setor.</summary>
    public Exame HemoglobinaGlicada { get; }

    /// <summary>Reticulocitos: mesmo tubo e mesmo setor do hemograma.</summary>
    public Exame Reticulocitos { get; }

    /// <summary>Glicemia: tubo cinza e exige 8 horas de jejum.</summary>
    public Exame Glicemia { get; }

    /// <summary>Coagulograma: tubo azul, o primeiro da ordem de coleta.</summary>
    public Exame Coagulograma { get; }

    /// <summary>Creatinina: tubo amarelo, sem jejum.</summary>
    public Exame Creatinina { get; }

    public MotivoRejeicao Hemolise { get; }
    public MotivoRejeicao TuboExtra { get; }

    public Paciente Paciente { get; }

    public CenarioDeLaboratorio()
    {
        Azul = Criar(new TipoTubo("Azul", "Citrato de sódio", 1, 2.7m));
        Amarela = Criar(new TipoTubo("Amarela", "Gel separador", 2, 5.0m));
        Roxa = Criar(new TipoTubo("Roxa", "EDTA K3", 4, 4.0m));
        Cinza = Criar(new TipoTubo("Cinza", "Fluoreto de sódio", 5, 2.0m));

        Hemograma = CriarExame("HEMOG", "Hemograma completo", Roxa, false, 0, "Hematologia");
        Reticulocitos = CriarExame("RETIC", "Reticulócitos", Roxa, false, 0, "Hematologia");
        HemoglobinaGlicada = CriarExame("HBA1C", "Hemoglobina glicada", Roxa, false, 0, "Bioquímica");
        Glicemia = CriarExame("GLI", "Glicemia de jejum", Cinza, true, 8, "Bioquímica");
        Coagulograma = CriarExame("TP", "Tempo de protrombina", Azul, false, 0, "Hemostasia");
        Creatinina = CriarExame("CREA", "Creatinina", Amarela, false, 0, "Bioquímica");

        Hemolise = Criar(new MotivoRejeicao("HEMOLISE", "Amostra hemolisada", exigeRecoleta: true));
        TuboExtra = Criar(new MotivoRejeicao("EXTRA", "Tubo coletado a mais", exigeRecoleta: false));

        Paciente = Criar(new Paciente("Maria Aparecida de Souza", new DateOnly(1948, 3, 12), "123", null));
    }

    private Exame CriarExame(string codigo, string nome, TipoTubo tubo, bool jejum, int horas, string setor)
    {
        var exame = Criar(new Exame(codigo, nome, tubo.Id, jejum, horas, setor));
        Definir(exame, nameof(Exame.TipoTubo), tubo);
        return exame;
    }

    private T Criar<T>(T entidade)
    {
        Definir(entidade!, "Id", _proximoId++);
        return entidade;
    }

    private static void Definir(object entidade, string propriedade, object valor)
    {
        var info = entidade.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)!;
        info.GetSetMethod(nonPublic: true)!.Invoke(entidade, [valor]);
    }

    /// <summary>Atribui Id as amostras recem-criadas, que no uso real viria do banco.</summary>
    public static void NumerarAmostras(IEnumerable<object> amostras, int inicio = 100)
    {
        var id = inicio;
        foreach (var amostra in amostras)
            Definir(amostra, "Id", id++);
    }
}
