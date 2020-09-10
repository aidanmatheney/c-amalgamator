namespace CSingleFiler
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Debug.Assert(!(args is null));

            if (args.Length > 1)
            {
                await Console.Error.WriteLineAsync("Optional single argument: project directory path").ConfigureAwait(false);
                return 1;
            }

            var projectDirectoryPath = args.Length == 1 ? args[0] : Directory.GetCurrentDirectory();

            var singleFiler = new SingleFiler();
            var singleFileSource = await singleFiler.CreateSingleFileSourceAsync(projectDirectoryPath).ConfigureAwait(false);
            await Console.Out.WriteLineAsync(singleFileSource).ConfigureAwait(false);

            return 0;
        }
    }
}