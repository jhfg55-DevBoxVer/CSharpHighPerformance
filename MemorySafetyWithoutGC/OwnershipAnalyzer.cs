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
        private static readonly LocalizableString Title = "所有权错误";
        private static readonly LocalizableString MessageFormat = "变量 '{0}' 被超出生命周期使用";
        private static readonly LocalizableString Description = "在 MSWGC 标记的代码块内，变量超出其所有权生命周期后不应继续使用";
        private const string Category = "Ownership";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // 禁止分析生成代码，提高性能
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // 注册分析局部声明、赋值表达式以及标识符名称
            context.RegisterSyntaxNodeAction(AnalyzeNode,
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.IdentifierName);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // 检查当前语法节点是否在以 MSWGC 标记的代码块内（可基于属性、注释或特定语法实现）
            // 此处仅用伪代码示例：
            if (!IsInMSWGCScope(context.Node))
                return;

            // 这里可以实现对变量生命周期分析的逻辑，例如：
            // - 检查变量赋值后的使用
            // - 如果发生所有权转移（比如赋值运算），报告原变量在后续被使用的问题

            // 示例：如果检测到局部变量声明，则模拟诊断报告
            if (context.Node is LocalDeclarationStatementSyntax localDecl)
            {
                // 假设对声明中的变量进行所有权检查（实际需进一步数据流分析）
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    // 根据分析结果（此示例中总是假定发现错误）
                    var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // 根据你的实际方案，实现如何判断节点是否在 MSWGC 代码块内。
        private bool IsInMSWGCScope(SyntaxNode node)
        {
            // 示例：检查是否包含特定的注释标记，例如 // MSWGC
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
