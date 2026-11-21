using CSR.Core.Arch.Checks;

namespace CSR.Core.Arch
{
    // Архитектурные признаки по синтаксису одного файла; конфиг arch.jsonc включает их по имени.
    // Признаки по всему решению (одна реализация, мертвый код, повторяющиеся параметры) - не здесь
    public static class ArchCatalog
    {
        public static readonly IReadOnlyList<ArchCheck> All =
        [
            new TypeBranchCheck(),
            new BehaviourLogicCheck(),
            new IndexApiCheck(),
            new StaticDataMethodsCheck(),
            new ProjectMechanismCheck(),
            new NumericConstantCheck(),
            new NumericLiteralCheck(),
            new TupleSignatureCheck(),
            new PartialFileCheck(),
            new DebugDrawCheck(),
            new ObsoleteReasonCheck(),
            new PersistedClassCheck(),
            new VariantFlagCheck(),
            new SingleImplementationCheck(),
            new DeadMemberCheck(),
            new ParameterSetCheck(),
            new UnraisedEventCheck(),
            new ClassCohesionCheck(),
        ];

        public static ArchCheck? Find(string name)
            => All.FirstOrDefault(check => check.Name == name);

        // Вопрос признака по имени; null - имя не из каталога
        public static string? GetQuestion(string name)
            => Find(name)?.Question;
    }
}
