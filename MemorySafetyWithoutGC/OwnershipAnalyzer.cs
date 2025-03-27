using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MemorySafetyWithoutGC
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OwnershipAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OwnershipViolation";
        private static readonly LocalizableString Title = "����Ȩ����";
        private static readonly LocalizableString MessageFormat = "���� '{0}' ������Ȩת�ƺ��Ա�ʹ��";
        private static readonly LocalizableString Description = "�ڴ��� [MSWGC] ��ǵĴ�����ڣ����� safe �����������ڷ��й��ڴ�ı�����Ҫ������ unmanaged Լ���Ҳ��� unsafe �У�����Ȩת�ƺ�Ӧ����ʹ��";
        private const string Category = "Ownership";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

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
            // ���� [MSWGC] ��ǵĴ�����ڽ��з���
            if (!IsInMSWGCScope(context.Node))
                return;

            // ��ֵ���ʽ: ���ԭ��������ࣩ������Ȩת�����
            if (context.Node is AssignmentExpressionSyntax assignExpr &&
                assignExpr.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                if (assignExpr.Left is IdentifierNameSyntax originalIdentifier)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(originalIdentifier).Symbol as ILocalSymbol;
                    if (symbol == null || !IsEligibleSymbol(symbol))
                        return;

                    // ʹ�� DataFlowAnalysis ��������������б�����ʹ�����
                    var block = assignExpr.FirstAncestorOrSelf<BlockSyntax>();
                    if (block != null)
                    {
                        var dataFlow = context.SemanticModel.AnalyzeDataFlow(block);
                        // ��������ڸ�ֵ����Ȼ����ȡ���򱨸����
                        if (dataFlow.ReadInside.Contains(symbol))
                        {
                            var diagnostic = Diagnostic.Create(Rule, originalIdentifier.GetLocation(), originalIdentifier.Identifier.Text);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }                       
        }

        /// <summary>
        /// �жϽڵ��Ƿ��ڴ��� [MSWGC] ��ǵķ����ڡ����Բ��������ڴ������
        /// </summary>
        private bool IsInMSWGCScope(SyntaxNode node)
        {
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
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// �жϱ����Ƿ�Ϊ���ڹ�����й��ڴ�����͡�
        /// ��ʾ���ж����������ͱ������� unmanaged Լ�������ұ�������λ�ñ����� safe �������У����� unsafe �����У���
        /// </summary>
        private bool IsEligibleSymbol(ILocalSymbol symbol)
        {
            var type = symbol.Type;
            // �����ж������Ƿ����� unmanaged Լ��
            if (!type.IsUnmanagedType)
                return false;

            // ���жϱ��������Ƿ��� safe ��������
            var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax != null && IsInUnsafeContext(syntax))
                return false;

            return true;
        }

        /// <summary>
        /// �жϸ����﷨�ڵ��Ƿ��� unsafe �����С�
        /// </summary>
        private bool IsInUnsafeContext(SyntaxNode node)
        {
            for (var current = node; current != null; current = current.Parent)
            {
                if (current is UnsafeStatementSyntax)
                    return true;
            }
            return false;
        }
    }
}
