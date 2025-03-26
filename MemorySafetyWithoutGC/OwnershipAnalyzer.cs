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
        private static readonly LocalizableString MessageFormat = "变量 '{0}' 在所有权转移后仍被使用";
        private static readonly LocalizableString Description = "在带有 [MSWGC] 标记的代码块内，变量在所有权转移后不应继续使用";
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

            // 分析局部声明、赋值表达式以及标识符名称
            context.RegisterSyntaxNodeAction(AnalyzeNode,
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.IdentifierName);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // 仅在 [MSWGC] 标记的代码块内生效
            if (!IsInMSWGCScope(context.Node))
                return;

            // 示例：假设在赋值表达式中发生所有权转移，
            // 则检测原变量是否在赋值后还被引用
            if (context.Node is SimpleAssignmentExpressionSyntax assignExpr)
            {
                // 获取左侧标识符名称
                if (assignExpr.Left is IdentifierNameSyntax originalIdentifier)
                {
                    // 使用 DataFlowAnalysis 分析所属代码块中变量的使用情况
                    var block = assignExpr.FirstAncestorOrSelf<BlockSyntax>();
                    if (block != null)
                    {
                        var dataFlow = context.SemanticModel.AnalyzeDataFlow(block);
                        // 如果变量在赋值后仍然作为 "ReadInside" 出现，则报告错误
                        if (dataFlow.ReadInside.Contains(context.SemanticModel.GetDeclaredSymbol(originalIdentifier)))
                        {
                            var diagnostic = Diagnostic.Create(Rule, originalIdentifier.GetLocation(), originalIdentifier.Identifier.Text);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }

            // 示例：局部变量声明中做简单检测（实际须结合数据流分析）
            if (context.Node is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    // 此处简单模拟逻辑：如果变量名包含 "moved" 则认为已发生所有权转移
                    // 实际需要根据赋值情况记录状态
                    if (variable.Identifier.Text.Contains("moved"))
                    {
                        var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.Text);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        /// <summary>
        /// 判断节点是否在带有 [MSWGC] 标记的方法或代码块内。
        /// 这里假设 [MSWGC] 属性会作用于方法声明。
        /// </summary>
        private bool IsInMSWGCScope(SyntaxNode node)
        {
            // 遍历祖先节点，如果遇到方法声明，检查其属性列表
            for (var current = node; current != null; current = current.Parent)
            {
                if (current is MethodDeclarationSyntax methodDeclaration)
                {
                    foreach (var attrList in methodDeclaration.AttributeLists)
                    {
                        foreach (var attribute in attrList.Attributes)
                        {
                            // 简单匹配属性名 "MSWGC"
                            if (attribute.Name.ToString() == "MSWGC" ||
                                attribute.Name.ToString().EndsWith(".MSWGC"))
                            {
                                return true;
                            }
                        }
                    }
                    // 如果到达方法声明而没有匹配，则退出遍历（不向上查找类型或命名空间级别的属性）
                    break;
                }
            }
            return false;
        }
    }
}
