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
        private static readonly LocalizableString MessageFormat = "���� '{0}' ��������������ʹ��";
        private static readonly LocalizableString Description = "�� MSWGC ��ǵĴ�����ڣ���������������Ȩ�������ں�Ӧ����ʹ��";
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

            // ע������ֲ���������ֵ���ʽ�Լ���ʶ������
            context.RegisterSyntaxNodeAction(AnalyzeNode,
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.IdentifierName);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // ��鵱ǰ�﷨�ڵ��Ƿ����� MSWGC ��ǵĴ�����ڣ��ɻ������ԡ�ע�ͻ��ض��﷨ʵ�֣�
            // �˴�����α����ʾ����
            if (!IsInMSWGCScope(context.Node))
                return;

            // �������ʵ�ֶԱ����������ڷ������߼������磺
            // - ��������ֵ���ʹ��
            // - �����������Ȩת�ƣ����縳ֵ���㣩������ԭ�����ں�����ʹ�õ�����

            // ʾ���������⵽�ֲ�������������ģ����ϱ���
            if (context.Node is LocalDeclarationStatementSyntax localDecl)
            {
                // ����������еı�����������Ȩ��飨ʵ�����һ��������������
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    // ���ݷ����������ʾ�������Ǽٶ����ִ���
                    var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // �������ʵ�ʷ�����ʵ������жϽڵ��Ƿ��� MSWGC ������ڡ�
        private bool IsInMSWGCScope(SyntaxNode node)
        {
            // ʾ��������Ƿ�����ض���ע�ͱ�ǣ����� // MSWGC
            var parent = node;
            while (parent != null)
            {
                if (parent is BlockSyntax block && block.GetLeadingTrivia().ToString().Contains("MSWGC"))
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return false;
        }
    }
}
