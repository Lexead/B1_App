using Microsoft.Win32;
using Npgsql;
using OfficeOpenXml;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace B1_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            try
            {
                var folderDialog = new OpenFolderDialog() { Title = "Specify destination folder", InitialDirectory = Environment.CurrentDirectory, FolderName = "files" };
                var folderResult = folderDialog.ShowDialog();
                if (folderResult == true)
                {
                    var folderPath = folderDialog.FolderName;
                    await FileWorker.Write(folderPath: folderPath, filesCount: 10, rowsCount: 1000, maxDegreeOfParallelism: 10, 
                        (int writedRowsCount) =>
                        {
                            Dispatcher.Invoke(() => ProgressGenerate.Content = $"Writed: {writedRowsCount}");
                        });
                }
            }
            finally
            {
                Dispatcher.Invoke(() => ((Button)sender).IsEnabled = true);
            }
        }

        private async void CombineButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            ExcludeMessage.Content = string.Empty;
            var excludeValue = ExcludeTextBox.Text;
            try
            {
                var folderDialog = new OpenFolderDialog() { Title = "Specify folder", InitialDirectory = Environment.CurrentDirectory, FolderName = "files" };
                var folderResult = folderDialog.ShowDialog();
                var fileDialog = new SaveFileDialog() { Title = "Specify destination file", InitialDirectory = Environment.CurrentDirectory, FileName = "output.txt" };
                var fileResult = fileDialog.ShowDialog();
                if (folderResult == true && fileResult == true)
                {
                    if (!string.IsNullOrEmpty(excludeValue))
                    {
                        var folderPath = folderDialog.FolderName;
                        var destFilePath = fileDialog.FileName;
                        await FileWorker.ReadToFile(folderPath: folderPath, destFilePath: destFilePath, excludeValue: excludeValue, maxDegreeOfParallelism: 10,
                            (int writedRowsCount, int excludedRowsCount) =>
                            {
                                Dispatcher.Invoke(() => ProgressCombine.Content = $"Writed: {writedRowsCount}\nExcluded:{excludedRowsCount}");
                            });
                    }
                    else
                    {
                        ExcludeMessage.Content = "Enter exclude value";
                    }
                }
            }
            finally 
            {
                Dispatcher.Invoke(() => ((Button)sender).IsEnabled = true);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            try
            {
                var fileDialog = new OpenFileDialog() { Title = "Specify file", InitialDirectory = Environment.CurrentDirectory, FileName = "output.txt" };
                var fileResult = fileDialog.ShowDialog();
                if (fileResult == true)
                {
                    await FileWorker.ImportToDatabase(filePath: fileDialog.FileName, connectionString: "Host=localhost;Port=5432;Username=postgres;Password=13032001;Database=B1", 
                        maxDegreeOfParallelism: 2, (int imported, int remains) =>
                        {
                            Dispatcher.Invoke(() => ProgressImport.Content = $"Imported: {imported}\nRemains: {remains}");
                        });
                }
            }
            finally
            {
                Dispatcher.Invoke(() => ((Button)sender).IsEnabled = true);
            }
        }

        private async void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Username=postgres;Password=13032001;Database=B1");
            await connection.OpenAsync().ConfigureAwait(false);
            var sqlCommand = "SELECT SUM(even_int), PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY double) as median FROM public.randomrows;";
            await using var command = new NpgsqlCommand(sqlCommand, connection);
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync())
            {
                Dispatcher.Invoke(() => CalculatedResult.Content = $"Even int sum: {reader["sum"]}\nDouble median: {reader["median"]}");
            }
        }

        private async void AddExcelButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            try
            {
                var fileDialog = new OpenFileDialog() { Title = "Specify excel file", InitialDirectory = Environment.CurrentDirectory, Filter = "Excel Files|*.xls;*.xlsx;*.xlsm" };
                var fileResult = fileDialog.ShowDialog();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                if (fileResult == true)
                {
                    LoadedFilesList.Items.Add(fileDialog.FileName);
                    await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Username=postgres;Password=13032001;Database=B1");
                    await connection.OpenAsync().ConfigureAwait(false);
                    var sqlCommand = "INSERT INTO public.balance_accounts(id, incoming_active, incoming_passive, " +
                        "outgoing_active, outgoing_passive, turnovers_debit, turnovers_credit) VALUES " +
                        "(@id, @incoming_active, @incoming_passive, @outgoing_active, @outgoing_passive, " +
                        "@turnovers_debit, @turnovers_credit)";

                    await using var command = new NpgsqlCommand(sqlCommand, connection);
                    command.Parameters.Add(new NpgsqlParameter("id", DbType.Int32));
                    command.Parameters.Add(new NpgsqlParameter("incoming_active", DbType.Decimal));
                    command.Parameters.Add(new NpgsqlParameter("incoming_passive", DbType.Decimal));
                    command.Parameters.Add(new NpgsqlParameter("outgoing_active", DbType.Decimal));
                    command.Parameters.Add(new NpgsqlParameter("outgoing_passive", DbType.Decimal));
                    command.Parameters.Add(new NpgsqlParameter("turnovers_debit", DbType.Decimal));
                    command.Parameters.Add(new NpgsqlParameter("turnovers_credit", DbType.Decimal));

                    using var package = new ExcelPackage(new FileInfo(fileDialog.FileName));
                    var ws = package.Workbook.Worksheets[0];
                    var start = ws.Dimension.Start;
                    var end = ws.Dimension.End;
                    var options = new ParallelOptions() { MaxDegreeOfParallelism = 6 };
                    var semaphore = new SemaphoreSlim(1);
                    await Parallel.ForAsync(start.Row, end.Row, async (int row, CancellationToken cancellationToken) =>
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            if (ws.Cells[row, start.Column].Text.All(ch => char.IsDigit(ch)) && ws.Cells[row, start.Column].Text.Length > 2)
                            {
                                command.Parameters["id"].Value = int.Parse(ws.Cells[row, start.Column].Text);
                                command.Parameters["incoming_active"].Value = decimal.Parse(ws.Cells[row, start.Column + 1].Text);
                                command.Parameters["incoming_passive"].Value = decimal.Parse(ws.Cells[row, start.Column + 2].Text);
                                command.Parameters["outgoing_active"].Value = decimal.Parse(ws.Cells[row, start.Column + 3].Text);
                                command.Parameters["outgoing_passive"].Value = decimal.Parse(ws.Cells[row, start.Column + 4].Text);
                                command.Parameters["turnovers_debit"].Value = decimal.Parse(ws.Cells[row, start.Column + 5].Text);
                                command.Parameters["turnovers_credit"].Value = decimal.Parse(ws.Cells[row, start.Column + 6].Text);
                                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    sqlCommand = "SELECT * FROM public.balance_accounts";
                    var dataAdapter = new NpgsqlDataAdapter(sqlCommand, connection);
                    
                    var dataSet = new DataSet();
                    dataSet.Reset();
                    dataAdapter.Fill(dataSet);
                    Dispatcher.Invoke(() => LoadedData.ItemsSource = dataSet.Tables[0].DefaultView);
                }
            }
            finally
            {
                Dispatcher.Invoke(() => ((Button)sender).IsEnabled = true);
            }
        }
    }
}