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
            // Ϊÿ�����ע�� CodeFix
            var diagnostic = context.Diagnostics[0];
            context.RegisterCodeFix(
                new Microsoft.CodeAnalysis.CodeActions.MyCodeAction(
                    "����Զ����ٴ���",
                    ct => AddDestructorAsync(context.Document, diagnostic, ct)),
                diagnostic);
        }

        private async Task<Document> AddDestructorAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            // ʾ�����ڵ�ǰ�����Ľ�β���һ�����ٵ��ã����磺variable.Dispose();
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null)
                return document;

            // �������ⶨλ�����ڵĿ�
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);
            var block = node.FirstAncestorOrSelf<BlockSyntax>();
            if (block == null)
                return document;

            // �˴���ʾ�����Ծֲ���������������ٵ���
            // ע�⣺ʵ����Ҫ���������Ȩת�ƺ�Ӧ�������ٴ����λ��
            var variableName = node.ToString();
            var destroyStatement = SyntaxFactory.ParseStatement($"{variableName}.Dispose();")
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

            // �����ٴ�����뵽������ĩβ
            var newBlock = block.AddStatements(destroyStatement);
            var newRoot = root.ReplaceNode(block, newBlock);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}

// ���� CodeAction ʵ��
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
