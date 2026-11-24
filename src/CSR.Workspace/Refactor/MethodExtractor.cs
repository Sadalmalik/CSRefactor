using CSR.Core.Text;
using CSR.Workspace.Loading;
using CSR.Workspace.References;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Извлечение метода из строк файла: строки - целые инструкции одного блока. Свой анализ потоков данных
    // (ExtractMethod Roslyn - внутренний API пакета Features): входы - параметры, один выход - возврат,
    // несколько выходов - out-параметры (объявленный в строках - out var в месте вызова). Отказ: переменная читается и меняется (нужен
    // ref), return / break / continue / goto из строк, await, yield, лямбда или локальная функция вокруг,
    // обобщенный метод, многострочная verbatim-строка. Метод - private, static, если не нужен экземпляр;
    // ставится после члена, строки сдвигаются на отступ тела метода без переформатирования
    public static class MethodExtractor
    {
        private const string Command = "extract";
        private const int MaxSignature = 120;

        public static async Task<RefactorPlan> PlanAsync(
            LoadedSolution loaded,
            string path,
            int startLine,
            int endLine,
            string name,
            bool verify,
            CancellationToken cancellation = default)
        {
            if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
                throw new SymbolException($"'{name}' - не идентификатор C#");

            var document = loaded.FindDocuments(path).FirstOrDefault()
                ?? throw new SymbolException($"файл {path} не входит в решение");
            var subject = $"{SourceLocations.GetRelativePath(loaded.Root, path)}:{startLine}-{endLine}";
            var text = await document.GetTextAsync(cancellation);
            if (startLine < 1 || endLine < startLine || endLine > text.Lines.Count)
                throw new SymbolException($"строки {startLine}-{endLine} вне файла (строк {text.Lines.Count})");

            var root = (await document.GetSyntaxRootAsync(cancellation))!;
            var model = (await document.GetSemanticModelAsync(cancellation))!;
            var selection = new Selection(text, root, model, startLine - 1, endLine - 1);
            var refusal = selection.Analyze();
            if (refusal != null)
                return RefactorPlan.Refuse(Command, subject, name, refusal);

            var changed = text.WithChanges(selection.BuildChanges(name));
            var solution = loaded.Current;
            var after = solution.WithDocumentText(document.Id, changed);
            var changes = await FileChanges.CollectAsync(solution, after, loaded.Root, cancellation);
            var compile = verify ? await CompileCheck.RunAsync(solution, after, loaded.Root, cancellation) : null;
            return new RefactorPlan
            {
                Command = Command,
                Subject = subject,
                Target = name,
                Changes = changes,
                Compile = compile,
                Notes = selection.Notes,
                Solution = after,
            };
        }

        // Выделенные инструкции, их потоки данных и текст нового метода
        private class Selection
        {
            private readonly SourceText m_Text;
            private readonly SyntaxNode m_Root;
            private readonly SemanticModel m_Model;
            private readonly int m_StartLine;
            private readonly int m_EndLine;
            private List<StatementSyntax> m_Statements = [];
            private MemberDeclarationSyntax m_Anchor = null!;
            private List<ISymbol> m_Inputs = [];
            private List<ISymbol> m_Outputs = [];
            private HashSet<ISymbol> m_DeclaredInside = new(SymbolEqualityComparer.Default);
            private bool m_IsStatic;
            private readonly List<TextChange> m_BodyChanges = [];
            private readonly Dictionary<string, string> m_OutTypes = new();

            public Selection(
                SourceText text,
                SyntaxNode root,
                SemanticModel model,
                int startLine,
                int endLine)
            {
                m_Text = text;
                m_Root = root;
                m_Model = model;
                m_StartLine = startLine;
                m_EndLine = endLine;
            }

            public List<string> Notes { get; } = [];

            public string? Analyze()
            {
                return FindStatements()
                    ?? FindMember()
                    ?? CheckControlFlow()
                    ?? CheckDataFlow()
                    ?? CheckTypes()
                    ?? CheckStrings();
            }

            public IEnumerable<TextChange> BuildChanges(string name)
            {
                var first = m_Text.Lines[m_StartLine];
                var last = m_Text.Lines[m_EndLine];
                var lineBreak = TextLayout.GetLineBreak(m_Text, m_StartLine);
                var statementIndent = GetIndent(m_Statements[0].SpanStart);
                var call = statementIndent + BuildCall(name) + lineBreak;
                yield return new TextChange(TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak), call);

                var anchorEnd = m_Text.Lines.GetLineFromPosition(m_Anchor.Span.End).EndIncludingLineBreak;
                var method = string.Join(lineBreak, BuildMethod(name)) + lineBreak;
                yield return new TextChange(new TextSpan(anchorEnd, 0), lineBreak + method);
            }

#region Анализ

            // Строки - целые инструкции одного блока
            private string? FindStatements()
            {
                var span = TextSpan.FromBounds(m_Text.Lines[m_StartLine].Start, m_Text.Lines[m_EndLine].End);
                var tokens = m_Root.DescendantTokens(span)
                    .Where(token => span.Contains(token.Span) && token.Span.Length > 0)
                    .ToList();
                if (tokens.Count == 0)
                    return "в строках нет кода";

                var firstToken = tokens[0];
                var lastToken = tokens[^1];
                foreach (var statement in firstToken.Parent!.AncestorsAndSelf().OfType<StatementSyntax>())
                {
                    if (statement.SpanStart != firstToken.SpanStart || statement.Parent is not BlockSyntax block)
                        continue;

                    var start = block.Statements.IndexOf(statement);
                    var end = start;
                    while (end < block.Statements.Count && block.Statements[end].Span.End < lastToken.Span.End)
                        end++;

                    if (end < block.Statements.Count && block.Statements[end].Span.End == lastToken.Span.End)
                    {
                        m_Statements = block.Statements.Skip(start).Take(end - start + 1).ToList();
                        return null;
                    }
                }

                return "строки - не целые инструкции одного блока";
            }

            // Член, в который извлекается метод: метод, конструктор, аксессор; лямбда и локальная функция вокруг - отказ
            private string? FindMember()
            {
                foreach (var ancestor in m_Statements[0].Ancestors())
                {
                    switch (ancestor)
                    {
                        case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax:
                            return "строки внутри лямбды или локальной функции - не поддержано";
                        case AccessorDeclarationSyntax { Parent.Parent: MemberDeclarationSyntax member }:
                            return SetMember(member);
                        case BaseMethodDeclarationSyntax method:
                            return SetMember(method);
                    }
                }

                return "строки не в методе, конструкторе или аксессоре";
            }

            // Метод ставится после члена; static - у статического члена или когда экземпляр не нужен
            private string? SetMember(MemberDeclarationSyntax member)
            {
                if (member.Parent is not TypeDeclarationSyntax || member.Parent is InterfaceDeclarationSyntax)
                    return "член не в классе или структуре";

                m_Anchor = member;
                m_IsStatic = member.Modifiers.Any(SyntaxKind.StaticKeyword) || !UsesInstance();
                if (member is MethodDeclarationSyntax { TypeParameterList: not null })
                    Notes.Add("метод обобщенный: параметры типа в новый метод не переносятся");

                return null;
            }

            private string? CheckControlFlow()
            {
                var flow = m_Model.AnalyzeControlFlow(m_Statements[0], m_Statements[^1]);
                if (flow == null || !flow.Succeeded)
                    return "анализ потока управления не удался";

                if (flow.ReturnStatements.Length > 0)
                    return "return в строках - не поддержано";

                if (flow.ExitPoints.Length > 0 || flow.EntryPoints.Length > 0)
                    return "break, continue или goto за пределы строк - не поддержано";

                var nodes = m_Statements.SelectMany(statement => statement.DescendantNodesAndSelf(IsOwnCode));
                if (nodes.Any(node => node is AwaitExpressionSyntax))
                    return "await в строках - не поддержано";

                return nodes.Any(node => node is YieldStatementSyntax) ? "yield в строках - не поддержано" : null;
            }

            private string? CheckDataFlow()
            {
                var data = m_Model.AnalyzeDataFlow(m_Statements[0], m_Statements[^1]);
                if (data == null || !data.Succeeded)
                    return "анализ потоков данных не удался";

                m_DeclaredInside = new HashSet<ISymbol>(data.VariablesDeclared, SymbolEqualityComparer.Default);
                m_Inputs = Order(data.DataFlowsIn.Where(symbol => symbol is not IParameterSymbol { IsThis: true }));
                m_Outputs = Order(data.DataFlowsOut.Where(symbol => symbol is not IParameterSymbol { IsThis: true }));
                var both = m_Inputs.Intersect(m_Outputs, SymbolEqualityComparer.Default).Select(symbol => symbol.Name);
                if (both.Any())
                    return $"переменная читается и меняется ({string.Join(", ", both)}) - нужен ref, не поддержано";

                var byRef = m_Outputs.OfType<IParameterSymbol>().Where(parameter => parameter.RefKind != RefKind.None);
                if (byRef.Any())
                    return $"меняется параметр out / ref ({string.Join(", ", byRef.Select(item => item.Name))})";

                var usedOutside = data.ReadOutside.Concat(data.WrittenOutside);
                var leaked = m_DeclaredInside
                    .Where(symbol => usedOutside.Contains(symbol, SymbolEqualityComparer.Default))
                    .Except(m_Outputs, SymbolEqualityComparer.Default)
                    .Select(symbol => symbol.Name)
                    .ToList();
                if (leaked.Count > 0)
                    return $"переменная объявлена в строках и используется после ({string.Join(", ", leaked)})";

                if (m_Outputs.Count < 2)
                    return null;

                return m_Outputs
                    .Where(m_DeclaredInside.Contains)
                    .Select(AddOutDeclaration)
                    .FirstOrDefault(item => item != null);
            }

            // Выход, объявленный в строках, при нескольких выходах - out-параметр: в теле объявление становится
            // присваиванием, в месте вызова - out var (или out с явным типом, как в исходном объявлении)
            private string? AddOutDeclaration(ISymbol output)
            {
                var declarator = m_Statements
                    .SelectMany(statement => statement.DescendantNodes())
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(
                        node => SymbolEqualityComparer.Default.Equals(m_Model.GetDeclaredSymbol(node), output));
                bool isPlain = declarator is { Initializer: not null, Parent: VariableDeclarationSyntax declaration }
                    && declaration.Variables.Count == 1
                    && declaration.Parent is LocalDeclarationStatementSyntax { Modifiers.Count: 0 };
                if (!isPlain)
                    return $"выход {output.Name}: объявлен не одной переменной с инициализатором - не поддержано";

                var type = ((VariableDeclarationSyntax)declarator!.Parent!).Type;
                m_BodyChanges.Add(
                    new TextChange(TextSpan.FromBounds(type.SpanStart, declarator.Identifier.SpanStart), ""));
                m_OutTypes[output.Name] = type.IsVar ? "var" : type.ToString();
                return null;
            }

            private string? CheckTypes()
            {
                foreach (var symbol in m_Inputs.Concat(m_Outputs))
                {
                    var type = GetType(symbol);
                    if (type.IsAnonymousType)
                        return $"анонимный тип у {symbol.Name}";

                    if (ContainsMethodTypeParameter(type))
                        return $"тип {symbol.Name} - параметр типа обобщенного метода, не поддержано";
                }

                return null;
            }

            // Сдвиг отступа меняет текст многострочной verbatim-строки
            private string? CheckStrings()
            {
                foreach (var token in m_Statements.SelectMany(statement => statement.DescendantTokens()))
                {
                    var lines = m_Text.Lines.GetLinePositionSpan(token.Span);
                    if (lines.Start.Line == lines.End.Line)
                        continue;

                    if (IsVerbatim(token))
                        return "многострочная verbatim-строка в строках: сдвиг отступа изменит ее текст";
                }

                return null;
            }

            private static bool IsVerbatim(SyntaxToken token)
            {
                if (token.Text.StartsWith("@", StringComparison.Ordinal)
                    || token.Text.StartsWith("$@", StringComparison.Ordinal))
                    return true;

                return token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                    && token.Parent?.Parent is InterpolatedStringExpressionSyntax interpolated
                    && interpolated.StringStartToken.Text.Contains('@');
            }

            // Код экземпляра: this, base, член экземпляра типа без квалификатора
            private bool UsesInstance()
            {
                foreach (var node in m_Statements.SelectMany(statement => statement.DescendantNodesAndSelf()))
                {
                    if (node is ThisExpressionSyntax or BaseExpressionSyntax)
                        return true;

                    if (node is not SimpleNameSyntax name || IsQualified(name))
                        continue;

                    var symbol = m_Model.GetSymbolInfo(name).Symbol;
                    bool isMember = symbol is IFieldSymbol or IPropertySymbol or IEventSymbol
                        || symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary };
                    if (isMember && !symbol!.IsStatic && symbol.ContainingType != null)
                        return true;
                }

                return false;
            }

#endregion


#region Текст

            private string BuildCall(string name)
            {
                var arguments = m_Inputs.Select(symbol => symbol.Name).ToList();
                if (m_Outputs.Count > 1)
                {
                    arguments.AddRange(m_Outputs.Select(GetOutArgument));
                    return $"{name}({string.Join(", ", arguments)});";
                }

                var call = $"{name}({string.Join(", ", arguments)});";
                if (m_Outputs.Count == 0)
                    return call;

                var output = m_Outputs[0];
                if (!m_DeclaredInside.Contains(output))
                    return $"{output.Name} = {call}";

                var declared = GetDeclaredType(output);
                return $"{declared} {output.Name} = {call}";
            }

            // out var x (out T x) для выхода, объявленного в строках; out x - для объявленного до них
            private string GetOutArgument(ISymbol output)
            {
                if (m_OutTypes.TryGetValue(output.Name, out string? type))
                    return $"out {type} {output.Name}";

                return "out " + output.Name;
            }

            private List<string> BuildMethod(string name)
            {
                var memberIndent = GetIndent(m_Anchor.SpanStart);
                var bodyIndent = TextLayout.Deeper(memberIndent);
                var position = m_Anchor.SpanStart;
                var returnType = m_Outputs.Count == 1 ? Display(GetType(m_Outputs[0]), position) : "void";
                var parameters = m_Inputs
                    .Select(symbol => $"{Display(GetType(symbol), position)} {symbol.Name}")
                    .ToList();
                if (m_Outputs.Count > 1)
                    parameters.AddRange(
                        m_Outputs.Select(symbol => $"out {Display(GetType(symbol), position)} {symbol.Name}"));

                var modifiers = m_IsStatic ? "private static" : "private";
                var lines = new List<string>();
                var head = $"{memberIndent}{modifiers} {returnType} {name}(";
                var signature = head + string.Join(", ", parameters) + ")";
                if (signature.Length <= MaxSignature || parameters.Count == 0)
                {
                    lines.Add(signature);
                }
                else
                {
                    lines.Add(head);
                    for (var i = 0; i < parameters.Count; i++)
                        lines.Add(bodyIndent + parameters[i] + (i < parameters.Count - 1 ? "," : ")"));
                }

                lines.Add(memberIndent + "{");
                var single = m_Outputs.Count == 1 ? m_Outputs[0] : null;
                if (single != null && !m_DeclaredInside.Contains(single))
                    lines.Add($"{bodyIndent}{Display(GetType(single), position)} {single.Name};");

                lines.AddRange(Reindent(GetIndent(m_Statements[0].SpanStart), bodyIndent));
                if (single != null)
                    lines.Add($"{bodyIndent}return {single.Name};");

                lines.Add(memberIndent + "}");
                return lines;
            }

            // Строки выделения с правками тела (объявления выходов-out) и сдвигом отступа
            private IEnumerable<string> Reindent(string from, string to)
            {
                var span = TextSpan.FromBounds(m_Text.Lines[m_StartLine].Start, m_Text.Lines[m_EndLine].End);
                var changes = m_BodyChanges
                    .Select(change => new TextSpan(change.Span.Start - span.Start, change.Span.Length))
                    .Select(removed => new TextChange(removed, ""))
                    .OrderBy(change => change.Span.Start);
                var selected = SourceText.From(m_Text.ToString(span)).WithChanges(changes);
                return selected.Lines.Select(line => TextLayout.Reindent(line.ToString(), from, to));
            }

            // Тип в объявлении выходной переменной: var или явный тип, как в исходном объявлении
            private string GetDeclaredType(ISymbol output)
            {
                var declarator = m_Statements
                    .SelectMany(statement => statement.DescendantNodes())
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(node => node.Identifier.ValueText == output.Name);
                if (declarator?.Parent is VariableDeclarationSyntax { Type: var type })
                    return type.ToString();

                return "var";
            }

#endregion

            private static bool IsOwnCode(SyntaxNode node)
                => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);

            private static bool IsQualified(SimpleNameSyntax name)
            {
                return name.Parent switch
                {
                    MemberAccessExpressionSyntax access => access.Name == name,
                    QualifiedNameSyntax qualified => qualified.Right == name,
                    MemberBindingExpressionSyntax => true,
                    _ => false,
                };
            }

            private static List<ISymbol> Order(IEnumerable<ISymbol> symbols)
            {
                return symbols
                    .OrderBy(symbol => symbol is IParameterSymbol ? 0 : 1)
                    .ThenBy(symbol => symbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
                    .ToList();
            }

            // Тип переменной; у var - тип инициализатора (тип var-переменной Roslyn всегда помечает как nullable)
            private ITypeSymbol GetType(ISymbol symbol)
            {
                if (symbol is IParameterSymbol parameter)
                    return parameter.Type;

                if (symbol is not ILocalSymbol local)
                    throw new InvalidOperationException($"не переменная: {symbol}");

                var syntax = local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (syntax is not VariableDeclaratorSyntax { Initializer.Value: { } value } declarator)
                    return local.Type;

                if (declarator.Parent is VariableDeclarationSyntax { Type.IsVar: true })
                    return m_Model.GetTypeInfo(value).Type ?? local.Type;

                return local.Type;
            }

            private static bool ContainsMethodTypeParameter(ITypeSymbol type)
            {
                return type switch
                {
                    ITypeParameterSymbol parameter => parameter.DeclaringMethod != null,
                    IArrayTypeSymbol array => ContainsMethodTypeParameter(array.ElementType),
                    INamedTypeSymbol named => named.TypeArguments.Any(ContainsMethodTypeParameter),
                    _ => false,
                };
            }

            private string Display(ITypeSymbol type, int position)
                => type.ToMinimalDisplayString(m_Model, position);

            private string GetIndent(int position)
                => TextLayout.GetIndent(m_Text, position);
        }
    }
}
