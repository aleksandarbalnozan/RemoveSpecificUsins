using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.MSBuild;

namespace RemoveSpecificUsings
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: RemoveSpecificUsings <path to the solution> <using namespace to remove>");
                return;
            }

            var solutionPath = args[0];
            var namespaceToRemove = args[1];
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);

            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    var root = await document.GetSyntaxRootAsync();
                    var model = await document.GetSemanticModelAsync();

                    var newRoot = RemoveSpecificUnusedUsings(root, model, namespaceToRemove, workspace);
                    if (newRoot != root)
                    {
                        solution = solution.WithDocumentSyntaxRoot(document.Id, newRoot);
                    }
                }
            }

            workspace.TryApplyChanges(solution);
            Console.WriteLine($"Unused '{namespaceToRemove}' using directives have been removed.");
        }

        private static SyntaxNode RemoveSpecificUnusedUsings(SyntaxNode root, SemanticModel model, string namespaceToRemove, Workspace workspace)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Where(u => u.Name.ToString().StartsWith(namespaceToRemove)).ToList();

            var diagnostics = model.GetDiagnostics();

            var unusedUsings = usings.Where(u =>
                diagnostics.Any(d => d.Location.SourceSpan.IntersectsWith(u.Span) && d.Id == "CS8019")).ToList();

            var editor = new SyntaxEditor(root, workspace);
            foreach (var unusedUsing in unusedUsings)
            {
                editor.RemoveNode(unusedUsing);
            }

            return editor.GetChangedRoot();
        }
    }
}
