internal class PensadorResponse
{
    public string TermoDePesquisa { get; set; }
    public int Total { get; set; }
    public Frase[] Frases { get; set; }
}

internal class Frase
{
    public string Autor { get; set; }
    public string Texto { get; set; }
}