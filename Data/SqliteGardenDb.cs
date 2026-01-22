using Microsoft.Data.Sqlite;
using System.IO;

namespace SmartGardenApi.Data;

/// <summary>
/// Lightweight SQLite helper (no Entity Framework). Provides connections and ensures schema exists.
/// </summary>
public sealed class SqliteGardenDb
{
    public string ConnectionString { get; }

    public SqliteGardenDb(IConfiguration configuration)
    {
        // Tenta ler da connection string primeiro, depois de variável de ambiente, depois default
        var connectionString = configuration.GetConnectionString("Garden") 
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:Garden")
            ?? "Data Source=garden.db";
        
        // Resolve caminhos relativos para um local persistente
        if (connectionString.StartsWith("Data Source="))
        {
            var dbPath = connectionString.Replace("Data Source=", "").Trim();
            string fullPath;
            
            // Se é um caminho relativo (não começa com /)
            if (!Path.IsPathRooted(dbPath))
            {
                // Tenta usar /home/site/wwwroot (Azure) ou /tmp (fallback)
                string basePath = Directory.Exists("/home/site/wwwroot") 
                    ? "/home/site/wwwroot"
                    : "/tmp";
                
                fullPath = Path.Combine(basePath, dbPath);
            }
            else
            {
                // Caminho absoluto - usa diretamente
                fullPath = dbPath;
            }
            
            // Cria o diretório se não existir (para caminhos absolutos e relativos)
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            connectionString = $"Data Source={fullPath}";
        }
        
        ConnectionString = connectionString;
    }

    public SqliteConnection CreateConnection() => new(ConnectionString);

    public async Task EnsureCreatedAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          PRAGMA foreign_keys = ON;

                          CREATE TABLE IF NOT EXISTS Users (
                              Id INTEGER PRIMARY KEY AUTOINCREMENT,
                              Username TEXT NOT NULL UNIQUE,
                              PasswordHash TEXT NOT NULL
                          );

                          CREATE TABLE IF NOT EXISTS Plants (
                              Id INTEGER PRIMARY KEY AUTOINCREMENT,
                              Name TEXT NOT NULL,
                              Location TEXT NOT NULL,
                              RequiredHumidity REAL NOT NULL,
                              LastWatered TEXT NOT NULL
                          );
                          """;

        await cmd.ExecuteNonQueryAsync();
    }
}
