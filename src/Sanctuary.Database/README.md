# Database Configuration
Sanctuary supports multiple database providers.
- MySQL `Sanctuary.Database.MySql`
- SQLite `Sanctuary.Database.Sqlite`

## Sample Configurations

### MySQL and MariaDB
The MySQL provider also supports MariaDB.

#### MySQL
> :warning: Note:
> If `VersionString` is not populated, the provider will use the default MySQL behavior.

```json
{
  "Database": {
    "Provider": "MySql",
    "ConnectionString": "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;"
  }
}
```

#### MariaDB
> :warning: Note:
> To use MariaDB, set the `VersionString` value in the database configuration to the MariaDB server version.

```json
{
  "Database": {
    "Provider": "MySql",
    "VersionString": "11.6.2-MariaDB",
    "ConnectionString": "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;"
  }
}
```

[More Connection String Examples](https://www.connectionstrings.com/mysql)

#### SQLite
```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=C:/sanctuary.db;"
  }
}
```

[More Connection String Examples](https://www.connectionstrings.com/sqlite)

---

# Database Migrations
Each provider has its own database project and should be used as the startup project when running EF Core migration commands:
- MySQL `Sanctuary.Database.MySql`
- SQLite `Sanctuary.Database.Sqlite`

## Using Package Manager Console (Visual Studio)
> :warning: Note:
> Open the Package Manager Console
> Change the default project to your database provider. Eg: `Sqlite`

### Create your first migration
```powershell
Add-Migration Initial
```

### Remove the latest migration
```powershell
Remove-Migration
```

### Create or update the database and schema
```powershell
Update-Database
```

## Using the Command Line (.NET CLI)
Run the commands from the solution directory and specify the database provider project as the startup project.

> :warning: Note:
Replace `[Provider]` with the database provider you want to use. Eg: `Sqlite`

### Create your first migration
```bash
dotnet ef migrations add Initial --startup-project Sanctuary.Database.[Provider]
```

### Remove the latest migration
```bash
dotnet ef migrations remove --startup-project Sanctuary.Database.[Provider]
```

### Create or update the database and schema
```bash
dotnet ef database update --startup-project Sanctuary.Database.[Provider]
```

## Additional resources
[Managing Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations)