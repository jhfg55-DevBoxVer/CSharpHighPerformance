using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SafeManualMemoryManagement.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OwnershipAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OwnershipViolation";
        private static readonly LocalizableString Title = "����Ȩ����";
        private static readonly LocalizableString MessageFormat = "���� '{0}' ������Ȩת�ƺ��Ա�ʹ��";
        private static readonly LocalizableString Description = "�ڴ��� [MSWGC] ��ǵĴ�����ڣ�����������Ȩת�ƺ�Ӧ����ʹ��";
        private const string Category = "Ownership";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // ��ֹ�������ɴ��룬�������
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // �����ֲ���������ֵ���ʽ�Լ���ʶ������
            context.RegisterSyntaxNodeAction(AnalyzeNode,
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.IdentifierName);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // ���� [MSWGC] ��ǵĴ��������Ч
            if (!IsInMSWGCScope(context.Node))
                return;

            // ʾ���������ڸ�ֵ���ʽ�з�������Ȩת�ƣ�
            // ����ԭ�����Ƿ��ڸ�ֵ�󻹱�����
            if (context.Node is SimpleAssignmentExpressionSyntax assignExpr)
            {
                // ��ȡ����ʶ������
                if (assignExpr.Left is IdentifierNameSyntax originalIdentifier)
                {
                    // ʹ�� DataFlowAnalysis ��������������б�����ʹ�����
                    var block = assignExpr.FirstAncestorOrSelf<BlockSyntax>();
                    if (block != null)
                    {
                        var dataFlow = context.SemanticModel.AnalyzeDataFlow(block);
                        // ��������ڸ�ֵ����Ȼ��Ϊ "ReadInside" ���֣��򱨸����
                        if (dataFlow.ReadInside.Contains(context.SemanticModel.GetDeclaredSymbol(originalIdentifier)))
                        {
                            var diagnostic = Diagnostic.Create(Rule, originalIdentifier.GetLocation(), originalIdentifier.Identifier.Text);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }

            // ʾ�����ֲ��������������򵥼�⣨ʵ������������������
            if (context.Node is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    // �˴���ģ���߼���������������� "moved" ����Ϊ�ѷ�������Ȩת��
                    // ʵ����Ҫ���ݸ�ֵ�����¼״̬
                    if (variable.Identifier.Text.Contains("moved"))
                    {
                        var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.Text);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        /// <summary>
        /// �жϽڵ��Ƿ��ڴ��� [MSWGC] ��ǵķ����������ڡ�
        /// ������� [MSWGC] ���Ի������ڷ���������
        /// </summary>
        private bool IsInMSWGCScope(SyntaxNode node)
        {
            // �������Ƚڵ㣬���������������������������б�
            for (var current = node; current != null; current = current.Parent)
            {
                if (current is MethodDeclarationSyntax methodDeclaration)
                {
                    foreach (var attrList in methodDeclaration.AttributeLists)
                    {
                        foreach (var attribute in attrList.Attributes)
                        {
                            // ��ƥ�������� "MSWGC"
                            if (attribute.Name.ToString() == "MSWGC" ||
                                attribute.Name.ToString().EndsWith(".MSWGC"))
                            {
                                return true;
                            }
                        }
                    }
                    // ������﷽��������û��ƥ�䣬���˳������������ϲ������ͻ������ռ伶������ԣ�
                    break;
                }
            }
            return false;
        }
    }
}
