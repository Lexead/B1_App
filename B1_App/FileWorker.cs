using Npgsql;
using Npgsql.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B1_App
{
    public static class FileWorker
    {
        public static async Task Write(string folderPath, int filesCount, int rowsCount, int maxDegreeOfParallelism, Action<int> progressAction)
        {
            int writedRowsCount = 0;
            var options = new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            var semaphore = new SemaphoreSlim(1);
            await Parallel.ForAsync(0, filesCount, options, async(int fileIter, CancellationToken cancellationToken) =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var strBuilder = new StringBuilder();
                    var current = DateTime.Now;
                    var fiveYearsAgo = current.AddYears(-5);
                    for (int rowIter = 0; rowIter < rowsCount; rowIter++)
                    {
                        strBuilder.Append(RandomGenerator.DateBetween(fiveYearsAgo, current).ToString("d"));
                        strBuilder.Append("||");
                        strBuilder.Append(RandomGenerator.EngString(10));
                        strBuilder.Append("||");
                        strBuilder.Append(RandomGenerator.RusString(10));
                        strBuilder.Append("||");
                        strBuilder.Append(RandomGenerator.EvenIntNumber(1, 100_000_000));
                        strBuilder.Append("||");
                        strBuilder.Append(RandomGenerator.DoubleNumber(1, 20));
                        strBuilder.Append("||");
                        strBuilder.AppendLine();
                        Interlocked.Increment(ref writedRowsCount);
                        progressAction.Invoke(writedRowsCount);
                    }
                    using var fileStream = new FileStream(Path.Combine(folderPath, $"file{fileIter + 1}.txt"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(fileStream, Encoding.UTF8);
                    await writer.WriteAsync(strBuilder, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public static async Task ReadToFile(string folderPath, string destFilePath, string excludeValue, int maxDegreeOfParallelism, Action<int, int> progressAction)
        {
            var files = Directory.EnumerateFiles(folderPath);
            int excludedRowsIndex = 0;
            int writedRowsCount = 0;

            using var fileStream = new FileStream(destFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(fileStream, Encoding.UTF8);

            var options = new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            var semaphore = new SemaphoreSlim(1);
            await Parallel.ForEachAsync(files, options, async (string filePath, CancellationToken cancellationToken) =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fileStream, Encoding.UTF8);

                    string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var strBuilder = new StringBuilder();
                    foreach (var row in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!row.Contains(excludeValue))
                        {
                            strBuilder.AppendLine(row);
                            Interlocked.Increment(ref writedRowsCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref excludedRowsIndex);
                        }
                        progressAction.Invoke(writedRowsCount, excludedRowsIndex);

                    }
                    await writer.WriteAsync(strBuilder, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public static async Task ImportToDatabase(string filePath, string connectionString, int maxDegreeOfParallelism, Action<int, int> progressAction)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            int operationIndex = 0;

            string text = await reader.ReadToEndAsync().ConfigureAwait(false);
            var strBuilder = new StringBuilder();
            var rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var sqlCommand = "INSERT INTO public.randomrows(id, date, eng, rus, even_int, double) VALUES (@id, @date, @eng, @rus, " +
                "@even_int, @double)";

            await using var command = new NpgsqlCommand(sqlCommand, connection);
            command.Parameters.Add(new NpgsqlParameter("id", System.Data.DbType.Int32));
            command.Parameters.Add(new NpgsqlParameter("date", System.Data.DbType.Date));
            command.Parameters.Add(new NpgsqlParameter("eng", System.Data.DbType.String));
            command.Parameters.Add(new NpgsqlParameter("rus", System.Data.DbType.String));
            command.Parameters.Add(new NpgsqlParameter("even_int", System.Data.DbType.Int32));
            command.Parameters.Add(new NpgsqlParameter("double", System.Data.DbType.Double));

            var options = new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            var semaphore = new SemaphoreSlim(1);
            int rowIter = 0;
            await Parallel.ForEachAsync(rows, options, async (string row, CancellationToken cancellationToken) =>
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var values = row.Split("||");
                    rowIter++;
                    command.Parameters["id"].Value = rowIter;
                    command.Parameters["date"].Value = DateTime.ParseExact(values[0], "dd.MM.yyyy", CultureInfo.InvariantCulture);
                    command.Parameters["eng"].Value = values[1];
                    command.Parameters["rus"].Value = values[2];
                    command.Parameters["even_int"].Value = int.Parse(values[3]);
                    command.Parameters["double"].Value = double.Parse(values[4]);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    Interlocked.Increment(ref operationIndex);
                    progressAction.Invoke(operationIndex, rows.Length - operationIndex);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }
    }
}
