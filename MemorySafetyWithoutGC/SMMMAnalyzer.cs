using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SMMMAnalyzer : DiagnosticAnalyzer
{//DiagnosticDescriptor:定义诊断信息（错误或警告）的描述符。
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        "SMMM001",//诊断 ID。
        "Ownership Mechanism Enforcement",//诊断标题。
        "Method '{0}' marked with [SMMM] should follow ownership rules",//诊断消息格式，{0} 表示方法名称。
        "Ownership",//诊断类别。
        DiagnosticSeverity.Error,//诊断的严重级别，这里是error
        isEnabledByDefault: true);//诊断默认启用

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);//定义该分析器支持的诊断描述符，这里是 Rule

    public override void Initialize(AnalysisContext context)//初始化分析器的配置和注册。
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);//配置生成代码的分析方式，这里不分析生成的代码
        context.EnableConcurrentExecution();//启用并发执行
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);//注册语法节点操作，这里是方法声明节点的分析
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)//定义了对方法声明节点的分析逻辑
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;//获取方法声明语法节点。
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);//获取方法的符号信息。

        if (methodSymbol.GetAttributes().Any(a => a.AttributeClass.Name == "SMMMAttribute"))//检查方法是否有 SMMM 特性
        {
            // 检查方法是否符合所有权机制规则
            // 例如：检查所有权的转移和借用规则
            var diagnostic = Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text);//创建诊断信息
            context.ReportDiagnostic(diagnostic);//报告诊断信息
        }
    }
}
