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
        private static readonly LocalizableString Title = "所有权错误";
        private static readonly LocalizableString MessageFormat = "变量 '{0}' 在所有权转移后仍被使用";
        private static readonly LocalizableString Description = "在带有 [MSWGC] 标记的代码块内，仅对 safe 上下文中用于非托管内存的变量（要求满足 unmanaged 约束且不在 unsafe 中）所有权转移后不应继续使用";
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
            // 仅在 [MSWGC] 标记的代码块内进行分析
            if (!IsInMSWGCScope(context.Node))
                return;

            // 赋值表达式: 检查原变量（左侧）的所有权转移情况
            if (context.Node is AssignmentExpressionSyntax assignExpr &&
                assignExpr.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                if (assignExpr.Left is IdentifierNameSyntax originalIdentifier)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(originalIdentifier).Symbol as ILocalSymbol;
                    if (symbol == null || !IsEligibleSymbol(symbol))
                        return;

                    // 使用 DataFlowAnalysis 分析所属代码块中变量的使用情况
                    var block = assignExpr.FirstAncestorOrSelf<BlockSyntax>();
                    if (block != null)
                    {
                        var dataFlow = context.SemanticModel.AnalyzeDataFlow(block);
                        // 如果变量在赋值后仍然被读取，则报告错误
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
        /// 判断节点是否在带有 [MSWGC] 标记的方法内。属性不能作用在代码块中
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
                            // 简单匹配属性名 "MSWGC"
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
        /// 判断变量是否为用于管理非托管内存的类型。
        /// 本示例判定变量的类型必须满足 unmanaged 约束，并且变量声明位置必须在 safe 上下文中（不在 unsafe 语句块中）。
        /// </summary>
        private bool IsEligibleSymbol(ILocalSymbol symbol)
        {
            var type = symbol.Type;
            // 首先判断类型是否满足 unmanaged 约束
            if (!type.IsUnmanagedType)
                return false;

            // 再判断变量声明是否在 safe 上下文中
            var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax != null && IsInUnsafeContext(syntax))
                return false;

            return true;
        }

        /// <summary>
        /// 判断给定语法节点是否在 unsafe 语句块中。
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
