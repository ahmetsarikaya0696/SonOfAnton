using System.Diagnostics;
using System.Reflection;

namespace SonOfAnton.Backend.Infrastructure;

/// <summary>
/// Docker container'lari otomatik baslatmak icin helper
/// </summary>
public static class DockerHelper
{
    private const string ContainerName = "sonofanton-postgres";
    private const string DbUser = "sonofanton";
    private const string DbPassword = "sonofanton_dev_password";
    private const string DbName = "sonofanton_db";
    private const string KeycloakContainerName = "sonofanton-keycloak";

    /// <summary>
    /// Docker Compose ile servisleri baslatir ve PostgreSQL hazir olana kadar bekler
    /// </summary>
    public static async Task EnsureDockerServicesRunningAsync(ILogger logger)
    {
        // Docker'in calisip calismadigini kontrol et
        if (!await IsDockerRunningAsync())
        {
            logger.LogWarning("Docker calismiyor. Lutfen Docker Desktop'i baslatin.");
            throw new InvalidOperationException("Docker calismiyor. Lutfen Docker Desktop'i baslatin ve uygulamayi yeniden calistirin.");
        }

        logger.LogInformation("Docker calisiyor.");

        // Container'larin durumunu kontrol et
        var isPostgresRunning = await IsContainerRunningAsync(ContainerName);
        
        if (isPostgresRunning)
        {
            logger.LogInformation("PostgreSQL container zaten calisiyor.");
            
            // Yine de PostgreSQL'in baglanti kabul ettiginden emin ol
            await WaitForPostgresReadyAsync(logger, TimeSpan.FromSeconds(30));
        }
        else
        {
            logger.LogInformation("Docker servisleri baslatiliyor...");
            
            // docker-compose.yml dosyasinin yolunu bul
            var composePath = FindDockerComposeFile(logger);
            if (composePath == null)
            {
                throw new FileNotFoundException("docker-compose.yml dosyasi bulunamadi. Lutfen solution root'ta docker-compose.yml dosyasinin oldugunden emin olun.");
            }

            logger.LogInformation("docker-compose.yml bulundu: {Path}", composePath);

            // Docker Compose up calistir
            var result = await RunCommandAsync("docker", $"compose -f \"{composePath}\" up -d postgres", TimeSpan.FromMinutes(3));
            
            if (result.ExitCode == 0)
            {
                logger.LogInformation("Docker servisleri baslatildi.");
                
                // PostgreSQL'in hazir olmasini bekle
                await WaitForPostgresReadyAsync(logger, TimeSpan.FromSeconds(60));
            }
            else
            {
                logger.LogError("Docker servisleri baslatilamadi: {Error}", result.Error);
                throw new InvalidOperationException($"Docker servisleri baslatilamadi: {result.Error}");
            }
        }

        // Kullanici, veritabani ve extension erisimini dogrula/olustur
        await EnsureDatabaseAccessAsync(logger);
    }

    /// <summary>
    /// Docker Compose ile Keycloak servisini baslatir ve hazir olana kadar bekler
    /// </summary>
    public static async Task EnsureKeycloakRunningAsync(ILogger logger)
    {
        // Docker'in calisip calismadigini kontrol et
        if (!await IsDockerRunningAsync())
        {
            logger.LogWarning("Docker calismiyor. Lutfen Docker Desktop'i baslatin.");
            throw new InvalidOperationException("Docker calismiyor. Lutfen Docker Desktop'i baslatin ve uygulamayi yeniden calistirin.");
        }

        logger.LogInformation("Keycloak icin Docker durumu kontrol ediliyor...");

        // Container'in durumunu kontrol et
        var isKeycloakRunning = await IsContainerRunningAsync(KeycloakContainerName);
        
        if (isKeycloakRunning)
        {
            logger.LogInformation("Keycloak container zaten calisiyor.");
            await WaitForKeycloakReadyAsync(logger, TimeSpan.FromSeconds(30));
        }
        else
        {
            logger.LogInformation("Keycloak servisi baslatiliyor...");
            
            // docker-compose.yml dosyasinin yolunu bul
            var composePath = FindDockerComposeFile(logger);
            if (composePath == null)
            {
                throw new FileNotFoundException("docker-compose.yml dosyasi bulunamadi.");
            }

            // Docker Compose up calistir
            var result = await RunCommandAsync("docker", $"compose -f \"{composePath}\" up -d keycloak", TimeSpan.FromMinutes(3));
            
            if (result.ExitCode == 0)
            {
                logger.LogInformation("Keycloak servisi baslatildi.");
                
                // Keycloak'in hazir olmasini bekle (Java baslangici biraz surebilir)
                await WaitForKeycloakReadyAsync(logger, TimeSpan.FromSeconds(60));
            }
            else
            {
                logger.LogError("Keycloak servisi baslatilamadi: {Error}", result.Error);
                throw new InvalidOperationException($"Keycloak servisi baslatilamadi: {result.Error}");
            }
        }
    }

    private static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var result = await RunCommandAsync("docker", "info", TimeSpan.FromSeconds(10));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsContainerRunningAsync(string containerName)
    {
        try
        {
            var result = await RunCommandAsync("docker", $"inspect -f \"{{{{.State.Running}}}}\" {containerName}", TimeSpan.FromSeconds(5));
            return result.ExitCode == 0 && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// PostgreSQL'de kullanici, veritabani ve pgvector extension erisimini dogrular.
    /// Eksik olanlari olusturur.
    /// </summary>
    private static async Task EnsureDatabaseAccessAsync(ILogger logger)
    {
        logger.LogInformation("PostgreSQL kullanici/veritabani erisimi kontrol ediliyor...");

        // 1. Kullanicinin var olup olmadigini kontrol et
        var userCheck = await RunPsqlCommandAsync(
            $"SELECT 1 FROM pg_roles WHERE rolname='{DbUser}'",
            "postgres", logger);

        if (!userCheck.Output.Contains("1"))
        {
            logger.LogWarning("Kullanici '{User}' bulunamadi, olusturuluyor...", DbUser);
            await RunPsqlCommandAsync(
                $"CREATE USER {DbUser} WITH PASSWORD '{DbPassword}' SUPERUSER",
                "postgres", logger);
            logger.LogInformation("Kullanici '{User}' olusturuldu.", DbUser);
        }
        else
        {
            logger.LogInformation("Kullanici '{User}' mevcut.", DbUser);
        }

        // 2. Veritabaninin var olup olmadigini kontrol et
        var dbCheck = await RunPsqlCommandAsync(
            $"SELECT 1 FROM pg_database WHERE datname='{DbName}'",
            "postgres", logger);

        if (!dbCheck.Output.Contains("1"))
        {
            logger.LogWarning("Veritabani '{Database}' bulunamadi, olusturuluyor...", DbName);
            await RunPsqlCommandAsync(
                $"CREATE DATABASE {DbName} OWNER {DbUser}",
                "postgres", logger);
            logger.LogInformation("Veritabani '{Database}' olusturuldu.", DbName);
        }
        else
        {
            logger.LogInformation("Veritabani '{Database}' mevcut.", DbName);
        }

        // 3. pgvector extension'i olustur
        var extCheck = await RunPsqlCommandAsync(
            "SELECT 1 FROM pg_extension WHERE extname='vector'",
            DbName, logger);

        if (!extCheck.Output.Contains("1"))
        {
            logger.LogWarning("pgvector extension bulunamadi, olusturuluyor...");
            await RunPsqlCommandAsync(
                "CREATE EXTENSION IF NOT EXISTS vector",
                DbName, logger);
            logger.LogInformation("pgvector extension olusturuldu.");
        }
        else
        {
            logger.LogInformation("pgvector extension mevcut.");
        }

        // 4. Kullaniciya yetkileri ver
        await RunPsqlCommandAsync(
            $"GRANT ALL PRIVILEGES ON DATABASE {DbName} TO {DbUser}",
            "postgres", logger);
        await RunPsqlCommandAsync(
            $"GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO {DbUser}",
            DbName, logger);
        await RunPsqlCommandAsync(
            $"GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO {DbUser}",
            DbName, logger);
        await RunPsqlCommandAsync(
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO {DbUser}",
            DbName, logger);
        await RunPsqlCommandAsync(
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO {DbUser}",
            DbName, logger);

        logger.LogInformation("PostgreSQL kullanici/veritabani erisimi hazir.");
    }

    /// <summary>
    /// Docker container icinde psql komutu calistirir.
    /// </summary>
    private static async Task<CommandResult> RunPsqlCommandAsync(string sql, string database, ILogger logger)
    {
        // postgres superuser ile baglanti kurup komutu calistir
        var escapedSql = sql.Replace("\"", "\\\"");
        var result = await RunCommandAsync(
            "docker",
            $"exec {ContainerName} psql -U postgres -d {database} -tAc \"{escapedSql}\"",
            TimeSpan.FromSeconds(15));

        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.Error))
        {
            logger.LogDebug("psql komutu hatasi: {Error} (SQL: {Sql})", result.Error.Trim(), sql);
        }

        return result;
    }

    private static string? FindDockerComposeFile(ILogger logger)
    {
        var searchedPaths = new List<string>();
        
        // 1. AppContext.BaseDirectory'den yukari dogru ara
        var currentDir = AppContext.BaseDirectory;
        logger.LogDebug("Baslangic dizini: {Dir}", currentDir);
        
        for (int i = 0; i < 10; i++)
        {
            var composePath = Path.Combine(currentDir, "docker-compose.yml");
            searchedPaths.Add(composePath);
            
            if (File.Exists(composePath))
            {
                return composePath;
            }
            
            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null) break;
            currentDir = parentDir.FullName;
        }
        
        // 2. Assembly Location'dan yukari dogru ara
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            currentDir = Path.GetDirectoryName(assemblyLocation);
            if (currentDir != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    var composePath = Path.Combine(currentDir, "docker-compose.yml");
                    if (!searchedPaths.Contains(composePath))
                    {
                        searchedPaths.Add(composePath);
                        
                        if (File.Exists(composePath))
                        {
                            return composePath;
                        }
                    }
                    
                    var parentDir = Directory.GetParent(currentDir);
                    if (parentDir == null) break;
                    currentDir = parentDir.FullName;
                }
            }
        }
        
        // 3. Current Directory'den yukari dogru ara
        currentDir = Environment.CurrentDirectory;
        for (int i = 0; i < 10; i++)
        {
            var composePath = Path.Combine(currentDir, "docker-compose.yml");
            if (!searchedPaths.Contains(composePath))
            {
                searchedPaths.Add(composePath);
                
                if (File.Exists(composePath))
                {
                    return composePath;
                }
            }
            
            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null) break;
            currentDir = parentDir.FullName;
        }
        
        // Bulunamadi - aranan yollari logla
        logger.LogWarning("docker-compose.yml bulunamadi. Aranan konumlar:");
        foreach (var path in searchedPaths.Distinct())
        {
            logger.LogWarning("  - {Path}", path);
        }
        
        return null;
    }

    private static async Task WaitForPostgresReadyAsync(ILogger logger, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("PostgreSQL'in hazir olmasi bekleniyor (max {Seconds} saniye)...", timeout.TotalSeconds);
        
        while (stopwatch.Elapsed < timeout)
        {
            var result = await RunCommandAsync("docker", $"exec {ContainerName} pg_isready -U postgres", TimeSpan.FromSeconds(5));
            
            if (result.ExitCode == 0)
            {
                logger.LogInformation("PostgreSQL hazir! (Bekleme suresi: {Elapsed:F1} saniye)", stopwatch.Elapsed.TotalSeconds);
                return;
            }
            
            await Task.Delay(1000);
        }
        
        throw new TimeoutException($"PostgreSQL {timeout.TotalSeconds} saniye icinde hazir olmadi. Lutfen Docker loglarini kontrol edin.");
    }

    private static async Task WaitForKeycloakReadyAsync(ILogger logger, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Keycloak'in hazir olmasi bekleniyor (max {Seconds} saniye)...", timeout.TotalSeconds);
        
        using var httpClient = new HttpClient();
        
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                // Keycloak health endpoint kontrolu
                var response = await httpClient.GetAsync("http://localhost:8080/health/ready");
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Keycloak hazir! (Bekleme suresi: {Elapsed:F1} saniye)", stopwatch.Elapsed.TotalSeconds);
                    return;
                }
            }
            catch
            {
                // Henuz baglanti saglanamadi, devam et
            }
            
            await Task.Delay(2000);
        }
        
        throw new TimeoutException($"Keycloak {timeout.TotalSeconds} saniye icinde hazir olmadi. Lutfen Docker loglarini ({KeycloakContainerName}) kontrol edin.");
    }

    private static async Task<CommandResult> RunCommandAsync(string command, string arguments, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(-1, "", "Process timeout");
        }

        return new CommandResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private record CommandResult(int ExitCode, string Output, string Error);
}

