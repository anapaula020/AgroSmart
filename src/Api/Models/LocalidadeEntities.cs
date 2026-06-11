namespace Api.Models;

public class Uf
{
    public int    Id    { get; set; } // ID IBGE
    public string Sigla { get; set; } = string.Empty;
    public string Nome  { get; set; } = string.Empty;

    public ICollection<Municipio> Municipios { get; set; } = [];
}

public class Municipio
{
    public int    Id     { get; set; } // Código IBGE
    public string Nome   { get; set; } = string.Empty;
    public int    UfId   { get; set; }
    public Uf?    Uf     { get; set; }
}
