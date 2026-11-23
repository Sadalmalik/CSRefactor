namespace CSR.Core.Style.Naming
{
    // Требование к имени - точка расширения naming: одна реализация на вид требования; ключ каскада - у вида
    // в NamingCatalog. Check - текст нарушения или null
    public interface INameRequirement
    {
        string? Check(NameSymbol symbol);
    }
}
