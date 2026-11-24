using CSR.Workspace.Loading;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Перенос члена в другой тип. Статический член (метод, свойство, поле, константа): ссылки получают
    // квалификатор типа-приемника, в теле члена ссылки на статические члены исходного типа - квалификатор
    // исходного. Метод экземпляра - только с параметром типа-приемника: приемник есть в каждом месте вызова
    // (аргумент), вызов M(a, p) становится p.M(a), в теле p.X - X, p - this. Иначе отказ. Данные экземпляра
    // и сериализуемые члены не переносятся. Строки члена переносятся как есть со сдвигом отступа
    public static class MemberMover
    {
        private const string Command = "move";

        public static async Task<RefactorPlan> PlanAsync(
            LoadedSolution loaded,
            ISymbol member,
            INamedTypeSymbol target,
            RefactorOptions options,
            bool verify,
            CancellationToken cancellation = default)
        {
            var subject = SymbolText.Qualified(member);
            var targetName = SymbolText.Qualified(target);
            var solution = loaded.Current;
            var refusal = GetRefusal(member, target, options);
            if (refusal != null)
                return RefactorPlan.Refuse(Command, subject, targetName, refusal);

            var move = new MoveContext(solution, member, target);
            refusal = await move.PrepareAsync(cancellation);
            if (refusal != null)
                return RefactorPlan.Refuse(Command, subject, targetName, refusal);

            var after = await move.BuildAsync(cancellation);
            var changes = await FileChanges.CollectAsync(solution, after, loaded.Root, cancellation);
            var compile = verify ? await CompileCheck.RunAsync(solution, after, loaded.Root, cancellation) : null;
            return new RefactorPlan
            {
                Command = Command,
                Subject = subject,
                Target = targetName,
                Changes = changes,
                Compile = compile,
                Notes = move.Notes,
                Solution = after,
            };
        }

        private static string? GetRefusal(ISymbol member, INamedTypeSymbol target, RefactorOptions options)
        {
            var source = member.ContainingType;
            bool isInSource = member.Locations.Any(location => location.IsInSource)
                && target.Locations.Any(location => location.IsInSource);
            if (!isInSource)
                return "член или тип-приемник из метаданных";

            if (member is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IFieldSymbol))
                return "переносятся методы, свойства и поля";

            if (SymbolEqualityComparer.Default.Equals(source, target))
                return "член уже в этом типе";

            if (target.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                return "тип-приемник - не класс и не структура";

            if (source.IsGenericType || target.IsGenericType)
                return "обобщенный исходный тип или тип-приемник - не поддержано";

            if (member.IsOverride || member.IsVirtual || member.IsAbstract || IsInterfaceImplementation(member))
                return "член переопределяет, переопределяется или реализует интерфейс";

            if (member.DeclaringSyntaxReferences.Length != 1)
                return "partial-член - не поддержано";

            if (target.GetMembers(member.Name).Any())
                return $"в {target.Name} уже есть член {member.Name}";

            var serialized = SerializationGuard.Check(member, options.SerializationAttributes);
            if (serialized.Count > 0)
                return $"данные сериализуются ({serialized[0].Reason}) - перенос между типами потеряет их";

            return member switch
            {
                IFieldSymbol { IsStatic: false, IsConst: false } => "поле экземпляра - данные объекта, не переносится",
                IPropertySymbol { IsStatic: false } => "свойство экземпляра - не поддержано",
                _ => null,
            };
        }

        private static bool IsInterfaceImplementation(ISymbol member)
        {
            var type = member.ContainingType;
            return type.AllInterfaces
                .SelectMany(face => face.GetMembers())
                .Select(type.FindImplementationForInterfaceMember)
                .Any(implementation => SymbolEqualityComparer.Default.Equals(implementation, member));
        }

        // Правки документов одного переноса
        private class MoveContext
        {
            private readonly Solution m_Solution;
            private readonly ISymbol m_Member;
            private readonly INamedTypeSymbol m_Target;
            private readonly INamedTypeSymbol m_Source;
            private readonly Dictionary<DocumentId, DocumentEdit> m_Edits = new();
            private MemberDeclarationSyntax m_Declaration = null!;
            private TypeDeclarationSyntax m_TargetDeclaration = null!;
            private SemanticModel m_SourceModel = null!;
            private IParameterSymbol? m_Receiver;
            private List<Location> m_References = [];

            public MoveContext(Solution solution, ISymbol member, INamedTypeSymbol target)
            {
                m_Solution = solution;
                m_Member = member;
                m_Target = target;
                m_Source = member.ContainingType;
            }

            public List<string> Notes { get; } = [];

            public async Task<string?> PrepareAsync(CancellationToken cancellation)
            {
                var syntax = await m_Member.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellation);
                m_Declaration = syntax.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();
                if (m_Declaration is BaseFieldDeclarationSyntax { Declaration.Variables.Count: > 1 })
                    return "поле в объявлении с несколькими переменными - не поддержано";

                if (m_Declaration.DescendantTrivia().Any(trivia => trivia.IsDirective))
                    return "директивы препроцессора в члене - не поддержано";

                var targetSyntax = await m_Target.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellation);
                m_TargetDeclaration = (TypeDeclarationSyntax)targetSyntax;
                var sourceDocument = m_Solution.GetDocument(m_Declaration.SyntaxTree);
                var targetDocument = m_Solution.GetDocument(m_TargetDeclaration.SyntaxTree);
                if (sourceDocument is null or SourceGeneratedDocument
                    || targetDocument is null or SourceGeneratedDocument)
                    return "член или тип-приемник в сгенерированном коде - не поддержано";

                m_SourceModel = (await sourceDocument.GetSemanticModelAsync(cancellation))!;
                m_References = await GetReferencesAsync(cancellation);
                bool isGenerated = m_References
                    .Any(location => m_Solution.GetDocument(location.SourceTree) is null or SourceGeneratedDocument);
                if (isGenerated)
                    return "ссылка в сгенерированном коде: генератор пересоздаст ее со старым типом - не поддержано";

                if (m_Member is IMethodSymbol { IsStatic: false } method)
                {
                    var refusal = FindReceiver(method);
                    if (refusal != null)
                        return refusal;
                }

                return CheckAccess();
            }

            public async Task<Solution> BuildAsync(CancellationToken cancellation)
            {
                var sourceDocument = m_Solution.GetDocument(m_Declaration.SyntaxTree)!;
                var targetDocument = m_Solution.GetDocument(m_TargetDeclaration.SyntaxTree)!;
                var targetModel = (await targetDocument.GetSemanticModelAsync(cancellation))!;
                var targetEdit = await GetEditAsync(targetDocument, cancellation);
                var closeBrace = m_TargetDeclaration.CloseBraceToken.SpanStart;
                var insertion = targetEdit.Text.Lines.GetLineFromPosition(closeBrace).Start;

                foreach (var location in m_References)
                    await EditReferenceAsync(location, cancellation);

                var sourceEdit = await GetEditAsync(sourceDocument, cancellation);
                sourceEdit.RemoveLines(SourceLines.GetNodeLines(sourceEdit.Text, m_Declaration));
                targetEdit.Insert(insertion, BuildMemberText(targetModel, insertion, targetEdit.Text));
                EditUsings(targetEdit, sourceEdit, sourceDocument.Id == targetDocument.Id);

                var after = m_Solution;
                foreach (var (id, edit) in m_Edits)
                    after = after.WithDocumentText(id, edit.Apply());

                return after;
            }

#region Проверки

            // Ссылки вне объявления самого члена (рекурсивный вызов в теле переносится вместе с ним)
            private async Task<List<Location>> GetReferencesAsync(CancellationToken cancellation)
            {
                var found = await SymbolFinder.FindReferencesAsync(m_Member, m_Solution, cancellation);
                return found
                    .SelectMany(reference => reference.Locations)
                    .Where(location => !location.IsImplicit && location.Location.IsInSource)
                    .Select(location => location.Location)
                    .Where(location => !IsInDeclaration(location))
                    .ToList();
            }

            private bool IsInDeclaration(Location location)
            {
                return location.SourceTree == m_Declaration.SyntaxTree
                    && m_Declaration.Span.Contains(location.SourceSpan);
            }

            // Приемник метода экземпляра - единственный параметр типа-приемника; используется только вызовом
            private string? FindReceiver(IMethodSymbol method)
            {
                var receivers = method.Parameters
                    .Where(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, m_Target))
                    .ToList();
                if (receivers.Count != 1 || receivers[0].RefKind != RefKind.None)
                {
                    var target = m_Target.Name;
                    return $"метод экземпляра без единственного параметра типа {target}: приемника в местах вызова нет";
                }

                m_Receiver = receivers[0];
                if (m_References.Any(location => GetInvocation(FindName(location)) == null))
                    return "метод используется не только вызовом (группа методов, делегат) - не поддержано";

                return null;
            }

            // Тело не использует экземпляр и закрытые члены исходного типа; закрытый член не используется вне себя
            private string? CheckAccess()
            {
                var privates = new List<string>();
                foreach (var node in m_Declaration.DescendantNodes())
                {
                    if (node is ThisExpressionSyntax or BaseExpressionSyntax)
                        return $"член использует экземпляр {m_Source.Name} (this / base)";

                    if (node is not SimpleNameSyntax name)
                        continue;

                    var symbol = m_SourceModel.GetSymbolInfo(name).Symbol;
                    if (symbol == null || IsMember(symbol) || !IsSourceMember(symbol))
                        continue;

                    if (!symbol.IsStatic && symbol is not INamedTypeSymbol && !IsQualifiedByOther(name))
                        return $"член использует экземпляр {m_Source.Name} ({symbol.Name})";

                    if (symbol.DeclaredAccessibility == Accessibility.Private)
                        privates.Add(symbol.Name);
                }

                if (privates.Count > 0)
                    return $"член использует private-члены {m_Source.Name}: {string.Join(", ", privates.Distinct())}";

                if (m_Member.DeclaredAccessibility == Accessibility.Private && m_References.Count > 0)
                    return $"private-член используется вне себя: в {m_Target.Name} будет недоступен";

                return null;
            }

            private bool IsMember(ISymbol symbol)
                => SymbolEqualityComparer.Default.Equals(symbol, m_Member);

            // Член исходного типа: поле, свойство, событие, метод (не локальная функция и не лямбда), вложенный тип;
            // у параметров и локальных переменных ContainingType - тоже исходный тип, они не члены
            private bool IsSourceMember(ISymbol symbol)
            {
                bool isMethod = symbol is IMethodSymbol method
                    && method.MethodKind is not (MethodKind.LocalFunction or MethodKind.LambdaMethod);
                if (!isMethod && symbol is not (IFieldSymbol or IPropertySymbol or IEventSymbol or INamedTypeSymbol))
                    return false;

                return SymbolEqualityComparer.Default.Equals(symbol.ContainingType, m_Source);
            }

#endregion


#region Ссылки

            // X.M -> Target.M; M без квалификатора вне приемника -> Target.M; вызов метода экземпляра -> p.M(...)
            private async Task EditReferenceAsync(Location location, CancellationToken cancellation)
            {
                var document = m_Solution.GetDocument(location.SourceTree)!;
                var edit = await GetEditAsync(document, cancellation);
                var model = (await document.GetSemanticModelAsync(cancellation))!;
                var name = FindName(location);
                if (m_Receiver != null)
                {
                    EditInstanceCall(edit, name);
                    return;
                }

                var target = m_Target.ToMinimalDisplayString(model, name.SpanStart);
                switch (name.Parent)
                {
                    case MemberAccessExpressionSyntax access when access.Name == name:
                        if (model.GetSymbolInfo(access.Expression).Symbol is INamedTypeSymbol)
                            edit.Replace(access.Expression.Span, target);
                        return;
                    case QualifiedNameSyntax qualified when qualified.Right == name:
                        edit.Replace(qualified.Left.Span, target);
                        return;
                }

                bool isInTarget = name.Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .Any(type => SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(type), m_Target));
                if (!isInTarget)
                    edit.Insert(name.SpanStart, target + ".");
            }

            // M(a, p, b) или x.M(a, p, b) -> p.M(a, b); сложный аргумент-приемник - в скобках
            private void EditInstanceCall(DocumentEdit edit, SimpleNameSyntax name)
            {
                var invocation = GetInvocation(name)!;
                var arguments = invocation.ArgumentList.Arguments;
                var receiver = arguments.FirstOrDefault(IsReceiverName) ?? arguments[m_Receiver!.Ordinal];
                var rest = arguments.Where(argument => argument != receiver).Select(argument => argument.ToString());
                var expression = receiver.Expression;
                bool isSimple = expression is IdentifierNameSyntax or MemberAccessExpressionSyntax
                    or InvocationExpressionSyntax or ThisExpressionSyntax or ElementAccessExpressionSyntax;
                var target = isSimple ? expression.ToString() : $"({expression})";
                edit.Replace(invocation.Span, $"{target}.{name}({string.Join(", ", rest)})");
            }

            private bool IsReceiverName(ArgumentSyntax argument)
                => argument.NameColon?.Name.Identifier.ValueText == m_Receiver!.Name;

            private static InvocationExpressionSyntax? GetInvocation(SimpleNameSyntax name)
            {
                ExpressionSyntax expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name
                    ? access
                    : name;
                return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression
                    ? invocation
                    : null;
            }

            private static SimpleNameSyntax FindName(Location location)
            {
                var root = location.SourceTree!.GetRoot();
                var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
                return node as SimpleNameSyntax ?? node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().First();
            }

#endregion


#region Текст члена

            // Строки члена для приемника: статические члены исходного типа - с квалификатором, у метода
            // экземпляра - без параметра-приемника, p.X -> X, p -> this; отступ - как у членов приемника
            private string BuildMemberText(SemanticModel targetModel, int insertion, SourceText targetText)
            {
                var sourceText = m_Declaration.SyntaxTree.GetText();
                var lines = SourceLines.GetNodeLines(sourceText, m_Declaration);
                var offset = lines.Start;
                var edit = new DocumentEdit(SourceText.From(sourceText.ToString(lines)));
                var sourceName = m_Source.ToMinimalDisplayString(targetModel, insertion);
                var parameters = (m_Declaration as MethodDeclarationSyntax)?.ParameterList.Span;
                foreach (var name in m_Declaration.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    // список параметров метода экземпляра пишется заново (без приемника)
                    if (m_Receiver != null && parameters?.Contains(name.Span) == true)
                        continue;

                    var symbol = m_SourceModel.GetSymbolInfo(name).Symbol;
                    if (symbol == null || IsMember(symbol))
                        continue;

                    if (m_Receiver != null && SymbolEqualityComparer.Default.Equals(symbol, m_Receiver))
                    {
                        EditReceiverUse(edit, name, offset);
                        continue;
                    }

                    bool isStatic = symbol.IsStatic || symbol is INamedTypeSymbol;
                    if (IsSourceMember(symbol) && isStatic && !IsQualifiedByOther(name))
                        edit.Insert(name.SpanStart - offset, sourceName + ".");
                }

                if (m_Receiver != null)
                    RemoveReceiverParameter(edit, offset);

                var from = GetIndent(sourceText, m_Declaration.SpanStart);
                var to = GetMemberIndent(targetText);
                var lineBreak = SourceLines.GetLineBreak(targetText);
                var text = Reindent(edit.Apply().ToString(), from, to).TrimEnd('\r', '\n') + lineBreak;
                var previous = insertion > 0
                    ? targetText.Lines.GetLineFromPosition(insertion - 1).ToString().Trim()
                    : "";
                return previous == "{" ? text : lineBreak + text;
            }

            // p.X -> X, p -> this
            private static void EditReceiverUse(DocumentEdit edit, SimpleNameSyntax name, int offset)
            {
                if (name.Parent is MemberAccessExpressionSyntax access && access.Expression == name)
                {
                    var span = TextSpan.FromBounds(name.SpanStart, access.OperatorToken.Span.End);
                    edit.Replace(new TextSpan(span.Start - offset, span.Length), "");
                    return;
                }

                edit.Replace(new TextSpan(name.SpanStart - offset, name.Span.Length), "this");
            }

            private void RemoveReceiverParameter(DocumentEdit edit, int offset)
            {
                var list = ((MethodDeclarationSyntax)m_Declaration).ParameterList;
                var parameters = list.Parameters
                    .Where(parameter => parameter.Identifier.ValueText != m_Receiver!.Name)
                    .Select(parameter => parameter.ToString());
                var span = TextSpan.FromBounds(list.OpenParenToken.Span.End, list.CloseParenToken.SpanStart);
                edit.Replace(new TextSpan(span.Start - offset, span.Length), string.Join(", ", parameters));
            }

            // Отступ членов приемника: первого члена, иначе на уровень глубже объявления типа
            private string GetMemberIndent(SourceText targetText)
            {
                var first = m_TargetDeclaration.Members.FirstOrDefault();
                if (first != null)
                    return GetIndent(targetText, first.SpanStart);

                var indent = GetIndent(targetText, m_TargetDeclaration.SpanStart);
                return indent.EndsWith("\t", StringComparison.Ordinal) ? indent + "\t" : indent + "    ";
            }

            private static string Reindent(string text, string from, string to)
            {
                var lines = text.Split('\n').Select(line =>
                {
                    var content = line.TrimEnd('\r');
                    if (content.Trim().Length == 0)
                        return line.Substring(content.Length);

                    return content.StartsWith(from, StringComparison.Ordinal)
                        ? to + line.Substring(from.Length)
                        : line;
                });
                return string.Join("\n", lines);
            }

            private static string GetIndent(SourceText text, int position)
            {
                var content = text.Lines.GetLineFromPosition(position).ToString();
                return content.Substring(0, content.Length - content.TrimStart().Length);
            }

#endregion


#region Директивы using

            // В файл приемника - директивы исходного файла, нужные члену; из исходного - ставшие лишними
            private void EditUsings(DocumentEdit targetEdit, DocumentEdit sourceEdit, bool isSameDocument)
            {
                var sourceRoot = (CompilationUnitSyntax)m_Declaration.SyntaxTree.GetRoot();
                var moved = UsingAnalysis.Collect(m_SourceModel, [m_Declaration]);
                if (!isSameDocument)
                {
                    var targetRoot = (CompilationUnitSyntax)m_TargetDeclaration.SyntaxTree.GetRoot();
                    var present = targetRoot.Usings.Select(item => item.ToString()).ToHashSet();
                    var missing = sourceRoot.Usings
                        .Where(item => item.GlobalKeyword == default)
                        .Where(item => UsingAnalysis.IsNeeded(m_SourceModel, item, moved))
                        .Where(item => !IsTargetNamespace(item))
                        .Select(item => item.ToString())
                        .Where(item => !present.Contains(item))
                        .ToList();
                    if (missing.Count > 0)
                        InsertUsings(targetEdit, targetRoot, missing);
                }

                var rest = sourceRoot.Members.Concat<SyntaxNode>(sourceRoot.AttributeLists);
                var remaining = UsingAnalysis.Collect(m_SourceModel, rest, m_Declaration);
                foreach (var directive in sourceRoot.Usings)
                {
                    bool isLost = UsingAnalysis.IsNeeded(m_SourceModel, directive, moved)
                        && !UsingAnalysis.IsNeeded(m_SourceModel, directive, remaining);
                    if (isLost)
                        sourceEdit.RemoveLines(SourceLines.GetNodeLines(sourceEdit.Text, directive));
                }
            }

            // Пространство имен приемника или его родитель: в файле приемника видно без using
            private bool IsTargetNamespace(UsingDirectiveSyntax directive)
            {
                if (directive.Alias != null || directive.StaticKeyword != default)
                    return false;

                var name = directive.NamespaceOrType.ToString();
                var space = m_Target.ContainingNamespace.ToDisplayString();
                return space == name || space.StartsWith(name + ".", StringComparison.Ordinal);
            }

            private static void InsertUsings(DocumentEdit edit, CompilationUnitSyntax root, List<string> usings)
            {
                var lineBreak = SourceLines.GetLineBreak(edit.Text);
                var block = string.Concat(usings.Select(item => item + lineBreak));
                if (root.Usings.Count == 0)
                {
                    edit.Insert(0, block + lineBreak);
                    return;
                }

                var last = edit.Text.Lines.GetLineFromPosition(root.Usings[^1].Span.End);
                edit.Insert(last.EndIncludingLineBreak, block);
            }

#endregion

            private async Task<DocumentEdit> GetEditAsync(Document document, CancellationToken cancellation)
            {
                if (m_Edits.TryGetValue(document.Id, out DocumentEdit? edit))
                    return edit;

                edit = new DocumentEdit(await document.GetTextAsync(cancellation));
                m_Edits[document.Id] = edit;
                return edit;
            }

            // Правая часть доступа к члену или квалифицированного имени, где слева не исходный тип
            private bool IsQualifiedByOther(SimpleNameSyntax name)
            {
                return name.Parent switch
                {
                    MemberAccessExpressionSyntax access when access.Name == name
                        => access.Expression is not (ThisExpressionSyntax or BaseExpressionSyntax),
                    QualifiedNameSyntax qualified => qualified.Right == name,
                    MemberBindingExpressionSyntax => true,
                    _ => false,
                };
            }
        }
    }
}
