using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace SafeManualMemoryManagement.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OwnershipCodeFixProvider)), Shared]
    public class OwnershipCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OwnershipAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // 为每个诊断注册 CodeFix
            var diagnostic = context.Diagnostics[0];
            context.RegisterCodeFix(
                new Microsoft.CodeAnalysis.CodeActions.MyCodeAction(
                    "添加自动销毁代码",
                    ct => AddDestructorAsync(context.Document, diagnostic, ct)),
                diagnostic);
        }

        private async Task<Document> AddDestructorAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            // 示例：在当前代码块的结尾添加一行销毁调用，例如：variable.Dispose();
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null)
                return document;

            // 查找问题定位点所在的块
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);
            var block = node.FirstAncestorOrSelf<BlockSyntax>();
            if (block == null)
                return document;

            // 此处简单示例：对局部变量名称添加销毁调用
            // 注意：实际需要清楚在所有权转移后应插入销毁代码的位置
            var variableName = node.ToString();
            var destroyStatement = SyntaxFactory.ParseStatement($"{variableName}.Dispose();")
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

            // 将销毁代码插入到代码块的末尾
            var newBlock = block.AddStatements(destroyStatement);
            var newRoot = root.ReplaceNode(block, newBlock);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}

// 辅助 CodeAction 实现
namespace Microsoft.CodeAnalysis.CodeActions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class MyCodeAction : CodeAction
    {
        private readonly string _title;
        private readonly Func<CancellationToken, Task<Document>> _createChangedDocument;

        public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument)
        {
            _title = title;
            _createChangedDocument = createChangedDocument;
        }

        public override string Title => _title;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            => _createChangedDocument(cancellationToken);
    }
}
