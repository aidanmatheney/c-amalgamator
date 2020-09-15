namespace CAmalgamator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class Amalgamator
    {
        private static readonly Regex MainFunctionRegex = new Regex(@"^int main\(", RegexOptions.Multiline);
        private static readonly Regex MainFileHeaderCommentRegex = new Regex(@"^\s*(\/\*(?!\*(?!\/))(?:.|\s)*?\*\/)");

        private static readonly Regex IncludeBuiltInHeaderRegex = new Regex(@"^#include <([^>]+)>\r?\n?", RegexOptions.Multiline);
        private static readonly Regex IncludeLocalHeaderRegex = new Regex(@"^#include ""([^""]+)""\r?\n?", RegexOptions.Multiline);
        private static readonly Regex PragmaRegex = new Regex(@"^#pragma .+\r?\n?", RegexOptions.Multiline);

        private static readonly Regex ConsecutiveNewLineRegex = new Regex(@"(?:\r?\n){3,}");

        private static readonly string SeparatorComment = new string('/', 80);

        private readonly bool _printFileNameComments;

        public Amalgamator(bool printFileNameComments = false) => _printFileNameComments = printFileNameComments;

        public async Task<string> CreateSingleFileSourceAsync(string projectDirectoryPath, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(projectDirectoryPath, nameof(projectDirectoryPath));

            var headerFilePaths = GetHeaderFilePaths(projectDirectoryPath);
            var implementationFilePaths = GetImplementationFilePaths(projectDirectoryPath);
            var mainFilePath = await GetMainFilePathAsync(implementationFilePaths, cancellationToken).ConfigureAwait(false);

            var singleFileSourceBuilder = new StringBuilder();

            var mainFileHeaderComment = await GetMainFileHeaderCommentAsync(mainFilePath, cancellationToken).ConfigureAwait(false);
            if (!(mainFileHeaderComment is null))
            {
                singleFileSourceBuilder.AppendLine(mainFileHeaderComment);
                singleFileSourceBuilder.AppendLine();
            }

            var builtInIncludes = await GetBuiltInIncludesAsync(headerFilePaths, implementationFilePaths, cancellationToken).ConfigureAwait(false);
            if (builtInIncludes.Count > 0)
            {
                foreach (var include in builtInIncludes.OrderByElement())
                {
                    singleFileSourceBuilder.AppendFormat("#include <{0}>{1}", include, Environment.NewLine);
                }

                singleFileSourceBuilder.AppendLine();
            }

            var headerOrder = await GetHeaderOrderAsync(headerFilePaths, cancellationToken).ConfigureAwait(false);
            foreach (var headerFilePath in headerOrder)
            {
                await AppendSourceFileAsync(singleFileSourceBuilder, headerFilePath, cancellationToken).ConfigureAwait(false);
            }

            foreach (var implementationFilePath in implementationFilePaths.Where(path => path != mainFilePath))
            {
                await AppendSourceFileAsync(singleFileSourceBuilder, implementationFilePath, cancellationToken).ConfigureAwait(false);
            }

            await AppendSourceFileAsync(singleFileSourceBuilder, mainFilePath, cancellationToken).ConfigureAwait(false);

            var singleFileSource = $"{RemoveExtraWhitespace(singleFileSourceBuilder.ToString())}{Environment.NewLine}";
            return singleFileSource;
        }

        private static List<string> GetHeaderFilePaths(string projectDirectoryPath)
        {
            var includeDirectoryPath = Path.Combine(projectDirectoryPath, "include");
            if (!Directory.Exists(includeDirectoryPath))
            {
                throw new AmalgamatorException($"include directory (\"{includeDirectoryPath}\") not found");
            }

            var headerFilePaths = Directory.EnumerateFiles(includeDirectoryPath, "*.h", SearchOption.AllDirectories).ToList();
            return headerFilePaths;
        }

        private static List<string> GetImplementationFilePaths(string projectDirectoryPath)
        {
            var srcDirectoryPath = Path.Combine(projectDirectoryPath, "src");
            if (!Directory.Exists(srcDirectoryPath))
            {
                throw new AmalgamatorException($"src directory (\"{srcDirectoryPath}\") not found");
            }

            var implementationFilePaths = Directory.EnumerateFiles(srcDirectoryPath, "*.c", SearchOption.AllDirectories).ToList();
            return implementationFilePaths;
        }

        private static async Task<string> GetMainFilePathAsync(IEnumerable<string> implementationFilePaths, CancellationToken cancellationToken)
        {
            var mainFilePaths = await implementationFilePaths.AsAsync()
                .SelectAwait(async filePath =>
                {
                    var fileText = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var hasMainFunction = MainFunctionRegex.IsMatch(fileText);

                    return new
                    {
                        FilePath = filePath,
                        HasMainFunction = hasMainFunction
                    };
                })
                .Where(file => file.HasMainFunction)
                .Select(file => file.FilePath)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (mainFilePaths.Count == 0)
            {
                throw new AmalgamatorException("No main function was found");
            }

            if (mainFilePaths.Count > 1)
            {
                throw new AmalgamatorException($"Multiple main functions found at the following file paths:{Environment.NewLine}{string.Join(Environment.NewLine, mainFilePaths)}");
            }

            return mainFilePaths[0];
        }

        private static async Task<string?> GetMainFileHeaderCommentAsync(string mainFilePath, CancellationToken cancellationToken)
        {
            var mainFileSource = await File.ReadAllTextAsync(mainFilePath, cancellationToken).ConfigureAwait(false);
            var match = MainFileHeaderCommentRegex.Match(mainFileSource);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static async Task<ISet<string>> GetBuiltInIncludesAsync
        (
            IEnumerable<string> headerFilePaths,
            IEnumerable<string> implementationFilePaths,
            CancellationToken cancellationToken
        )
        {
            var includes = new HashSet<string>();
            foreach (var filePath in headerFilePaths.Concat(implementationFilePaths))
            {
                var source = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                foreach (Match? includeBuiltInHeaderMatch in IncludeBuiltInHeaderRegex.Matches(source))
                {
                    var builtInHeaderPath = includeBuiltInHeaderMatch!.Groups[1].Value;
                    includes.Add(builtInHeaderPath);
                }
            }

            return includes;
        }

        private static async Task<IList<string>> GetHeaderOrderAsync(IEnumerable<string> headerFilePaths, CancellationToken cancellationToken)
        {
            var headerOrderWithDuplicates = new Stack<string>();

            async Task AddHeaderAndDependencies(string filePath)
            {
                headerOrderWithDuplicates!.Push(filePath);

                var directoryPath = Path.GetDirectoryName(filePath)!;
                var source = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                foreach (Match? includeLocalHeaderMatch in IncludeLocalHeaderRegex.Matches(source))
                {
                    var dependencyRelativeFilePath = includeLocalHeaderMatch!.Groups[1].Value;
                    var dependencyFilePath = Path.GetFullPath(Path.Combine(directoryPath, dependencyRelativeFilePath));
                    await AddHeaderAndDependencies(dependencyFilePath).ConfigureAwait(false);
                }
            }

            foreach (var filePath in headerFilePaths)
            {
                await AddHeaderAndDependencies(filePath).ConfigureAwait(false);
            }

            var headerOrder = new List<string>();
            var addedHeaders = new HashSet<string>();
            while (headerOrderWithDuplicates.TryPop(out var headerFilePath))
            {
                if (!addedHeaders.Contains(headerFilePath))
                {
                    headerOrder.Add(headerFilePath);
                    addedHeaders.Add(headerFilePath);
                }
            }

            return headerOrder;
        }

        private async Task AppendSourceFileAsync(StringBuilder singleFileSourceBuilder, string filePath, CancellationToken cancellationToken)
        {
            if (_printFileNameComments)
            {
                singleFileSourceBuilder.AppendLine(SeparatorComment);
                singleFileSourceBuilder.AppendFormat("// {0}{1}", filePath, Environment.NewLine);
                singleFileSourceBuilder.AppendLine(SeparatorComment);
                singleFileSourceBuilder.AppendLine();
            }

            var source = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var processedSource = RemoveSourceDirectives(source);
            singleFileSourceBuilder.AppendLine(processedSource);

            singleFileSourceBuilder.AppendLine();
        }

        private static string RemoveSourceDirectives(string source)
        {
            var alteredSource = source;
            alteredSource = IncludeLocalHeaderRegex.Replace(alteredSource, string.Empty);
            alteredSource = IncludeBuiltInHeaderRegex.Replace(alteredSource, string.Empty);
            alteredSource = PragmaRegex.Replace(alteredSource, string.Empty);

            return alteredSource;
        }

        private static string RemoveExtraWhitespace(string source)
        {
            var alteredSource = source;
            alteredSource = ConsecutiveNewLineRegex.Replace(alteredSource, $"{Environment.NewLine}{Environment.NewLine}");
            alteredSource = alteredSource.Trim();

            return alteredSource;
        }
    }
}