namespace CSingleFiler
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Debug.Assert(!(args is null));

            if (args.Length != 1)
            {
                await Console.Error.WriteLineAsync("Project directory path must be specified as the single argument").ConfigureAwait(false);
                return 1;
            }

            var projectDirectoryPath = args[0];

            var singleFiler = new SingleFiler();
            var singleFileSource = await singleFiler.CreateSingleFileSourceAsync(projectDirectoryPath).ConfigureAwait(false);
            await Console.Out.WriteLineAsync(singleFileSource).ConfigureAwait(false);

            return 0;
        }
    }
}